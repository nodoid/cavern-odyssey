using System;
using System.Collections.Generic;
using System.Linq;
using SuperSqij;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Contains("--verify"))
        {
            Verify();
            return;
        }

        using var game = new SuperSqijGame();
        // --capture <file> <width> <height> <frames>: dump raw RGBA frames for the
        // gameplay videos (see tools/capture_video.sh).
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && args.Length > capture + 4)
            game.RequestCapture(args[capture + 1], int.Parse(args[capture + 2]),
                int.Parse(args[capture + 3]), int.Parse(args[capture + 4]),
                args.Length > capture + 5 ? int.Parse(args[capture + 5]) : 0);
        game.Run();
    }

    private static void Verify()
    {
        var world = LevelGenerator.Generate(1986);
        Console.WriteLine($"seed={world.Seed}");
        Console.WriteLine($"rooms={World.RoomCount} doors={world.Doors.Count} monstas={world.Monstas.Count}");
        Console.WriteLine($"fragments={world.Pickups.Count(p => p.Kind == PickupKind.Fragment)}");
        Console.WriteLine($"fruits={world.Pickups.Count(p => p.Kind == PickupKind.Fruit)}");
        Console.WriteLine($"enemies={world.Enemies.Count} props={world.Props.Count}");
        Console.WriteLine($"map={World.MapWidth}x{World.MapHeight} start={world.StartPosition}");

        // 1. Geometry flood fill ignoring doors: every room should contain reachable floor.
        var openRooms = FloodRooms(world, respectDoors: false, out int openTiles);
        Console.WriteLine($"geometry-reachable rooms={openRooms.Count}/{World.RoomCount} tiles={openTiles}");

        // 2. Flood fill with doors shut: exactly the vault rooms must be sealed off.
        var gatedRooms = FloodRooms(world, respectDoors: true, out _);
        var sealedRooms = Enumerable.Range(0, World.RoomCount).Where(i => !gatedRooms.Contains(i)).ToArray();
        var vaultRooms = Enumerable.Range(0, World.RoomCount).Where(i => world.Rooms[i].Vault).ToArray();
        Console.WriteLine($"sealed={sealedRooms.Length} vaults={vaultRooms.Length} " +
                          $"match={sealedRooms.OrderBy(i => i).SequenceEqual(vaultRooms.OrderBy(i => i))}");

        // 3. Puzzle economy: every rune colour must have as many doors as obtainable passes.
        var doorColors = world.Doors.GroupBy(d => d.ColorIndex).Select(g => g.Count()).ToArray();
        var monstaColors = new int[6];
        foreach (var m in world.Monstas) { monstaColors[m.ColorA]++; monstaColors[m.ColorB]++; }
        int doorsPerColour = World.DoorTotal / 6;
        int passesPerColour = World.MonstaTotal * 2 / 6;
        Console.WriteLine("doors per colour: " + string.Join(",", doorColors));
        Console.WriteLine("passes per colour: " + string.Join(",", monstaColors));
        Console.WriteLine("puzzle balanced=" + doorColors.All(c => c == doorsPerColour) +
                          " passes=" + monstaColors.All(c => c == passesPerColour));

        // 4. Nothing that is needed for progress may sit inside a locked vault.
        int fruitInVault = world.Pickups.Count(p => p.Kind == PickupKind.Fruit &&
            world.Rooms[RoomIndex(world, p.Pos)].Vault);
        int monstaInVault = world.Monstas.Count(m => world.Rooms[RoomIndex(world, m.Pos)].Vault);
        Console.WriteLine($"fruit in vault={fruitInVault} monstas in vault={monstaInVault}");

        // 5. Vault rooms each hold exactly one fragment.
        var fragmentsInVaults = world.Pickups.Count(p => p.Kind == PickupKind.Fragment &&
            world.Rooms[RoomIndex(world, p.Pos)].Vault);
        Console.WriteLine($"fragments inside vaults={fragmentsInVaults}");

        // 6. Every difficulty must generate a complete level.
        bool allDifficulties = true;
        foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard })
        {
            var d = LevelGenerator.Generate(1986, difficulty);
            bool complete = d.Doors.Count == World.DoorTotal && d.Monstas.Count == World.MonstaTotal &&
                            d.Pickups.Count(p => p.Kind == PickupKind.Fragment) == World.FragmentTotal &&
                            d.Pickups.Count(p => p.Kind == PickupKind.Fruit) == World.FruitTotal;
            Console.WriteLine($"difficulty {difficulty.Name()}: doors={d.Doors.Count} monstas={d.Monstas.Count} " +
                              $"enemies={d.Enemies.Count} complete={complete}");
            allDifficulties &= complete;
        }

        bool ok = allDifficulties &&
                  openRooms.Count == World.RoomCount && sealedRooms.Length == World.DoorTotal &&
                  doorColors.All(c => c == doorsPerColour) &&
                  monstaColors.All(c => c == passesPerColour) &&
                  fruitInVault == 0 && monstaInVault == 0 && fragmentsInVaults == World.FragmentTotal;
        Console.WriteLine(ok ? "VERIFY: PASS" : "VERIFY: FAIL");
    }

    private static int RoomIndex(World w, Microsoft.Xna.Framework.Vector2 p)
    {
        var (x, y) = w.RoomAt(p);
        return y * World.RoomsX + x;
    }

    private static HashSet<int> FloodRooms(World world, bool respectDoors, out int tileCount)
    {
        var startTx = (int)(world.StartPosition.X / World.TileSize);
        var startTy = (int)(world.StartPosition.Y / World.TileSize);
        var seen = new bool[World.MapTilesX * World.MapTilesY];
        var rooms = new HashSet<int>();
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startTx, startTy));
        seen[startTy * World.MapTilesX + startTx] = true;
        int count = 0;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            count++;
            var (rx, ry) = world.RoomAt(new Microsoft.Xna.Framework.Vector2(x * World.TileSize, y * World.TileSize));
            rooms.Add(ry * World.RoomsX + rx);
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= World.MapTilesX || ny >= World.MapTilesY) continue;
                if (seen[ny * World.MapTilesX + nx]) continue;
                if (world.IsSolid(nx, ny)) continue;
                if (respectDoors)
                {
                    var centre = new Microsoft.Xna.Framework.Vector2(nx * World.TileSize + 8, ny * World.TileSize + 8);
                    if (world.Doors.Any(d => !d.Open && d.Bounds.Contains(centre))) continue;
                }
                seen[ny * World.MapTilesX + nx] = true;
                queue.Enqueue((nx, ny));
            }
        }
        tileCount = count;
        return rooms;
    }
}
