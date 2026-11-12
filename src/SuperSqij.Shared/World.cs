using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace SuperSqij;

public enum Difficulty { Easy, Medium, Hard }

/// <summary>Tuning knobs for the three difficulty levels.</summary>
public static class DifficultyInfo
{
    public static string Name(this Difficulty d) => d switch
    {
        Difficulty.Easy => "EASY",
        Difficulty.Hard => "HARD",
        _ => "MEDIUM",
    };

    /// <summary>Multiplier applied to all damage the player takes.</summary>
    public static float DamageScale(this Difficulty d) => d switch
    {
        Difficulty.Easy => 0.65f,
        Difficulty.Hard => 1.35f,
        _ => 1f,
    };

    /// <summary>Chance that a non-start room gets a creature.</summary>
    public static float EnemyChance(this Difficulty d) => d switch
    {
        Difficulty.Easy => 0.28f,
        Difficulty.Hard => 0.62f,
        _ => 0.44f,
    };

    public static int MaxEnemies(this Difficulty d) => d switch
    {
        Difficulty.Easy => 48,
        Difficulty.Hard => 120,
        _ => 78,
    };

    public static float LavaChance(this Difficulty d) => d switch
    {
        Difficulty.Easy => 0.18f,
        Difficulty.Hard => 0.50f,
        _ => 0.34f,
    };

    public static float SpikeChance(this Difficulty d) => d switch
    {
        Difficulty.Easy => 0.14f,
        Difficulty.Hard => 0.42f,
        _ => 0.28f,
    };

    public static int EnergyPods(this Difficulty d) => d switch
    {
        Difficulty.Easy => 16,
        Difficulty.Hard => 6,
        _ => 10,
    };
}

public enum TileKind : byte
{
    Empty = 0,
    Rock = 1,
    Ledge = 2,
    Lava = 3,
    Spikes = 4,
}

public struct RectF
{
    public float X, Y, W, H;
    public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
    public float Left => X;
    public float Right => X + W;
    public float Top => Y;
    public float Bottom => Y + H;
    public Vector2 Center => new(X + W / 2, Y + H / 2);
    public bool Intersects(RectF o) => X < o.Right && Right > o.X && Y < o.Bottom && Bottom > o.Y;
    public bool Contains(Vector2 p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
    public RectF Inflate(float d) => new(X - d, Y - d, W + d * 2, H + d * 2);
    public static RectF FromCenter(Vector2 c, float w, float h) => new(c.X - w / 2, c.Y - h / 2, w, h);
}

public sealed class RoomInfo
{
    public string Name = "";
    public bool Visited;
    public bool Vault;
    public int FragmentIndex = -1;
}

public sealed class World
{
    public const int TileSize = 16;
    public const int RoomTilesX = 32;
    public const int RoomTilesY = 18;
    public const int RoomsX = 16;
    public const int RoomsY = 16;
    public const int RoomCount = RoomsX * RoomsY;
    public const int MapTilesX = RoomsX * RoomTilesX;
    public const int MapTilesY = RoomsY * RoomTilesY;
    public const int MapWidth = MapTilesX * TileSize;
    public const int MapHeight = MapTilesY * TileSize;

    public readonly int Seed;
    public readonly TileKind[] Tiles = new TileKind[MapTilesX * MapTilesY];
    public readonly RoomInfo[] Rooms = new RoomInfo[RoomCount];
    public readonly List<Enemy> Enemies = new();
    public readonly List<Projectile> Projectiles = new();
    public readonly List<Pickup> Pickups = new();
    public readonly List<Monsta> Monstas = new();
    public readonly List<Door> Doors = new();
    public readonly List<Prop> Props = new();
    public readonly List<Particle> Particles = new();

    public Altar Altar = null!;
    public Vector2 StartPosition;
    public int StartRoomX = 7, StartRoomY = 7;
    public float Time;
    public Difficulty Difficulty = Difficulty.Medium;
    public float DamageScale => Difficulty.DamageScale();

    // Progress: 24 sealed vaults across 256 rooms, fed by 12 Dispensa-Monstas.
    public const int FragmentTotal = 24;
    public const int FruitTotal = 36;
    public const int MonstaTotal = 12;
    public const int DoorTotal = 24;
    public int FragmentsCollected;
    public int FruitCount;
    public int DoorsOpened;
    public readonly int[] Passes = new int[6];
    public bool TreeComplete => FragmentsCollected >= FragmentTotal;

    public World(int seed)
    {
        Seed = seed;
        for (int i = 0; i < Rooms.Length; i++) Rooms[i] = new RoomInfo();
    }

    public ref TileKind TileRef(int tx, int ty) => ref Tiles[ty * MapTilesX + tx];

    public TileKind TileAt(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= MapTilesX || ty >= MapTilesY) return TileKind.Rock;
        return Tiles[ty * MapTilesX + tx];
    }

    public void SetTile(int tx, int ty, TileKind kind)
    {
        if (tx < 0 || ty < 0 || tx >= MapTilesX || ty >= MapTilesY) return;
        Tiles[ty * MapTilesX + tx] = kind;
    }

    public bool IsSolid(int tx, int ty) => TileAt(tx, ty) is TileKind.Rock or TileKind.Ledge;

    public bool IsSolidPx(Vector2 p) => IsSolid((int)MathF.Floor(p.X / TileSize), (int)MathF.Floor(p.Y / TileSize));

    public bool HitsSolid(RectF box)
    {
        int x0 = (int)MathF.Floor(box.Left / TileSize);
        int x1 = (int)MathF.Floor((box.Right - 0.001f) / TileSize);
        int y0 = (int)MathF.Floor(box.Top / TileSize);
        int y1 = (int)MathF.Floor((box.Bottom - 0.001f) / TileSize);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (IsSolid(x, y)) return true;
        return false;
    }

    public bool HitsHazard(RectF box, out bool lava)
    {
        lava = false;
        int x0 = (int)MathF.Floor(box.Left / TileSize);
        int x1 = (int)MathF.Floor((box.Right - 0.001f) / TileSize);
        int y0 = (int)MathF.Floor(box.Top / TileSize);
        int y1 = (int)MathF.Floor((box.Bottom - 0.001f) / TileSize);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var t = TileAt(x, y);
            if (t == TileKind.Lava) { lava = true; return true; }
            if (t == TileKind.Spikes) return true;
        }
        return false;
    }

    public bool LineOfSight(Vector2 a, Vector2 b)
    {
        float dist = Vector2.Distance(a, b);
        int steps = (int)(dist / 8f);
        for (int i = 1; i < steps; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)steps);
            if (IsSolidPx(p)) return false;
        }
        return true;
    }

    public (int x, int y) RoomAt(Vector2 world)
    {
        int rx = Math.Clamp((int)(world.X / (RoomTilesX * TileSize)), 0, RoomsX - 1);
        int ry = Math.Clamp((int)(world.Y / (RoomTilesY * TileSize)), 0, RoomsY - 1);
        return (rx, ry);
    }

    public RoomInfo RoomInfoAt(Vector2 world)
    {
        var (x, y) = RoomAt(world);
        return Rooms[y * RoomsX + x];
    }

    public void VisitRoom(Vector2 world)
    {
        var (x, y) = RoomAt(world);
        Rooms[y * RoomsX + x].Visited = true;
    }

    public Vector2? FindFloorSpot(int roomX, int roomY, Random rng, int clearance = 2)
    {
        int bx = roomX * RoomTilesX;
        int by = roomY * RoomTilesY;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            int tx = bx + 3 + rng.Next(RoomTilesX - 6);
            int ty = by + 4 + rng.Next(RoomTilesY - 8);
            if (!IsOpenColumn(tx, ty - 1, clearance + 1)) continue;
            if (!IsSolid(tx, ty + 1) && !IsSolid(tx + 1, ty + 1)) continue;
            bool blocked = false;
            foreach (var d in Doors)
                if (MathF.Abs(d.Bounds.Center.X - (tx * TileSize + 8)) < 48) blocked = true;
            if (blocked) continue;
            if (IsClearOfProps(tx, ty)) return new Vector2(tx * TileSize + 2, (ty + 1) * TileSize - 20);
        }
        // centre fallback
        for (int dy = 2; dy < RoomTilesY - 2; dy++)
        for (int dx = 2; dx < RoomTilesX - 2; dx++)
        {
            int tx = bx + dx, ty = by + dy;
            if (IsOpenColumn(tx, ty - 1, 3) && (IsSolid(tx, ty + 1) || IsSolid(tx + 1, ty + 1)))
                return new Vector2(tx * TileSize + 2, (ty + 1) * TileSize - 20);
        }
        return null;
    }

    private bool IsOpenColumn(int tx, int ty, int rows)
    {
        for (int i = 0; i < rows; i++)
            if (IsSolid(tx, ty - i) || IsSolid(tx + 1, ty - i)) return false;
        return true;
    }

    private bool IsClearOfProps(int tx, int ty)
    {
        var p = new Vector2(tx * TileSize + 8, ty * TileSize + 8);
        foreach (var prop in Props)
            if (Vector2.DistanceSquared(prop.Pos, p) < 32 * 32) return false;
        if (Altar != null && Vector2.DistanceSquared(Altar.Bounds.Center, p) < 90 * 90) return false;
        return true;
    }

    public Vector2? FindAirSpot(int roomX, int roomY, Random rng)
    {
        int bx = roomX * RoomTilesX;
        int by = roomY * RoomTilesY;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int tx = bx + 4 + rng.Next(RoomTilesX - 8);
            int ty = by + 4 + rng.Next(RoomTilesY - 8);
            if (!IsSolid(tx, ty) && !IsSolid(tx, ty - 1)) return new Vector2(tx * TileSize + 8, ty * TileSize + 8);
        }
        return null;
    }

    public void SpawnParticles(Vector2 at, Color color, int count, float speed = 80f, float life = 0.5f)
    {
        var rng = new Random(Seed ^ (int)(at.X * 7) ^ (int)(at.Y * 13) ^ (int)(Time * 1000));
        for (int i = 0; i < count; i++)
        {
            float a = (float)(rng.NextDouble() * Math.PI * 2);
            float s = (float)(rng.NextDouble() * speed) + speed * 0.2f;
            Particles.Add(new Particle
            {
                Pos = at,
                Vel = new Vector2(MathF.Cos(a) * s, MathF.Sin(a) * s),
                Life = life * (0.5f + (float)rng.NextDouble()),
                MaxLife = life,
                Color = color,
                Size = 1 + rng.Next(0, 2),
                Gravity = 60f,
            });
        }
    }

    public void UpdateEffectParticles(float dt)
    {
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            var p = Particles[i];
            p.Life -= dt;
            if (p.Life <= 0) { Particles.RemoveAt(i); continue; }
            p.Vel.Y += p.Gravity * dt;
            p.Pos += p.Vel * dt;
            Particles[i] = p;
        }
    }
}
