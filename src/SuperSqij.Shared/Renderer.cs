using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperSqij;

public sealed class Renderer
{
    public const int ViewW = 640;
    public const int ViewH = 360;

    private readonly Art _art;
    public bool Scanlines = true;
    public bool Minimap = true;
    public float DamageFlash;
    public Vector2 Camera;
    public float Shake;

    public Renderer(Art art) => _art = art;

    private SpriteBatch _batch = null!;
    public void Bind(SpriteBatch batch) => _batch = batch;

    // ------------------------------------------------------------------ utils
    private void Rect(float x, float y, float w, float h, Color color)
    {
        _batch.Draw(_art.White, new Rectangle((int)MathF.Round(x), (int)MathF.Round(y),
            Math.Max(1, (int)MathF.Round(w)), Math.Max(1, (int)MathF.Round(h))), color);
    }

    private void GlowRect(float x, float y, float size, Color color)
    {
        _batch.Draw(_art.Glow, new Rectangle((int)x, (int)y, (int)size, (int)size), color);
    }

    private void Text(string text, float x, float y, Color color, float scale = 1f, bool shadow = true)
        => PixelFont.Draw(_batch, _art.Font, text, new Vector2(x, y), color, scale, 1f, shadow);

    private void TextCentered(string text, float cx, float y, Color color, float scale = 1f, bool shadow = true)
        => PixelFont.DrawCentered(_batch, _art.Font, text, cx, y, color, scale, 1f, shadow);

    // ------------------------------------------------------------ background
    public void DrawBackground(float time, float depthY)
    {
        _batch.Draw(_art.BgFar, new Rectangle(-(int)(Camera.X * 0.12f) % 1024, (int)depthY, 1024, 360),
            new Color(255, 255, 255) * 0.95f);
        _batch.Draw(_art.BgFar, new Rectangle(-(int)(Camera.X * 0.12f) % 1024 + 1024, (int)depthY, 1024, 360),
            new Color(255, 255, 255) * 0.95f);
        _batch.Draw(_art.BgFar, new Rectangle(-(int)(Camera.X * 0.12f) % 1024 - 1024, (int)depthY, 1024, 360),
            new Color(255, 255, 255) * 0.95f);

        int nearOffset = -(int)(Camera.X * 0.32f) % 1024;
        _batch.Draw(_art.BgNear, new Rectangle(nearOffset, (int)(depthY * 0.6f), 1024, 360),
            new Color(255, 255, 255) * 0.85f);
        _batch.Draw(_art.BgNear, new Rectangle(nearOffset + 1024, (int)(depthY * 0.6f), 1024, 360),
            new Color(255, 255, 255) * 0.85f);

        // cave wall behind the whole playfield
        int wallX = -((int)(Camera.X * 0.62f) % 512);
        int wallY = -((int)(Camera.Y * 0.72f) % 512);
        for (int y = -512; y < ViewH + 512; y += 512)
        for (int x = -512; x < ViewW + 512; x += 512)
            _batch.Draw(_art.BgWall, new Rectangle(wallX + x, wallY + y, 512, 512),
                new Color(255, 255, 255) * 0.92f);

        // faint ambient bloom near the middle of the cavern
        _batch.Draw(_art.Glow, new Rectangle(-120, -200, 880, 760), Palette.Teal * 0.12f);

        // slow drifting spore motes
        for (int i = 0; i < 26; i++)
        {
            float seed = i * 37.7f;
            float x = (seed * 13.3f + time * (8f + (i % 5) * 3f)) % 700 - 30;
            float y = (seed * 7.1f + MathF.Sin(time * 0.6f + i) * 14f) % 380;
            float a = 0.06f + 0.09f * (0.5f + 0.5f * MathF.Sin(time * 1.3f + i));
            Rect(x, y, 1 + (i % 2), 1 + (i % 2), Palette.Teal * a);
        }
    }

    // ------------------------------------------------------------------ world
    public void BeginWorld()
    {
        _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null,
            Matrix.CreateTranslation(-MathF.Round(Camera.X), -MathF.Round(Camera.Y), 0f));
    }

    public void EndBatch() => _batch.End();

    public void DrawTiles(World world, float time)
    {
        int tx0 = Math.Max(0, (int)(Camera.X / World.TileSize) - 1);
        int ty0 = Math.Max(0, (int)(Camera.Y / World.TileSize) - 1);
        int tx1 = Math.Min(World.MapTilesX - 1, (int)((Camera.X + ViewW) / World.TileSize) + 1);
        int ty1 = Math.Min(World.MapTilesY - 1, (int)((Camera.Y + ViewH) / World.TileSize) + 1);
        int lavaFrame = (int)(time * 7f) & 3;

        for (int ty = ty0; ty <= ty1; ty++)
        for (int tx = tx0; tx <= tx1; tx++)
        {
            var kind = world.TileAt(tx, ty);
            if (kind == TileKind.Empty) continue;
            int px = tx * World.TileSize;
            int py = ty * World.TileSize;
            int variant = ((tx * 73856093) ^ (ty * 19349663)) & 0x7fffffff;
            switch (kind)
            {
                case TileKind.Rock:
                {
                    _batch.Draw(_art.Tiles, new Vector2(px, py), Art.RockTiles[variant & 3], Color.White);
                    bool openAbove = world.TileAt(tx, ty - 1) is TileKind.Empty or TileKind.Lava or TileKind.Spikes;
                    if (openAbove)
                        _batch.Draw(_art.Tiles, new Vector2(px, py), Art.MossTiles[(variant >> 2) & 1], Color.White);
                    bool openBelow = !world.IsSolid(tx, ty + 1);
                    if (openBelow)
                        Rect(px, py + World.TileSize - 3, World.TileSize, 3, Palette.Ink * 0.45f);
                    if (!world.IsSolid(tx - 1, ty))
                        Rect(px, py, 2, World.TileSize, Palette.Ink * 0.25f);
                    if (!world.IsSolid(tx + 1, ty))
                        Rect(px + World.TileSize - 2, py, 2, World.TileSize, Palette.Ink * 0.25f);
                    break;
                }
                case TileKind.Ledge:
                    _batch.Draw(_art.Tiles, new Vector2(px, py), Art.LedgeTiles[variant & 1], Color.White);
                    break;
                case TileKind.Lava:
                    _batch.Draw(_art.Tiles, new Vector2(px, py), Art.LavaTiles[lavaFrame], Color.White);
                    break;
                case TileKind.Spikes:
                    _batch.Draw(_art.Tiles, new Vector2(px, py), Art.SpikeTiles[variant & 1], Color.White);
                    break;
            }
        }
    }

    public void DrawProps(World world, float time)
    {
        foreach (var prop in world.Props)
        {
            if (!InView(prop.Pos, 64)) continue;
            switch (prop.Kind)
            {
                case PropKind.Torch:
                    _batch.Draw(_art.Torch, new Vector2((int)prop.Pos.X, (int)prop.Pos.Y),
                        new Rectangle(((int)(time * 9f + prop.Phase) & 1) * 16, 0, 16, 32), Color.White);
                    _batch.Draw(_art.Glow, new Rectangle((int)prop.Pos.X - 40, (int)prop.Pos.Y - 30, 96, 96),
                        new Color(255, 170, 80) * 0.6f);
                    break;
                case PropKind.Crystal:
                    _batch.Draw(_art.Tiles, new Vector2((int)prop.Pos.X, (int)prop.Pos.Y),
                        Art.CrystalTiles[prop.Variant & 1], Color.White);
                    _batch.Draw(_art.Glow, new Rectangle((int)prop.Pos.X - 18, (int)prop.Pos.Y - 18, 52, 52),
                        Palette.Teal * (0.28f + 0.12f * MathF.Sin(time * 2f + prop.Phase)));
                    break;
                case PropKind.Mushroom:
                {
                    int x = (int)prop.Pos.X, y = (int)prop.Pos.Y;
                    Color cap = prop.Variant == 0 ? Palette.Pink : prop.Variant == 1 ? Palette.Purple : Palette.Teal;
                    Rect(x + 2, y + 2, 2, 4, new Color(214, 200, 180));
                    Rect(x, y - 2, 6, 4, cap);
                    Rect(x + 1, y - 3, 3, 2, cap * 0.7f);
                    break;
                }
            }
        }
    }

    public void DrawDoors(World world, float time)
    {
        foreach (var door in world.Doors)
        {
            if (!InView(door.Bounds.Center, 120)) continue;
            var color = Palette.DoorColors[door.ColorIndex];
            if (door.Open && door.OpenAmount > 0.99f) continue;
            float alpha = door.Open ? 1f - door.OpenAmount : 1f;
            _batch.Draw(_art.Glow, new Rectangle((int)door.Bounds.Center.X - 60, (int)door.Bounds.Center.Y - 60, 120, 120),
                color * (0.30f * alpha));
            _batch.Draw(_art.Door, new Rectangle((int)door.Bounds.X, (int)door.Bounds.Y,
                (int)door.Bounds.W, (int)door.Bounds.H), Color.White * alpha);
            // tint overlay so the barrier reads as its rune colour
            Rect(door.Bounds.X + 2, door.Bounds.Y + 4, door.Bounds.W - 4, door.Bounds.H - 8, color * (0.25f * alpha));
            // rune sigil
            float cx = door.Bounds.Center.X, cy = door.Bounds.Center.Y;
            Rect(cx - 5, cy - 5, 10, 10, color * (0.9f * alpha));
            Rect(cx - 3, cy - 3, 6, 6, Palette.Ink * (0.8f * alpha));
            if (door.Open)
                Rect(door.Bounds.X, door.Bounds.Y, door.Bounds.W, 3, color * (0.7f * alpha));
        }
    }

    public void DrawAltar(World world, float time)
    {
        var altar = world.Altar;
        if (altar == null || !InView(altar.Bounds.Center, 200)) return;
        _batch.Draw(_art.Altar, new Rectangle((int)altar.Bounds.X, (int)altar.Bounds.Y,
            (int)altar.Bounds.W, (int)altar.Bounds.H), Color.White);
        float glow = world.TreeComplete ? 0.55f + 0.25f * MathF.Sin(time * 3f) : 0.22f;
        var tint = world.TreeComplete ? Palette.GreenLight : Palette.Teal;
        _batch.Draw(_art.Glow, new Rectangle((int)altar.Bounds.Center.X - 90, (int)altar.Bounds.Y - 60, 180, 180),
            tint * (0.35f * glow));
        // fragment progress above the tree
        int rows = (World.FragmentTotal + 5) / 6;
        for (int i = 0; i < World.FragmentTotal; i++)
        {
            int col = i % 6, row = i / 6;
            float x = altar.Bounds.X + 24 + col * 9;
            float y = altar.Bounds.Y - 16 - (rows - 1 - row) * 8;
            bool got = i < world.FragmentsCollected;
            Rect(x, y, 6, 6, got ? Palette.GreenLight : Palette.Ink * 0.6f);
            if (got) Rect(x + 1, y + 1, 4, 4, Palette.Green);
        }
    }

    public void DrawMonstas(World world, float time)
    {
        foreach (var m in world.Monstas)
        {
            if (!InView(m.Pos, 90)) continue;
            int frame = (int)(time * 2.2f + m.Pos.X * 0.02f) & 1;
            _batch.Draw(_art.Monsta, new Rectangle((int)m.Pos.X, (int)m.Pos.Y, 32, 32),
                new Rectangle(frame * 32, 0, 32, 32), Color.White);
            // the two pass colours this creature dispenses
            DrawGem(m.Pos + new Vector2(10, -14), Palette.DoorColors[m.ColorA], m.Produced);
            DrawGem(m.Pos + new Vector2(24, -14), Palette.DoorColors[m.ColorB], m.Produced);
            // feeding tray
            for (int i = 0; i < 3; i++)
            {
                float x = m.Pos.X + 9 + i * 6;
                float y = m.Pos.Y + 27;
                Color c = i < m.Fed ? Palette.Pink : Palette.Ink * 0.7f;
                Rect(x, y, 5, 5, c);
            }
            if (!m.Produced)
                _batch.Draw(_art.Glow, new Rectangle((int)m.Pos.X + 16 - 40, (int)m.Pos.Y + 16 - 40, 80, 80),
                    Palette.Purple * 0.16f);
        }
    }

    private void DrawGem(Vector2 at, Color color, bool lit)
    {
        Rect(at.X, at.Y, 6, 6, color * (lit ? 1f : 0.85f));
        Rect(at.X + 1, at.Y + 1, 3, 3, Color.White * (lit ? 0.95f : 0.5f));
    }

    public void DrawPickups(World world, float time)
    {
        foreach (var p in world.Pickups)
        {
            if (p.Taken || !InView(p.Pos, 60)) continue;
            float bob = MathF.Sin(time * 3f + p.Phase) * 2.5f;
            var pos = new Vector2(p.Pos.X - 8, p.Pos.Y - 8 + bob);
            Rectangle src = p.Kind switch
            {
                PickupKind.Fruit => new Rectangle(0, 0, 16, 16),
                PickupKind.Fragment => new Rectangle(16, 0, 16, 16),
                PickupKind.Pass => new Rectangle(32, 0, 16, 16),
                _ => new Rectangle(48, 0, 16, 16),
            };
            Color glow = p.Kind switch
            {
                PickupKind.Fruit => Palette.Pink,
                PickupKind.Fragment => Palette.GreenLight,
                PickupKind.Pass => Palette.DoorColors[p.ColorIndex],
                _ => Palette.Danger,
            };
            _batch.Draw(_art.Glow, new Rectangle((int)pos.X - 12, (int)pos.Y - 12, 40, 40),
                glow * (0.22f + 0.10f * MathF.Sin(time * 4f + p.Phase)));
            _batch.Draw(_art.Items, pos, src, Color.White);
        }
    }

    public void DrawEnemies(World world, float time)
    {
        foreach (var e in world.Enemies)
        {
            if (!e.Alive || !InView(e.Pos, 60)) continue;
            int frame = (int)(e.Anim * 5f) & 1;
            var src = new Rectangle((int)e.Kind * 24, frame * 24, 24, 24);
            var pos = new Vector2((int)e.Pos.X - 1, (int)e.Pos.Y - 2);
            var effects = e.FacingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            if (e.Kind == EnemyKind.Turret) effects = SpriteEffects.None;
            _batch.Draw(_art.Enemies, pos, src, Color.White, 0f, Vector2.Zero, 1f, effects, 0f);
            Color glow = e.Kind switch
            {
                EnemyKind.Crawler => new Color(255, 150, 90),
                EnemyKind.Floater => Palette.Purple,
                EnemyKind.Turret => Palette.Teal,
                _ => new Color(255, 90, 140),
            };
            _batch.Draw(_art.Glow, new Rectangle((int)pos.X - 12, (int)pos.Y - 12, 48, 48), glow * 0.16f);
        }
    }

    public void DrawPlayer(Player player, float time)
    {
        if (player.Dead) return;
        if (player.Invuln > 0.05f && ((int)(time * 22f) & 1) == 0) return;
        int col = 0;
        if (!player.OnGround)
            col = player.Vel.Y < 0 ? 4 : 5;
        else if (player.ShootFlash > 0)
            col = 2;
        else if (player.Moving)
            col = 1 + ((int)(player.AnimTime * 8f) & 1);
        var src = new Rectangle(col * 24, 0, 24, 24);
        var pos = new Vector2((int)player.Pos.X - 2, (int)player.Pos.Y - 4);
        var effects = player.FacingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        _batch.Draw(_art.Glow, new Rectangle((int)pos.X - 16, (int)pos.Y - 16, 56, 56), Palette.Green * 0.20f);
        _batch.Draw(_art.Sqij, pos, src, Color.White, 0f, Vector2.Zero, 1f, effects, 0f);
        if (player.ShootFlash > 0)
        {
            float mx = player.FacingRight ? player.Pos.X + 22 : player.Pos.X - 6;
            _batch.Draw(_art.Glow, new Rectangle((int)mx - 6, (int)player.Pos.Y - 4, 28, 28),
                Palette.Gold * 0.75f);
        }
        if (player.FlapsLeft > 0 && !player.OnGround)
        {
            for (int i = 0; i < player.FlapsLeft; i++)
                Rect(pos.X - 4 - i * 5, pos.Y + 20, 3, 3, Palette.GreenLight * 0.9f);
        }
    }

    public void DrawProjectiles(World world)
    {
        foreach (var p in world.Projectiles)
        {
            _batch.Draw(_art.Glow, new Rectangle((int)p.Pos.X - 8, (int)p.Pos.Y - 8, 24, 24), p.Color * 0.85f);
            Rect(p.Pos.X - 2, p.Pos.Y - 2, 5, 5, Palette.Paper);
        }
    }

    public void DrawParticles(World world)
    {
        foreach (var p in world.Particles)
        {
            float t = Math.Clamp(p.Life / MathF.Max(0.001f, p.MaxLife), 0f, 1f);
            Rect(p.Pos.X, p.Pos.Y, p.Size, p.Size, p.Color * t);
        }
    }

    private bool InView(Vector2 pos, float margin)
        => pos.X > Camera.X - margin && pos.X < Camera.X + ViewW + margin &&
           pos.Y > Camera.Y - margin && pos.Y < Camera.Y + ViewH + margin;

    // -------------------------------------------------------------------- HUD
    public void DrawHud(World world, Player player, Vector2 playerRoomCentre, string banner, float bannerAlpha,
        string message, float messageAlpha, float objectivePulse)
    {
        // energy
        Rect(8, 8, 152, 38, Palette.Ink * 0.55f);
        float ew = 118;
        Rect(14, 16, ew + 4, 10, Palette.Ink * 0.75f);
        Rect(16, 18, ew, 6, new Color(40, 48, 66));
        float ratio = Math.Clamp(player.Energy / Player.MaxEnergy, 0f, 1f);
        Color ecolor = ratio > 0.55f ? Palette.Green : ratio > 0.28f ? Palette.Gold : Palette.Danger;
        Rect(16, 18, ew * ratio, 6, ecolor);
        for (int i = 1; i < 6; i++) Rect(16 + ew * i / 6f, 18, 1, 6, Palette.Ink * 0.6f);
        Text($"ENERGY { (int)MathF.Ceiling(player.Energy)}%", 16, 28, Palette.Paper);

        // fragments
        Rect(ViewW / 2f - 132, 8, 264, 40, Palette.Ink * 0.5f);
        TextCentered("ENERTREE", ViewW / 2f, 12, Palette.Teal);
        for (int i = 0; i < World.FragmentTotal; i++)
        {
            float x = ViewW / 2f - World.FragmentTotal * 5f + i * 10f;
            bool got = i < world.FragmentsCollected;
            Rect(x, 24, 8, 8, got ? Palette.GreenLight : Palette.Ink * 0.7f);
            Rect(x + 2, 26, 4, 4, got ? Palette.Green : new Color(60, 72, 90));
        }
        TextCentered($"{world.FragmentsCollected}/{World.FragmentTotal}", ViewW / 2f, 36, Palette.Paper * 0.8f);

        // fruit + passes
        Rect(ViewW - 146, 8, 138, 42, Palette.Ink * 0.5f);
        _batch.Draw(_art.Items, new Vector2(ViewW - 132, 14), new Rectangle(0, 0, 16, 16), Color.White);
        Text($"x{world.FruitCount}", ViewW - 112, 18, Palette.Pink);
        for (int i = 0; i < 6; i++)
        {
            float x = ViewW - 72 + (i % 3) * 18;
            float y = 14 + (i / 3) * 17;
            var c = Palette.DoorColors[i];
            Rect(x, y, 14, 14, Palette.Ink * 0.6f);
            Rect(x, y, 14, 1, c * 0.9f);
            Rect(x, y + 13, 14, 1, c * 0.9f);
            Rect(x, y, 1, 14, c * 0.9f);
            Rect(x + 13, y, 1, 14, c * 0.9f);
            _batch.Draw(_art.Items, new Vector2(x - 1, y - 1), new Rectangle(32, 0, 16, 16),
                world.Passes[i] > 0 ? c : new Color(110, 120, 140) * 0.35f);
            if (world.Passes[i] > 1)
                Text($"{world.Passes[i]}", x + 8, y + 6, Palette.Paper, 1f, true);
        }

        if (Minimap) DrawMinimap(world, player);

        // objective ring marker
        DrawWaypoint(world, player, objectivePulse);

        if (bannerAlpha > 0.01f)
        {
            Rect(ViewW / 2f - 150, 54, 300, 30, Palette.Ink * (0.55f * bannerAlpha));
            TextCentered(banner, ViewW / 2f, 58, Palette.Teal * bannerAlpha, 2f, true);
            TextCentered("SECTOR " + SectorName(world, player), ViewW / 2f, 76,
                Palette.Paper * (0.6f * bannerAlpha));
        }

        if (messageAlpha > 0.01f)
            TextCentered(message, ViewW / 2f, ViewH - 54, Palette.Gold * messageAlpha, 1f, true);

        if (DamageFlash > 0.01f)
            Rect(0, 0, ViewW, ViewH, Palette.Danger * (0.28f * DamageFlash));
    }

    private static string SectorName(World world, Player player)
    {
        var (x, y) = world.RoomAt(player.Center);
        return $"{x + 1}-{y + 1}";
    }

    private void DrawMinimap(World world, Player player)
    {
        const int cell = 3;
        const int gap = 1;
        float w = World.RoomsX * (cell + gap);
        float h = World.RoomsY * (cell + gap);
        float x0 = 14, y0 = 46;
        Rect(x0 - 4, y0 - 4, w + 8, h + 8, Palette.Ink * 0.75f);
        Rect(x0 - 4, y0 - 4, w + 8, 1, Palette.Teal * 0.5f);
        var (px, py) = world.RoomAt(player.Center);
        for (int y = 0; y < World.RoomsY; y++)
        for (int x = 0; x < World.RoomsX; x++)
        {
            var info = world.Rooms[y * World.RoomsX + x];
            float x1 = x0 + x * (cell + gap);
            float y1 = y0 + y * (cell + gap);
            if (!info.Visited)
            {
                Rect(x1, y1, cell, cell, new Color(44, 54, 76) * 0.85f);
                continue;
            }
            Color c = info.Vault ? new Color(66, 118, 104) : new Color(58, 100, 116);
            if (info.Vault && info.FragmentIndex >= 0 && info.FragmentIndex >= world.FragmentsCollected)
                c = new Color(140, 220, 150);
            Rect(x1, y1, cell, cell, c);
            if (x == px && y == py)
                Rect(x1 - 1, y1 - 1, cell + 2, cell + 2, Palette.Paper);
        }
        Text("MAP", x0 + w + 6, y0, Palette.Teal * 0.9f);
    }

    private void DrawWaypoint(World world, Player player, float pulse)
    {
        Vector2? target = null;
        foreach (var p in world.Pickups)
        {
            if (p.Kind != PickupKind.Fragment || p.Taken) continue;
            if (target == null || Vector2.DistanceSquared(p.Pos, player.Center) <
                Vector2.DistanceSquared(target.Value, player.Center))
                target = p.Pos;
        }
        if (target == null && world.Altar != null) target = world.Altar.Bounds.Center;
        if (target == null) return;
        var dir = target.Value - player.Center;
        if (dir.LengthSquared() < 200 * 200) return;
        dir.Normalize();
        var centre = new Vector2(ViewW / 2f, ViewH / 2f) + dir * 120f;
        var color = world.TreeComplete ? Palette.Teal : Palette.GreenLight;
        for (int i = 0; i < 4; i++)
            Rect(centre.X + dir.X * i * 3 - 1.5f, centre.Y + dir.Y * i * 3 - 1.5f, 3, 3,
                color * (0.7f - i * 0.14f) * (0.6f + 0.4f * pulse));
    }

    public void DrawOverlays()
    {
        _batch.Draw(_art.Vignette, new Rectangle(0, 0, ViewW, ViewH), Color.White);
        if (Scanlines)
            _batch.Draw(_art.Scanlines, new Rectangle(0, 0, ViewW, ViewH), new Color(255, 255, 255) * 0.35f);
    }
}
