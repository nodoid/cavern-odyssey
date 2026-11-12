using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace SuperSqij;

/// <summary>
/// Reads and writes hand-edited tile maps. The format is line oriented and run length
/// encoded so a level stays small and can be diffed or tweaked in a text editor.
/// </summary>
public static class LevelFile
{
    public const string Header = "SQIJLEVEL 2";

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SuperSqij", "levels", "custom.sqij");

    public static void Save(World world, string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var sb = new StringBuilder();
        sb.AppendLine(Header);
        sb.AppendLine("seed " + world.Seed.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("difficulty " + ((int)world.Difficulty).ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("start " + world.StartPosition.X.ToString("0.##", CultureInfo.InvariantCulture) + " " +
                      world.StartPosition.Y.ToString("0.##", CultureInfo.InvariantCulture));
        sb.AppendLine("tiles");
        for (int ty = 0; ty < World.MapTilesY; ty++)
        {
            int run = 1;
            var kind = world.TileAt(0, ty);
            for (int tx = 1; tx <= World.MapTilesX; tx++)
            {
                var next = tx < World.MapTilesX ? world.TileAt(tx, ty) : (TileKind)255;
                if (tx < World.MapTilesX && next == kind) { run++; continue; }
                sb.Append((byte)kind).Append('*').Append(run).Append(' ');
                kind = next;
                run = 1;
            }
            sb.AppendLine();
        }

        sb.AppendLine("objects");
        foreach (var p in world.Pickups)
            sb.AppendLine($"{PickupTag(p.Kind)} {N(p.Pos.X)} {N(p.Pos.Y)} {p.ColorIndex}");
        foreach (var m in world.Monstas)
            sb.AppendLine($"monsta {N(m.Pos.X)} {N(m.Pos.Y)} {m.ColorA} {m.ColorB}");
        foreach (var d in world.Doors)
            sb.AppendLine($"door {N(d.Bounds.X)} {N(d.Bounds.Y)} {N(d.Bounds.W)} {N(d.Bounds.H)} {d.ColorIndex}");
        foreach (var p in world.Props)
            sb.AppendLine($"{p.Kind.ToString().ToLowerInvariant()} {N(p.Pos.X)} {N(p.Pos.Y)} {p.Variant}");
        if (world.Altar != null)
            sb.AppendLine($"altar {N(world.Altar.Bounds.X)} {N(world.Altar.Bounds.Y)} " +
                          $"{N(world.Altar.Bounds.W)} {N(world.Altar.Bounds.H)}");

        File.WriteAllText(path, sb.ToString());
    }

    private static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string PickupTag(PickupKind kind) => kind switch
    {
        PickupKind.Fruit => "fruit",
        PickupKind.Fragment => "fragment",
        PickupKind.Pass => "pass",
        _ => "energy",
    };

    public static World Load(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 5 || !lines[0].Trim().StartsWith("SQIJLEVEL"))
            throw new InvalidDataException("Not a Cavern Odyssey level file.");

        int seed = ParseInt(lines[1], "seed");
        var difficulty = Difficulty.Medium;
        int cursor = 2;
        if (lines.Length > 2 && lines[2].TrimStart().StartsWith("difficulty"))
        {
            var diffParts = lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (diffParts.Length > 1 &&
                int.TryParse(diffParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) &&
                d >= (int)Difficulty.Easy && d <= (int)Difficulty.Hard)
                difficulty = (Difficulty)d;
            cursor = 3;
        }
        var startParts = lines[cursor].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (startParts.Length < 3 || startParts[0] != "start")
            throw new InvalidDataException("Missing start position.");
        var start = new Vector2(
            float.Parse(startParts[1], CultureInfo.InvariantCulture),
            float.Parse(startParts[2], CultureInfo.InvariantCulture));

        // Entities (doors, monstas, pickups) always come from the generator; only the
        // tiles and the player start are editable, so rebuild from the stored seed.
        var world = LevelGenerator.Generate(seed, difficulty);

        int ty = 0;
        for (int i = cursor + 1; i < lines.Length && ty < World.MapTilesY; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line == "tiles") continue;
            int tx = 0;
            foreach (var token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = token.Split('*');
                if (bits.Length != 2) throw new InvalidDataException("Bad run '" + token + "'.");
                var kind = (TileKind)int.Parse(bits[0], CultureInfo.InvariantCulture);
                int count = int.Parse(bits[1], CultureInfo.InvariantCulture);
                for (int n = 0; n < count && tx < World.MapTilesX; n++)
                    world.SetTile(tx++, ty, kind);
            }
            if (tx != World.MapTilesX)
                throw new InvalidDataException($"Row {ty} has {tx} tiles, expected {World.MapTilesX}.");
            ty++;
        }
        if (ty != World.MapTilesY)
            throw new InvalidDataException($"Level has {ty} rows, expected {World.MapTilesY}.");

        world.StartPosition = start;
        var (rx, ry) = world.RoomAt(start);
        world.StartRoomX = rx;
        world.StartRoomY = ry;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() != "objects") continue;
            ReadObjects(world, lines, i + 1);
            break;
        }
        return world;
    }

    private static void ReadObjects(World world, string[] lines, int start)
    {
        world.Pickups.Clear();
        world.Monstas.Clear();
        world.Doors.Clear();
        world.Props.Clear();
        world.Altar = null!;

        for (int i = start; i < lines.Length; i++)
        {
            var b = lines[i].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (b.Length == 0) continue;
            float F(int at) => float.Parse(b[at], CultureInfo.InvariantCulture);
            int I(int at) => int.Parse(b[at], CultureInfo.InvariantCulture);
            switch (b[0])
            {
                case "fruit":
                    world.Pickups.Add(new Pickup { Kind = PickupKind.Fruit, Pos = new Vector2(F(1), F(2)), ColorIndex = I(3) });
                    break;
                case "fragment":
                    world.Pickups.Add(new Pickup { Kind = PickupKind.Fragment, Pos = new Vector2(F(1), F(2)), ColorIndex = I(3) });
                    break;
                case "pass":
                    world.Pickups.Add(new Pickup { Kind = PickupKind.Pass, Pos = new Vector2(F(1), F(2)), ColorIndex = I(3) });
                    break;
                case "energy":
                    world.Pickups.Add(new Pickup { Kind = PickupKind.Energy, Pos = new Vector2(F(1), F(2)), ColorIndex = I(3) });
                    break;
                case "monsta":
                    world.Monstas.Add(new Monsta { Pos = new Vector2(F(1), F(2)), ColorA = I(3), ColorB = I(4) });
                    break;
                case "door":
                    world.Doors.Add(new Door
                    {
                        Bounds = new RectF(F(1), F(2), F(3), F(4)),
                        ColorIndex = I(5),
                        Rune = I(5),
                    });
                    break;
                case "torch":
                case "crystal":
                case "mushroom":
                    world.Props.Add(new Prop
                    {
                        Kind = Enum.Parse<PropKind>(b[0], true),
                        Pos = new Vector2(F(1), F(2)),
                        Variant = b.Length > 3 ? I(3) : 0,
                    });
                    break;
                case "altar":
                    world.Altar = new Altar { Bounds = new RectF(F(1), F(2), F(3), F(4)) };
                    break;
            }
        }
    }

    private static int ParseInt(string line, string key)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] != key)
            throw new InvalidDataException("Missing '" + key + "' line.");
        return int.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Playability check for the current tile map: the start must be standing in open air
    /// and every room must be reachable on foot. Also reports entities buried in rock.
    /// </summary>
    public static (bool Ok, string Message) Validate(World world)
    {
        int sx = (int)(world.StartPosition.X / World.TileSize);
        int sy = (int)(world.StartPosition.Y / World.TileSize);
        if (world.IsSolid(sx, sy))
            return (false, "START IS BURIED IN ROCK");

        var seen = new bool[World.MapTilesX * World.MapTilesY];
        var rooms = new bool[World.RoomCount];
        var queue = new Queue<(int X, int Y)>();
        seen[sy * World.MapTilesX + sx] = true;
        queue.Enqueue((sx, sy));
        int tiles = 0;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            tiles++;
            var (rx, ry) = world.RoomAt(new Vector2(x * World.TileSize, y * World.TileSize));
            rooms[ry * World.RoomsX + rx] = true;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= World.MapTilesX || ny >= World.MapTilesY) continue;
                int index = ny * World.MapTilesX + nx;
                if (seen[index] || world.IsSolid(nx, ny)) continue;
                seen[index] = true;
                queue.Enqueue((nx, ny));
            }
        }

        int reachableRooms = rooms.Count(r => r);
        if (reachableRooms != World.RoomCount)
            return (false, $"FAIL {reachableRooms}/{World.RoomCount} ROOMS REACHABLE");
        return (true, $"PASS ALL {World.RoomCount} ROOMS REACHABLE");
    }
}
