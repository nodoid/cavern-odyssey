using System;
using Microsoft.Xna.Framework;

namespace SuperSqij;

public enum PickupKind { Fruit, Fragment, Pass, Energy }

public enum EnemyKind { Crawler, Floater, Turret, Bat }

public enum PropKind { Crystal, Torch, Mushroom }

public struct Particle
{
    public Vector2 Pos;
    public Vector2 Vel;
    public float Life;
    public float MaxLife;
    public Color Color;
    public int Size;
    public float Gravity;
}

public sealed class Pickup
{
    public Vector2 Pos;
    public PickupKind Kind;
    public int ColorIndex;
    public float Phase;
    public bool Taken;
    public RectF Bounds => new(Pos.X - 6, Pos.Y - 6, 12, 12);
}

public sealed class Monsta
{
    public Vector2 Pos;           // top-left of 32x32
    public int ColorA;
    public int ColorB;
    public int Fed;
    public bool Produced;
    public float Anim;
    public RectF Bounds => new(Pos.X, Pos.Y, 32, 32);
}

public sealed class Door
{
    public RectF Bounds;
    public int ColorIndex;
    public int Rune;
    public bool Open;
    public float OpenAmount;
    public float Phase;
}

public sealed class Altar
{
    public RectF Bounds;          // 96x64
    public float Glow;
}

public sealed class Prop
{
    public Vector2 Pos;
    public PropKind Kind;
    public int Variant;
    public float Phase;
}

public sealed class Projectile
{
    public Vector2 Pos;
    public Vector2 Vel;
    public bool FromPlayer;
    public float Life = 1.6f;
    public Color Color;
    public RectF Bounds => new(Pos.X - 3, Pos.Y - 3, 6, 6);
}

public sealed class Enemy
{
    public Vector2 Pos;           // top-left, 22x20 body
    public Vector2 Vel;
    public EnemyKind Kind;
    public int HP = 1;
    public float Anim;
    public bool FacingRight = true;
    public bool Alive = true;
    public float FireTimer;
    public Vector2 Home;
    public RectF Bounds => new(Pos.X + 1, Pos.Y + 2, 20, 18);

    public void Update(World world, Vector2 playerCenter, float playerEnergy, float dt, Action<Projectile> spawn)
    {
        Anim += dt;
        const float S = World.TileSize;
        switch (Kind)
        {
            case EnemyKind.Crawler:
            {
                float speed = 34f * (FacingRight ? 1 : -1);
                Vel.X = speed;
                Vel.Y = MathF.Min(Vel.Y + 640f * dt, 420f);
                Move(world, dt);
                // turn around at walls or platform edges
                var ahead = new Vector2(Pos.X + (FacingRight ? 24 : -4), Pos.Y + 16);
                var belowAhead = new Vector2(ahead.X, Pos.Y + 24);
                if (world.HitsSolid(RectF.FromCenter(ahead, 4, 4)) || !world.IsSolidPx(belowAhead))
                    FacingRight = !FacingRight;
                break;
            }
            case EnemyKind.Floater:
            {
                var drift = new Vector2(MathF.Sin(Anim * 0.7f + Home.X * 0.01f) * 22f, MathF.Cos(Anim * 0.9f) * 12f);
                if (Vector2.DistanceSquared(playerCenter, Pos) < 240 * 240)
                    drift += Vector2.Normalize(playerCenter - Pos) * 34f;
                Vel = drift;
                MoveFlyer(world, dt);
                break;
            }
            case EnemyKind.Bat:
            {
                if (Vector2.DistanceSquared(playerCenter, Pos) < 260 * 260)
                {
                    var dir = playerCenter - Pos;
                    if (dir.LengthSquared() > 1f) dir.Normalize();
                    Vel = Vector2.Lerp(Vel, dir * 78f, dt * 3f);
                    FacingRight = dir.X >= 0;
                }
                else
                {
                    Vel = new Vector2(MathF.Sin(Anim * 1.1f) * 40f, MathF.Cos(Anim * 1.6f) * 26f);
                }
                MoveFlyer(world, dt);
                break;
            }
            case EnemyKind.Turret:
            {
                FireTimer -= dt;
                if (FireTimer <= 0 && Vector2.DistanceSquared(playerCenter, Pos) < 300 * 300 &&
                    world.LineOfSight(Pos + new Vector2(11, 8), playerCenter))
                {
                    FireTimer = 2.1f;
                    var dir = playerCenter - Pos;
                    if (dir.LengthSquared() > 1f) dir.Normalize();
                    FacingRight = dir.X >= 0;
                    spawn(new Projectile
                    {
                        Pos = Pos + new Vector2(11, 8),
                        Vel = dir * 150f,
                        FromPlayer = false,
                        Color = new Color(255, 190, 120),
                    });
                }
                break;
            }
        }
    }

    private void Move(World world, float dt)
    {
        const float S = World.TileSize;
        float dx = Vel.X * dt;
        Pos.X += dx;
        var b = Bounds;
        if (dx != 0)
        {
            int y0 = (int)(b.Top / S), y1 = (int)((b.Bottom - 1) / S);
            if (dx > 0)
            {
                int tx = (int)((b.Right - 1) / S);
                for (int ty = y0; ty <= y1; ty++)
                    if (world.IsSolid(tx, ty)) { Pos.X = tx * S - b.W - 1; Vel.X = 0; break; }
            }
            else
            {
                int tx = (int)(b.Left / S);
                for (int ty = y0; ty <= y1; ty++)
                    if (world.IsSolid(tx, ty)) { Pos.X = (tx + 1) * S - 1; Vel.X = 0; break; }
            }
        }
        float dy = Vel.Y * dt;
        Pos.Y += dy;
        b = Bounds;
        if (dy != 0)
        {
            int x0 = (int)(b.Left / S), x1 = (int)((b.Right - 1) / S);
            if (dy > 0)
            {
                int ty = (int)((b.Bottom - 1) / S);
                for (int tx = x0; tx <= x1; tx++)
                    if (world.IsSolid(tx, ty)) { Pos.Y = ty * S - b.H - 2; Vel.Y = 0; break; }
            }
            else
            {
                int ty = (int)(b.Top / S);
                for (int tx = x0; tx <= x1; tx++)
                    if (world.IsSolid(tx, ty)) { Pos.Y = (ty + 1) * S - 2; Vel.Y = 0; break; }
            }
        }
    }

    private void MoveFlyer(World world, float dt)
    {
        var next = Pos + Vel * dt;
        var box = new RectF(next.X + 1, next.Y + 2, 20, 18);
        if (world.HitsSolid(box))
        {
            // slide along whichever axis is free
            var xOnly = new RectF(Pos.X + Vel.X * dt + 1, Pos.Y + 2, 20, 18);
            var yOnly = new RectF(Pos.X + 1, Pos.Y + Vel.Y * dt + 2, 20, 18);
            if (!world.HitsSolid(xOnly)) next = new Vector2(next.X, Pos.Y);
            else if (!world.HitsSolid(yOnly)) next = new Vector2(Pos.X, next.Y);
            else next = Pos;
            Vel *= -0.35f;
        }
        next.X = Math.Clamp(next.X, 0, World.MapWidth - 24);
        next.Y = Math.Clamp(next.Y, 0, World.MapHeight - 24);
        Pos = next;
    }
}

public sealed class Player
{
    public Vector2 Pos;
    public Vector2 Vel;
    public bool OnGround;
    public bool FacingRight = true;
    public int FlapsLeft = 2;
    public float Energy = MaxEnergy;
    public float Invuln;
    public float ShootCooldown;
    public float AnimTime;
    public float Coyote;
    public float JumpBuffer;
    public float ShootFlash;
    public bool Dead;
    public bool Moving;

    public const float MaxEnergy = 100f;
    public const int Width = 20;
    public const int Height = 20;
    public RectF Bounds => new(Pos.X, Pos.Y, Width, Height);
    public Vector2 Center => new(Pos.X + Width / 2, Pos.Y + Height / 2);
    public Vector2 Head => new(Pos.X + Width / 2, Pos.Y + 4);

    private const float Gravity = 660f;
    private const float MaxFall = 430f;
    private const float RunSpeed = 118f;
    private const float Accel = 820f;
    private const float JumpVel = 218f;
    private const float FlapVel = 196f;

    public void Reset(Vector2 pos)
    {
        Pos = pos;
        Vel = Vector2.Zero;
        Energy = MaxEnergy;
        Invuln = 0.6f;
        FlapsLeft = 2;
        Dead = false;
    }

    public void Update(World world, Controls c, float dt, Action<Vector2> onFlap, Action<Vector2, bool> onShoot)
    {
        AnimTime += dt;
        Invuln = MathF.Max(0, Invuln - dt);
        ShootCooldown = MathF.Max(0, ShootCooldown - dt);
        ShootFlash = MathF.Max(0, ShootFlash - dt);

        float target = c.MoveX * RunSpeed;
        if (MathF.Abs(c.MoveX) > 0.05f)
        {
            Vel.X = MoveTowards(Vel.X, target, Accel * dt);
            FacingRight = c.MoveX > 0;
            Moving = true;
        }
        else
        {
            Vel.X = MoveTowards(Vel.X, 0, (OnGround ? 900f : 220f) * dt);
            Moving = false;
        }

        if (c.JumpPressed) JumpBuffer = 0.12f;
        JumpBuffer = MathF.Max(0, JumpBuffer - dt);
        Coyote = OnGround ? 0.1f : MathF.Max(0, Coyote - dt);

        if (JumpBuffer > 0 && (OnGround || Coyote > 0) && Vel.Y >= -1f)
        {
            Vel.Y = -JumpVel;
            JumpBuffer = 0;
            Coyote = 0;
            OnGround = false;
            FlapsLeft = 2;
            onFlap(Pos);
        }
        else if (c.JumpPressed && !OnGround && FlapsLeft > 0 && Vel.Y > -FlapVel * 0.8f)
        {
            Vel.Y = -FlapVel;
            FlapsLeft--;
            onFlap(Pos);
        }

        Vel.Y = MathF.Min(Vel.Y + Gravity * dt, MaxFall);

        MoveX(world, Vel.X * dt);
        ManageDoors(world);
        bool wasGround = OnGround;
        MoveY(world, Vel.Y * dt);
        if (!wasGround && OnGround) FlapsLeft = 2;

        if (c.FirePressed && ShootCooldown <= 0)
        {
            ShootCooldown = 0.26f;
            ShootFlash = 0.12f;
            onShoot(Head, FacingRight);
        }

        if (world.HitsHazard(Bounds, out bool lava))
            Damage((lava ? 30f : 18f) * world.DamageScale,
                lava ? new Vector2(0, -120) : new Vector2(0, -80));

        if (Energy <= 0) Dead = true;
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta) return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    private void MoveX(World world, float dx)
    {
        if (dx == 0) return;
        Pos.X += dx;
        var b = Bounds;
        int y0 = (int)(b.Top / World.TileSize);
        int y1 = (int)((b.Bottom - 0.001f) / World.TileSize);
        if (dx > 0)
        {
            int tx = (int)((b.Right - 0.001f) / World.TileSize);
            for (int ty = y0; ty <= y1; ty++)
                if (world.IsSolid(tx, ty)) { Pos.X = tx * World.TileSize - Width; Vel.X = 0; break; }
        }
        else
        {
            int tx = (int)(b.Left / World.TileSize);
            for (int ty = y0; ty <= y1; ty++)
                if (world.IsSolid(tx, ty)) { Pos.X = (tx + 1) * World.TileSize; Vel.X = 0; break; }
        }
        Pos.X = Math.Clamp(Pos.X, 0, World.MapWidth - Width);
    }

    private void MoveY(World world, float dy)
    {
        Pos.Y += dy;
        var b = Bounds;
        int x0 = (int)(b.Left / World.TileSize);
        int x1 = (int)((b.Right - 0.001f) / World.TileSize);
        OnGround = false;
        if (dy > 0)
        {
            int ty = (int)((b.Bottom - 0.001f) / World.TileSize);
            for (int tx = x0; tx <= x1; tx++)
                if (world.IsSolid(tx, ty))
                {
                    Pos.Y = ty * World.TileSize - Height;
                    Vel.Y = 0;
                    OnGround = true;
                    break;
                }
        }
        else if (dy < 0)
        {
            int ty = (int)(b.Top / World.TileSize);
            for (int tx = x0; tx <= x1; tx++)
                if (world.IsSolid(tx, ty)) { Pos.Y = (ty + 1) * World.TileSize; Vel.Y = 0; break; }
        }
        if (!OnGround)
        {
            // probe one pixel below
            var probe = new RectF(Pos.X + 2, Pos.Y + Height, Width - 4, 1.5f);
            if (world.HitsSolid(probe) && Vel.Y >= 0)
            {
                OnGround = true;
                Vel.Y = 0;
            }
        }
        Pos.Y = Math.Clamp(Pos.Y, 0, World.MapHeight - Height);
    }

    private void ManageDoors(World world)
    {
        var b = Bounds;
        foreach (var door in world.Doors)
        {
            if (door.Open) continue;
            if (!b.Intersects(door.Bounds)) continue;
            if (b.Center.X < door.Bounds.Center.X)
                Pos.X = door.Bounds.Left - Width;
            else
                Pos.X = door.Bounds.Right;
            Vel.X = 0;
            b = Bounds;
        }
    }

    public bool Damage(float amount, Vector2 knockback)
    {
        if (Invuln > 0 || Dead) return false;
        Energy = MathF.Max(0, Energy - amount);
        Invuln = 1.1f;
        Vel += knockback;
        if (Energy <= 0) Dead = true;
        return true;
    }
}
