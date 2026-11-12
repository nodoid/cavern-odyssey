using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SuperSqij;

/// <summary>
/// Store-asset capture mode: a small scripted player drives the game and every frame is
/// written as raw RGBA to a file that ffmpeg turns into a video. Enabled by the desktop
/// build's --capture switch; a normal run never touches any of this.
/// </summary>
public sealed partial class SuperSqijGame
{
    private sealed record CaptureRequest(string Path, int Width, int Height, int Frames, int RoomSlot,
        string ShotMode, bool Mobile);

    private CaptureRequest? _captureRequest;
    private RenderTarget2D? _captureTarget;
    private FileStream? _captureStream;
    private byte[]? _captureBuffer;
    private Color[]? _captureColors;
    private int _capturedFrames;
    private float _capturedTime;

    // Demo player state, driven by a fixed seed so every capture shows the same run.
    private uint _demoRandom = 20260101;
    private int _demoDir = 1;
    private float _demoDirTimer;
    private float _demoStuck;
    private float _demoJumpCooldown;
    private float _demoSpitCooldown;
    private int _demoTitleStep;
    private Pickup? _demoTarget;
    private float _demoTargetTimer;
    private float _demoTargetAge;
    private readonly List<Pickup> _demoAvoid = new();
    private Vector2 _demoLastPos;
    private float _demoStillTime;

    private System.Diagnostics.Stopwatch? _captureStopwatch;

    public void RequestCapture(string path, int width, int height, int frames, int roomSlot = 0,
        string shotMode = "", bool mobile = false)
        => _captureRequest = new CaptureRequest(path, width, height, frames, roomSlot, shotMode, mobile);

    private bool Capturing => _captureRequest != null;

    /// <summary>Starts a capture; later clips skip the splash and title and open on gameplay.</summary>
    private void BeginCapture()
    {
        // Store screenshots hold one screen still instead of playing a demo run.
        if (_captureRequest!.ShotMode.Length > 0)
        {
            StartNewGame(1986);
            switch (_captureRequest.ShotMode)
            {
                case "title": SetMode(GameMode.Title); break;
                case "scores": SetMode(GameMode.HighScores); break;
                case "credits": SetMode(GameMode.Credits); break;
                default: WarpToRoomSlot(_captureRequest.RoomSlot); break;
            }
            StartCapture();
            return;
        }

        if (_captureRequest.RoomSlot > 0)
        {
            StartNewGame(1986);
            WarpToRoomSlot(_captureRequest.RoomSlot);
            // The tutorial line belongs in the opening clip only; later clips show the
            // room banner and the controls hint instead.
            _messageTimer = 0f;
            _introTimer = 3f;
            _demoTitleStep = 3;
        }
        StartCapture();
    }

    private void StartCapture()
    {
        var req = _captureRequest!;
        _captureTarget = new RenderTarget2D(GraphicsDevice, req.Width, req.Height, false,
            SurfaceFormat.Color, DepthFormat.None);
        _captureColors = new Color[req.Width * req.Height];
        _captureBuffer = new byte[_captureColors.Length * 4];
        _captureStream = new FileStream(req.Path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 22);
        IsMouseVisible = false;
        // One Draw per Update keeps the dump in lockstep with the 30fps encode.
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30.0);
        _demoDirTimer = 2.5f;
        _demoSpitCooldown = 1.5f;
        // The recorded audio starts when the audio device opened, which happens just
        // before this. Log how far into it the first frame lands so the mux can trim.
        _captureStopwatch = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"CAPTURE: {req.Width}x{req.Height}, {req.Frames} frames -> {req.Path}");
    }

    private void CaptureFrame(float dt)
    {
        if (_captureStream == null) return;
        var req = _captureRequest!;
        float scale = MathF.Min(req.Width / (float)Renderer.ViewW, req.Height / (float)Renderer.ViewH);
        int dw = (int)MathF.Round(Renderer.ViewW * scale);
        int dh = (int)MathF.Round(Renderer.ViewH * scale);

        GraphicsDevice.SetRenderTarget(_captureTarget);
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _spriteBatch.Draw(_scene, new Rectangle((req.Width - dw) / 2, (req.Height - dh) / 2, dw, dh), Color.White);
        _spriteBatch.End();
        _captureTarget!.GetData(_captureColors);
        GraphicsDevice.SetRenderTarget(null);

        var colors = _captureColors!;
        var buffer = _captureBuffer!;
        for (int i = 0, o = 0; i < colors.Length; i++, o += 4)
        {
            buffer[o] = colors[i].R;
            buffer[o + 1] = colors[i].G;
            buffer[o + 2] = colors[i].B;
            buffer[o + 3] = 255;
        }
        if (_capturedFrames == 0 && _captureStopwatch != null)
            Console.WriteLine($"CAPTURE: audio offset {_captureStopwatch.Elapsed.TotalSeconds:0.000}s");

        _captureStream.Write(buffer, 0, buffer.Length);
        _capturedFrames++;
        _capturedTime += dt;
        if (_capturedFrames < req.Frames) return;

        _captureStream.Flush();
        _captureStream.Dispose();
        _captureStream = null;
        Console.WriteLine($"CAPTURE: wrote {_capturedFrames} frames covering {_capturedTime:0.00}s");
        Exit();
    }

    /// <summary>Replaces the real input with the scripted demo player.</summary>
    private Controls DemoControls(float dt)
    {
        var c = new Controls();
        if (_captureRequest!.ShotMode.Length > 0) return c;
        if (_mode != GameMode.Title) _demoTitleStep = 0;

        switch (_mode)
        {
            case GameMode.Title:
                // Walk the menu down and back up, then start the descent.
                if (_demoTitleStep == 0 && _modeTime > 1.0f) { c.MenuDownPressed = true; _demoTitleStep = 1; }
                else if (_demoTitleStep == 1 && _modeTime > 1.8f) { c.MenuUpPressed = true; _demoTitleStep = 2; }
                else if (_demoTitleStep == 2 && _modeTime > 2.9f)
                {
                    StartNewGame(1986);
                    WarpToRoomSlot(_captureRequest!.RoomSlot);
                    _demoTitleStep = 3;
                }
                break;

            case GameMode.Playing:
                DemoPlay(dt, ref c);
                break;

            case GameMode.GameOver:
                if (_modeTime > 2.5f) c.ConfirmPressed = true;
                break;
        }

        return c;
    }

    private void DemoPlay(float dt, ref Controls c)
    {
        var p = _player;
        _demoDirTimer -= dt;
        _demoJumpCooldown -= dt;
        _demoSpitCooldown -= dt;
        _demoTargetTimer -= dt;

        // Head for the closest uncollected pickup; that keeps the footage moving and
        // makes the run read like someone actually playing.
        if (_demoTarget is null || _demoTarget.Taken || _demoTargetTimer <= 0f)
        {
            var previous = _demoTarget;
            _demoTarget = NearestPickup(p.Center);
            if (!ReferenceEquals(previous, _demoTarget)) _demoTargetAge = 0f;
            _demoTargetTimer = 0.6f;
        }
        // Give up on a pickup the bot cannot work out how to reach, so the run keeps
        // travelling instead of hopping at the same ledge for half the video.
        _demoTargetAge += dt;
        if (_demoTarget != null && _demoTargetAge > 4.5f)
        {
            _demoAvoid.Add(_demoTarget);
            _demoTarget = null;
            _demoTargetTimer = 0f;
            _demoDir = -_demoDir;
            _demoJumpCooldown = 0.5f;
        }

        Vector2? goal = _demoTarget?.Pos;
        if (goal is { } g && MathF.Abs(g.X - p.Center.X) > 10f)
            _demoDir = g.X > p.Center.X ? 1 : -1;

        var ahead = new Vector2(p.Center.X + _demoDir * (Player.Width / 2f + 4), p.Pos.Y + 10);
        bool wallAhead = _world.HitsSolid(RectF.FromCenter(ahead, 4, 8));
        bool pitAhead = !_world.IsSolidPx(new Vector2(p.Center.X + _demoDir * 18f, p.Pos.Y + Player.Height + 8));
        bool above = goal is { } gg && gg.Y < p.Pos.Y - 14f;

        _demoStillTime = Vector2.DistanceSquared(p.Pos, _demoLastPos) < 4f ? _demoStillTime + dt : 0f;
        _demoLastPos = p.Pos;

        _demoStuck = wallAhead && p.OnGround ? _demoStuck + dt : 0f;
        bool wantsJump = _demoStuck > 0.12f || (pitAhead && p.OnGround) || above;
        if (wantsJump && p.OnGround && _demoJumpCooldown <= 0f)
        {
            c.JumpPressed = true;
            _demoJumpCooldown = 0.28f;
        }
        // Flap to gain height while climbing, or break out of a corner.
        if (!p.OnGround && p.Vel.Y > 20f && p.FlapsLeft > 0 &&
            (above || _demoStillTime > 0.5f || (_demoStuck > 0.2f && DemoRandom() < 0.15f)))
            c.JumpPressed = true;

        if (_demoStuck > 0.9f)
        {
            _demoDir = -_demoDir;
            _demoStuck = 0f;
            _demoDirTimer = 1.6f;
        }
        if (_demoDirTimer <= 0f)
        {
            if (_demoTarget is null && DemoRandom() < 0.5f) _demoDir = -_demoDir;
            _demoDirTimer = 2.4f + DemoRandom() * 2f;
        }

        c.MoveX = _demoDir;
        if (_demoSpitCooldown <= 0f)
        {
            c.FirePressed = true;
            _demoSpitCooldown = 2.4f + DemoRandom() * 2.2f;
        }
    }

    private Pickup? NearestPickup(Vector2 from)
        => NearestPickup(from, 360f);

    private Pickup? NearestPickup(Vector2 from, float radius)
    {
        Pickup? best = null;
        float bestDistance = radius * radius;
        foreach (var pickup in _world.Pickups)
        {
            if (pickup.Taken || _demoAvoid.Contains(pickup)) continue;
            float d = Vector2.DistanceSquared(from, pickup.Pos);
            if (d >= bestDistance) continue;
            bestDistance = d;
            best = pickup;
        }
        // Nothing in reach once the bot has given up on the local pickups: reopen the
        // whole level so it heads off somewhere new.
        if (best is null && _demoAvoid.Count > 0)
        {
            _demoAvoid.Clear();
            return null;
        }
        return best;
    }

    /// <summary>
    /// Drops the player into a different part of the level so each clip in a montage
    /// shows a different room. Slot 0 keeps the normal start.
    /// </summary>
    private void WarpToRoomSlot(int slot)
    {
        if (slot <= 0) return;
        int index = ChooseRoom(slot);
        if (index < 0) return;

        int rx = index % World.RoomsX;
        int ry = index / World.RoomsX;
        int x0 = rx * World.RoomTilesX;
        int y0 = ry * World.RoomTilesY;
        // Walk outwards from the middle of the room looking for a floor tile with clear
        // space above it, so the player never lands inside geometry.
        for (int step = 0; step < World.RoomTilesX; step++)
        {
            int tx = x0 + (World.RoomTilesX / 2) + (step % 2 == 0 ? step / 2 : -(step + 1) / 2);
            if (tx < x0 || tx >= x0 + World.RoomTilesX) continue;
            for (int ty = y0 + World.RoomTilesY - 1; ty > y0 + 2; ty--)
            {
                if (!_world.IsSolid(tx, ty)) continue;
                if (_world.IsSolid(tx, ty - 1) || _world.IsSolid(tx, ty - 2)) continue;
                _player.Reset(new Vector2(tx * World.TileSize + 6, ty * World.TileSize - Player.Height - 2));
                _world.VisitRoom(_player.Center);
                SnapCamera();
                Console.WriteLine($"CAPTURE: warped to room {index} '{_world.Rooms[index].Name}'");
                return;
            }
        }
    }

    /// <summary>Picks a room that shows off something different in each clip.</summary>
    private int ChooseRoom(int slot)
    {
        int RoomOf(Vector2 position)
        {
            var (x, y) = _world.RoomAt(position);
            return y * World.RoomsX + x;
        }

        var start = RoomOf(_world.StartPosition);
        IEnumerable<int> Candidates() => slot switch
        {
            1 => _world.Monstas.Select(m => RoomOf(m.Pos)),
            2 => _world.Doors.Select(d => RoomOf(d.Bounds.Center)),
            _ => _world.Enemies.Select(e => RoomOf(e.Pos)),
        };

        foreach (var room in Candidates().Distinct().OrderBy(i => i))
            if (room != start) return room;
        return -1;
    }

    /// <summary>xorshift32: a fixed sequence, so the captured run is repeatable.</summary>
    private float DemoRandom()
    {
        _demoRandom ^= _demoRandom << 13;
        _demoRandom ^= _demoRandom >> 17;
        _demoRandom ^= _demoRandom << 5;
        return (_demoRandom & 0xFFFFFF) / (float)0x1000000;
    }
}
