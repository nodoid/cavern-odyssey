using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace SuperSqij;

/// <summary>
/// Builds a continuous, fully connected 256-room cave. Doors only ever seal a leaf room that
/// holds one Enertree fragment, so the twelve Dispensa-Monstas (each yielding two coloured
/// passes) are always enough to open all twenty-four doors. Generation retries until that
/// structure exists, and the difficulty setting governs hazards and creature density.
/// </summary>
public static class LevelGenerator
{
    private sealed class Edge
    {
        public int AX, AY, BX, BY;
        public bool Vertical;
        public bool Tree;
        public int WallTileX, CorridorY; // horizontal edges
        public int WallTileY, ShaftX;     // vertical edges
    }

    private sealed class UnionFind
    {
        private readonly int[] _p;
        public UnionFind(int n) { _p = Enumerable.Range(0, n).ToArray(); }
        public int Find(int a) { while (_p[a] != a) { _p[a] = _p[_p[a]]; a = _p[a]; } return a; }
        public bool Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return false;
            _p[b] = a;
            return true;
        }
    }

    public static World Generate(int seed, Difficulty difficulty = Difficulty.Medium)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int s = seed + attempt * 7919;
            var w = new World(s) { Difficulty = difficulty };
            if (TryGenerate(w, s, difficulty, out var vaults) && vaults.Count == World.DoorTotal)
            {
                PlaceItems(w, s, vaults);
                bool complete = w.Pickups.Count(p => p.Kind == PickupKind.Fragment) == World.FragmentTotal &&
                                w.Pickups.Count(p => p.Kind == PickupKind.Fruit) == World.FruitTotal &&
                                w.Monstas.Count == World.MonstaTotal &&
                                w.Doors.Count == World.DoorTotal;
                if (complete) return w;
            }
        }
        throw new InvalidOperationException("Could not generate a solvable level.");
    }

    private static bool TryGenerate(World w, int seed, Difficulty difficulty, out List<(int x, int y)> vaults)
    {
        var rng = new Random(seed);
        vaults = new List<(int, int)>();
        Fill(w, TileKind.Rock);

        CarveRooms(w, rng);
        var edges = BuildEdges(w, rng);
        CarveEdges(w, edges);
        CarveLedges(w, rng);
        AddHazards(w, rng, difficulty);
        AddProps(w, rng);

        // Find leaf rooms reachable through a single horizontal edge: these make clean vaults.
        var degree = new int[World.RoomCount];
        foreach (var e in edges)
        {
            degree[e.AY * World.RoomsX + e.AX]++;
            degree[e.BY * World.RoomsX + e.BX]++;
        }
        var candidates = new List<Edge>();
        foreach (var e in edges)
        {
            if (!e.Vertical && e.Tree)
            {
                int a = e.AY * World.RoomsX + e.AX;
                int b = e.BY * World.RoomsX + e.BX;
                if (degree[a] == 1 && !(e.AX == w.StartRoomX && e.AY == w.StartRoomY))
                    candidates.Add(ReverseIfNeeded(e, a));
                else if (degree[b] == 1 && !(e.BX == w.StartRoomX && e.BY == w.StartRoomY))
                    candidates.Add(ReverseIfNeeded(e, b));
            }
        }
        if (candidates.Count < World.DoorTotal) return false;
        Shuffle(candidates, rng);

        // Choose vault rooms well spread out.
        var chosen = new List<Edge>();
        foreach (var e in candidates)
        {
            int vx = e.BX, vy = e.BY;
            bool tooClose = chosen.Any(c =>
                Math.Abs(c.BX - vx) + Math.Abs(c.BY - vy) < 2);
            if (tooClose) continue;
            chosen.Add(e);
            vaults.Add((vx, vy));
            if (vaults.Count == World.DoorTotal) break;
        }
        if (vaults.Count < World.DoorTotal) return false;

        // Doors sit exactly in the 2-tile wall opening.
        int doorIndex = 0;
        foreach (var e in chosen)
        {
            var door = new Door
            {
                Bounds = new RectF(e.WallTileX * World.TileSize, e.CorridorY * World.TileSize,
                    2 * World.TileSize, 3 * World.TileSize),
                ColorIndex = doorIndex % 6,
                Rune = doorIndex % 6,
                Phase = (float)rng.NextDouble() * 6f,
            };
            w.Doors.Add(door);
            w.Rooms[e.BY * World.RoomsX + e.BX].Vault = true;
            doorIndex++;
        }

        PlaceAltarAndStart(w, rng);
        PlaceRoomsLabels(w);
        return true;
    }

    private static Edge ReverseIfNeeded(Edge e, int leafIndex)
    {
        // Ensure B is the leaf; the door uses the wall on B's side.
        int b = e.BY * World.RoomsX + e.BX;
        if (b == leafIndex) return e;
        (e.AX, e.BX) = (e.BX, e.AX);
        (e.AY, e.BY) = (e.BY, e.AY);
        e.WallTileX = Math.Max(e.AX, e.BX) * World.RoomTilesX - 2;
        return e;
    }

    private static void Fill(World w, TileKind kind)
    {
        Array.Fill(w.Tiles, kind);
    }

    private static void CarveRooms(World w, Random rng)
    {
        for (int ry = 0; ry < World.RoomsY; ry++)
        for (int rx = 0; rx < World.RoomsX; rx++)
        {
            int bx = rx * World.RoomTilesX;
            int by = ry * World.RoomTilesY;
            for (int y = 2; y < World.RoomTilesY - 2; y++)
            for (int x = 2; x < World.RoomTilesX - 2; x++)
            {
                // ragged cave edges
                int edgeNoise = Hash(rx, ry, x, y) % 100;
                bool nearEdge = x < 4 || x > World.RoomTilesX - 5 || y < 4 || y > World.RoomTilesY - 5;
                if (nearEdge && edgeNoise < 13) continue;
                w.SetTile(bx + x, by + y, TileKind.Empty);
            }
            // occasional interior pillar for shape
            if (rng.NextDouble() < 0.5)
            {
                int px = bx + 8 + rng.Next(16);
                int py = by + 6 + rng.Next(7);
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    w.SetTile(px + x, py + y, TileKind.Rock);
            }
        }
    }

    private static List<Edge> BuildEdges(World w, Random rng)
    {
        var edges = new List<Edge>();
        var uf = new UnionFind(World.RoomCount);
        for (int ry = 0; ry < World.RoomsY; ry++)
        for (int rx = 0; rx < World.RoomsX; rx++)
        {
            if (rx + 1 < World.RoomsX)
                edges.Add(new Edge
                {
                    AX = rx, AY = ry, BX = rx + 1, BY = ry, Vertical = false,
                    WallTileX = (rx + 1) * World.RoomTilesX - 2,
                    CorridorY = ry * World.RoomTilesY + 11 + rng.Next(2),
                });
            if (ry + 1 < World.RoomsY)
                edges.Add(new Edge
                {
                    AX = rx, AY = ry, BX = rx, BY = ry + 1, Vertical = true,
                    WallTileY = (ry + 1) * World.RoomTilesY - 2,
                    ShaftX = rx * World.RoomTilesX + 8 + rng.Next(16),
                });
        }
        Shuffle(edges, rng);

        var result = new List<Edge>();
        var extra = new List<Edge>();
        foreach (var e in edges)
        {
            int a = e.AY * World.RoomsX + e.AX;
            int b = e.BY * World.RoomsX + e.BX;
            bool joins = uf.Union(a, b);
            if (joins)
            {
                e.Tree = true;
                result.Add(e);
            }
            else
            {
                extra.Add(e);
            }
        }
        // A handful of loops makes the cave less tree-like while keeping vaults valid.
        foreach (var e in extra)
            if (rng.NextDouble() < 0.16) result.Add(e);
        return result;
    }

    private static void CarveEdges(World w, List<Edge> edges)
    {
        foreach (var e in edges)
        {
            if (!e.Vertical)
            {
                int y0 = e.CorridorY;
                for (int x = e.WallTileX - 1; x <= e.WallTileX + 4; x++)
                for (int y = y0; y < y0 + 3; y++)
                    w.SetTile(x, y, TileKind.Empty);
                // ensure a floor under the corridor
                for (int x = e.WallTileX - 1; x <= e.WallTileX + 4; x++)
                    w.SetTile(x, y0 + 3, TileKind.Rock);
                for (int x = e.WallTileX - 1; x <= e.WallTileX + 4; x++)
                    w.SetTile(x, y0 - 1, TileKind.Rock);
            }
            else
            {
                int x0 = e.ShaftX;
                int yStart = e.WallTileY - 2;
                int yEnd = e.WallTileY + 5;
                for (int y = yStart; y <= yEnd; y++)
                for (int x = x0; x < x0 + 5; x++)
                    w.SetTile(x, y, TileKind.Empty);
                // staggered hand-hold ledges make the shaft climbable
                bool left = true;
                for (int y = yStart + 2; y < yEnd - 1; y += 4)
                {
                    if (left)
                        for (int x = x0; x < x0 + 2; x++)
                        for (int dy = 0; dy < 1; dy++)
                            w.SetTile(x, y + dy, TileKind.Ledge);
                    else
                        for (int x = x0 + 3; x < x0 + 5; x++)
                            w.SetTile(x, y, TileKind.Ledge);
                    left = !left;
                }
            }
        }
    }

    private static void CarveLedges(World w, Random rng)
    {
        for (int ry = 0; ry < World.RoomsY; ry++)
        for (int rx = 0; rx < World.RoomsX; rx++)
        {
            int bx = rx * World.RoomTilesX;
            int by = ry * World.RoomTilesY;
            int count = 3 + rng.Next(2);
            for (int i = 0; i < count; i++)
            {
                int len = 5 + rng.Next(7);
                int x0 = bx + 4 + rng.Next(World.RoomTilesX - 10 - len);
                int y = by + 5 + rng.Next(World.RoomTilesY - 9);
                for (int x = 0; x < len; x++)
                {
                    if (w.TileAt(x0 + x, y) == TileKind.Empty &&
                        w.TileAt(x0 + x, y - 1) == TileKind.Empty)
                        w.SetTile(x0 + x, y, TileKind.Ledge);
                }
                // little supporting stalactites
                if (rng.NextDouble() < 0.5)
                {
                    int sx = x0 + rng.Next(len);
                    for (int dy = 1; dy < 2 + rng.Next(3); dy++)
                        if (w.TileAt(sx, y - dy) == TileKind.Empty)
                            w.SetTile(sx, y - dy, TileKind.Rock);
                }
            }
        }
    }

    private static void AddHazards(World w, Random rng, Difficulty difficulty)
    {
        float lavaChance = difficulty.LavaChance();
        float spikeChance = difficulty.SpikeChance();
        for (int ry = 0; ry < World.RoomsY; ry++)
        for (int rx = 0; rx < World.RoomsX; rx++)
        {
            if (rx == w.StartRoomX && ry == w.StartRoomY) continue;
            int bx = rx * World.RoomTilesX;
            int by = ry * World.RoomTilesY;
            if (rng.NextDouble() < lavaChance)
            {
                int width = 4 + rng.Next(4);
                int x0 = bx + 5 + rng.Next(World.RoomTilesX - 10 - width);
                int y = by + World.RoomTilesY - 3;
                for (int x = 0; x < width; x++)
                    if (!w.IsSolid(x0 + x, y))
                        w.SetTile(x0 + x, y, TileKind.Lava);
            }
            if (rng.NextDouble() < spikeChance)
            {
                int x0 = bx + 6 + rng.Next(14);
                int y = by + 8 + rng.Next(5);
                for (int x = 0; x < 4; x++)
                    if (!w.IsSolid(x0 + x, y) && w.IsSolid(x0 + x, y + 1))
                        w.SetTile(x0 + x, y, TileKind.Spikes);
            }
        }
    }

    private static void AddProps(World w, Random rng)
    {
        for (int ry = 0; ry < World.RoomsY; ry++)
        for (int rx = 0; rx < World.RoomsX; rx++)
        {
            int bx = rx * World.RoomTilesX;
            int by = ry * World.RoomTilesY;
            // torches on walls
            int placed = 0;
            for (int attempt = 0; attempt < 20 && placed < 2; attempt++)
            {
                int tx = bx + 3 + rng.Next(World.RoomTilesX - 6);
                int ty = by + 5 + rng.Next(8);
                if (w.TileAt(tx, ty) != TileKind.Empty || w.TileAt(tx, ty - 1) != TileKind.Empty) continue;
                if (!w.IsSolid(tx - 1, ty) && !w.IsSolid(tx + 1, ty)) continue;
                w.Props.Add(new Prop
                {
                    Pos = new Vector2(tx * World.TileSize, ty * World.TileSize - 8),
                    Kind = PropKind.Torch,
                    Phase = (float)rng.NextDouble() * 3f,
                });
                placed++;
            }
            // glowing crystal clusters
            if (rng.NextDouble() < 0.75)
            {
                int tx = bx + 4 + rng.Next(World.RoomTilesX - 9);
                int ty = by + 5 + rng.Next(World.RoomTilesY - 9);
                if (w.TileAt(tx, ty) == TileKind.Empty)
                {
                    w.Props.Add(new Prop
                    {
                        Pos = new Vector2(tx * World.TileSize, ty * World.TileSize),
                        Kind = PropKind.Crystal,
                        Variant = rng.Next(2),
                        Phase = (float)rng.NextDouble() * 6f,
                    });
                }
            }
            if (rng.NextDouble() < 0.5)
            {
                var spot = w.FindFloorSpot(rx, ry, rng);
                if (spot.HasValue)
                {
                    w.Props.Add(new Prop
                    {
                        Pos = spot.Value + new Vector2(6, 8),
                        Kind = PropKind.Mushroom,
                        Variant = rng.Next(3),
                        Phase = (float)rng.NextDouble() * 6f,
                    });
                }
            }
        }
    }

    private static void PlaceAltarAndStart(World w, Random rng)
    {
        int bx = w.StartRoomX * World.RoomTilesX;
        int by = w.StartRoomY * World.RoomTilesY;
        int floorY = -1;
        int centerX = bx + World.RoomTilesX / 2;
        for (int y = by + World.RoomTilesY - 3; y > by + 3; y--)
        {
            if (w.IsSolid(centerX, y) && !w.IsSolid(centerX, y - 1) && !w.IsSolid(centerX, y - 2) &&
                !w.IsSolid(centerX + 1, y - 1))
            {
                floorY = y;
                break;
            }
        }
        if (floorY < 0)
        {
            var spot = w.FindFloorSpot(w.StartRoomX, w.StartRoomY, rng);
            floorY = (int)((spot?.Y ?? by * 16 + 200) / 16) + 1;
        }
        float altarX = centerX * World.TileSize - 48;
        float ground = floorY * World.TileSize;
        w.Altar = new Altar { Bounds = new RectF(altarX, ground - 64, 96, 64) };
        w.Rooms[w.StartRoomY * World.RoomsX + w.StartRoomX].Name = "THE GREAT PLATFORM";
        var start = new Vector2(altarX - 64, ground - Player.Height);
        if (w.HitsSolid(RectF.FromCenter(start + new Vector2(10, 10), 18, 18)))
            start = new Vector2(altarX + 132, ground - Player.Height);
        w.StartPosition = start;
    }

    private static void PlaceItems(World w, int seed, List<(int x, int y)> vaults)
    {
        var rng = new Random(seed ^ 0x51ee7);
        var usedRooms = new HashSet<int>();
        var positions = new List<Vector2>();

        // One fragment behind each door.
        for (int i = 0; i < vaults.Count; i++)
        {
            var (vx, vy) = vaults[i];
            var spot = w.FindFloorSpot(vx, vy, rng);
            if (!spot.HasValue) continue;
            var pos = spot.Value + new Vector2(8, 8);
            w.Pickups.Add(new Pickup { Pos = pos, Kind = PickupKind.Fragment, Phase = i * 0.7f });
            positions.Add(pos);
            w.Rooms[vy * World.RoomsX + vx].FragmentIndex = i;
        }

        // 12 Dispensa-Monstas, spread across non-vault rooms.
        var roomCandidates = new List<(int x, int y)>();
        for (int y = 0; y < World.RoomsY; y++)
        for (int x = 0; x < World.RoomsX; x++)
        {
            int idx = y * World.RoomsX + x;
            if (w.Rooms[idx].Vault) continue;
            if (x == w.StartRoomX && y == w.StartRoomY) continue;
            roomCandidates.Add((x, y));
        }
        Shuffle(roomCandidates, rng);
        var monstaRooms = new List<(int x, int y)>();
        foreach (var rc in roomCandidates)
        {
            if (monstaRooms.Any(m => Math.Abs(m.x - rc.x) + Math.Abs(m.y - rc.y) < 2)) continue;
            var spot = w.FindFloorSpot(rc.x, rc.y, rng);
            if (!spot.HasValue) continue;
            var pos = spot.Value + new Vector2(-8, -12); // sit the 32px creature on the floor
            int i = monstaRooms.Count;
            w.Monstas.Add(new Monsta
            {
                Pos = pos,
                ColorA = i % 6,
                ColorB = (i + 3) % 6,
            });
            positions.Add(pos);
            usedRooms.Add(rc.y * World.RoomsX + rc.x);
            monstaRooms.Add(rc);
            if (monstaRooms.Count == World.MonstaTotal) break;
        }

        // Doodlefoodle fruits (three feeds per monsta).
        int fruitPlaced = 0;
        int guard = 0;
        while (fruitPlaced < World.FruitTotal && guard++ < 1600)
        {
            var rc = roomCandidates[rng.Next(roomCandidates.Count)];
            int idx = rc.y * World.RoomsX + rc.x;
            if (w.Rooms[idx].Vault) continue;
            var spot = w.FindFloorSpot(rc.x, rc.y, rng);
            if (!spot.HasValue) continue;
            var pos = spot.Value + new Vector2(8, 8);
            if (positions.Any(p => Vector2.DistanceSquared(p, pos) < 60 * 60)) continue;
            w.Pickups.Add(new Pickup { Pos = pos, Kind = PickupKind.Fruit, Phase = fruitPlaced * 0.9f });
            positions.Add(pos);
            fruitPlaced++;
        }

        // Emergency energy pods.
        int pods = w.Difficulty.EnergyPods();
        for (int i = 0; i < pods; i++)
        {
            var rc = roomCandidates[rng.Next(roomCandidates.Count)];
            var spot = w.FindFloorSpot(rc.x, rc.y, rng);
            if (!spot.HasValue) continue;
            w.Pickups.Add(new Pickup
            {
                Pos = spot.Value + new Vector2(8, 8),
                Kind = PickupKind.Energy,
                Phase = i,
            });
        }

        // Nasty creatures to shoot or avoid.
        int enemyCount = 0;
        float enemyChance = w.Difficulty.EnemyChance();
        int enemyCap = w.Difficulty.MaxEnemies();
        for (int y = 0; y < World.RoomsY; y++)
        for (int x = 0; x < World.RoomsX; x++)
        {
            if (x == w.StartRoomX && y == w.StartRoomY) continue;
            if (rng.NextDouble() > enemyChance) continue;
            int howMany = rng.Next(1, w.Difficulty == Difficulty.Hard ? 4 : 3);
            for (int i = 0; i < howMany && enemyCount < enemyCap; i++)
            {
                var kind = (EnemyKind)rng.Next(4);
                Vector2? spot = kind is EnemyKind.Bat or EnemyKind.Floater
                    ? w.FindAirSpot(x, y, rng)
                    : w.FindFloorSpot(x, y, rng);
                if (!spot.HasValue) continue;
                var pos = spot.Value;
                if (Vector2.DistanceSquared(pos, w.StartPosition) < 220 * 220) continue;
                w.Enemies.Add(new Enemy
                {
                    Pos = pos,
                    Kind = kind,
                    Home = pos,
                    HP = kind == EnemyKind.Crawler ? 2 : 1,
                    FireTimer = 1f + (float)rng.NextDouble(),
                    FacingRight = rng.NextDouble() < 0.5,
                });
                enemyCount++;
            }
        }
    }

    private static void PlaceRoomsLabels(World w)
    {
        string[] adj =
        {
            "SUNKEN", "HOLLOW", "CRIMSON", "GLIMMERING", "SILENT", "BROKEN", "ANCIENT", "WHISPERING",
            "FROZEN", "VERDANT", "SHADOWED", "TIDAL", "MOLTEN", "FORGOTTEN", "LUMINOUS", "WEARY",
        };
        string[] noun =
        {
            "GALLERY", "CHASM", "VAULT", "CAVERN", "HOLLOWS", "SPIRES", "TUNNELS", "CHAMBER",
            "ABYSS", "RIFT", "CRYPT", "CISTERN", "PASSAGE", "DELVE", "SANCTUM", "REACH",
        };
        for (int y = 0; y < World.RoomsY; y++)
        for (int x = 0; x < World.RoomsX; x++)
        {
            var info = w.Rooms[y * World.RoomsX + x];
            if (!string.IsNullOrEmpty(info.Name)) continue;
            int a = (x * 7 + y * 3 + w.Seed) & 15;
            int n = (x * 5 + y * 11 + w.Seed * 3) & 15;
            info.Name = $"{adj[a]} {noun[n]}";
        }
    }

    private static int Hash(int a, int b, int c, int d)
    {
        unchecked
        {
            int h = a * 73856093 ^ b * 19349663 ^ c * 83492791 ^ (int)(d * 2654435761L);
            h ^= h >> 13;
            return h & 0x7fffffff;
        }
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
