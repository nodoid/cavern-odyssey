using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace SuperSqij;

public enum GameMode { Splash, Title, Help, Options, Controls, Credits, Editor, Playing, Paused, GameOver, Victory, NameEntry, HighScores }

public sealed partial class SuperSqijGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _scene = null!;
    private Art _art = null!;
    private Renderer _renderer = null!;
    private AudioSys _audio = null!;
    private readonly InputRouter _input = new();

    private World _world = null!;
    private Player _player = null!;
    private GameMode _mode = GameMode.Splash;
    private float _time;
    private float _modeTime;
    private float _runTime;
    private float _splashTime;
    private float _bannerTimer;
    private string _banner = "";
    private float _messageTimer;
    private string _message = "";
    private float _messageCooldown;
    private string _context = "";
    private float _shake;
    private Vector2 _camera;
    private int _lastRoomX = -1, _lastRoomY = -1;
    private int _menuIndex;
    private int _pauseIndex;
    private int _optionIndex;
    private float _introTimer;
    private Vector2 _virtualScale = Vector2.One;
    private Vector2 _virtualOffset;
    private bool _mobile;
    private bool _fullscreen;
    private KeyboardState _prevKeys;
    private bool _loggedFirstDraw;
    private bool _loggedBlit;

    // ------------------------------------------------------------------ editor
    private World? _editorWorld;
    private Vector2 _editorCamera;
    private Vector2 _editorCursor;
    private int _editorBrush = 1;
    private bool _editorGrid = true;
    private string _editorStatus = "";
    private float _editorStatusTimer;
    private KeyboardState _editorPrevKeys;
    private MouseState _editorPrevMouse;
    private bool _editorHasPrevInput;

    private enum EditorBrush
    {
        Empty, Rock, Ledge, Lava, Spikes,
        Fruit, Fragment, Pass, Monsta, Door, Torch, Crystal, Mushroom, Altar,
    }

    private static readonly (EditorBrush Brush, string Name, Color Tint)[] BrushList =
    {
        (EditorBrush.Empty, "ERASE", new Color(58, 68, 88)),
        (EditorBrush.Rock, "ROCK", new Color(102, 122, 162)),
        (EditorBrush.Ledge, "LEDGE", new Color(72, 152, 104)),
        (EditorBrush.Lava, "LAVA", new Color(255, 150, 80)),
        (EditorBrush.Spikes, "SPIKES", new Color(200, 210, 230)),
        (EditorBrush.Fruit, "FRUIT", Palette.Pink),
        (EditorBrush.Fragment, "SHARD", Palette.GreenLight),
        (EditorBrush.Pass, "PASS", Palette.Gold),
        (EditorBrush.Monsta, "MONSTA", Palette.Purple),
        (EditorBrush.Door, "DOOR", Palette.Teal),
        (EditorBrush.Torch, "TORCH", new Color(255, 170, 80)),
        (EditorBrush.Crystal, "CRYSTAL", Palette.Teal),
        (EditorBrush.Mushroom, "SHROOM", Palette.PurpleLight),
        (EditorBrush.Altar, "ALTAR", Palette.Green),
    };

    private const int PaletteCols = 7;
    private const int PaletteCellW = 72;
    private const int PaletteCellH = 26;
    private int _editorColor;

    private string[] _titleItems = { "START DESCENT", "DIFFICULTY", "HOW TO PLAY", "HIGH SCORES", "OPTIONS", "CREDITS" };
    private string _titleNotice = "";
    private float _titleNoticeTimer;
    private Difficulty _difficulty = Difficulty.Medium;
    private List<HighScoreEntry> _highScoreTable = new();
    private int _lastScore;
    private int _lastRank;
    private bool _scoreSubmitted;

    // ------------------------------------------------------------------ name entry
    private const string NameLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int NameCols = 6;
    private const int NameCellW = 30;
    private const int NameCellH = 18;
    private const int NameGridX = 230;
    private const int NameGridY = 150;
    private const int NameMaxLength = 6;
    private readonly List<char> _nameChars = new();
    private int _nameCursor;
    private bool _pendingVictory;
    private GameMode _pendingEndMode = GameMode.GameOver;
    private KeyboardState _namePrevKeys;
    private static readonly string[] PauseItems = { "RESUME", "RESTART", "QUIT TO TITLE" };
    private string[] _optionLabels = { "SOUND", "SCANLINES", "FULLSCREEN", "CONTROLS", "BACK" };
    private readonly KeyBindings _bindings = KeyBindings.Load();
    private GameAction? _rebindAction;
    private int _keyIndex;
    private KeyboardState _rebindPrevKeys;

    public SuperSqijGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = false,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        Window.Title = "Cavern Odyssey";
    }

    protected override void Initialize()
    {
        Console.WriteLine("SQIJ: Game.Initialize");
#if IOS
        _mobile = true;
        // Attach MonoGame's legacy UIWindow to the UIKit scene (required on iOS 27).
        if (Services.GetService(typeof(UIKit.UIWindow)) is UIKit.UIWindow iosWindow &&
            SuperSqij.iOS.SceneDelegate.Instance is { } sceneDelegate)
        {
            iosWindow.WindowScene = sceneDelegate.Scene;
            // CoordinateSpace is obsoleted in iOS 26; the scene's effective geometry
            // is the documented replacement on 26 and later.
            var sceneSpace = OperatingSystem.IsIOSVersionAtLeast(26)
                ? sceneDelegate.Scene.EffectiveGeometry.CoordinateSpace
                : sceneDelegate.Scene.CoordinateSpace;
            iosWindow.Frame = sceneSpace.Bounds;
            iosWindow.MakeKeyAndVisible();
            sceneDelegate.Window = iosWindow;
        }
#endif
#if ANDROID
        _mobile = true;
#endif
#if DESKTOP
        Window.AllowUserResizing = true;
#endif
        _scene = new RenderTarget2D(GraphicsDevice, Renderer.ViewW, Renderer.ViewH, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        // The editor needs a mouse and hotkeys, so phones only get the normal entries.
        if (!_mobile)
            _titleItems = new[]
            {
                "START DESCENT", "DIFFICULTY", "CUSTOM LEVEL", "HOW TO PLAY", "HIGH SCORES", "OPTIONS",
                "CREDITS", "LEVEL EDITOR",
            };
        else
            // Phones are always fullscreen, so that switch would be a dead entry.
            _optionLabels = new[] { "SOUND", "SCANLINES", "BACK" };
        _input.Bindings = _bindings;
        _input.ScreenSize = new Vector2(GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight);
        _input.VirtualScale = new Vector2(GraphicsDevice.PresentationParameters.BackBufferWidth / (float)Renderer.ViewW,
            GraphicsDevice.PresentationParameters.BackBufferHeight / (float)Renderer.ViewH);
#if DESKTOP
        // Keep the whole 16:9 frame (including the bottom hint line) on screen once
        // the title bar and dock/menu bar are taken into account.
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        int fitW = Math.Min(1024, Math.Max(640, display.Width - 120));
        int fitH = Math.Min(576, Math.Max(360, display.Height - 180));
        float fit = MathF.Min(fitW / 1280f, fitH / 720f);
        int winW = Math.Max(640, (int)(1280 * fit));
        int winH = Math.Max(360, (int)(720 * fit));
        if (winW != GraphicsDevice.PresentationParameters.BackBufferWidth ||
            winH != GraphicsDevice.PresentationParameters.BackBufferHeight)
        {
            _graphics.PreferredBackBufferWidth = winW;
            _graphics.PreferredBackBufferHeight = winH;
            _graphics.ApplyChanges();
            _input.ScreenSize = new Vector2(winW, winH);
            _input.VirtualScale = new Vector2(winW / (float)Renderer.ViewW, winH / (float)Renderer.ViewH);
        }
#endif
        base.Initialize();
    }

    protected override void LoadContent()
    {
        Console.WriteLine("SQIJ: Game.LoadContent");
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _art = new Art(GraphicsDevice);
        _renderer = new Renderer(_art);
        _renderer.Bind(_spriteBatch);
        _audio = new AudioSys();
        _audio.Initialize();
        _audio.StartMusic(MusicTrack.Game);

        _world = LevelGenerator.Generate(1986);
        _player = new Player();
        _player.Reset(_world.StartPosition);
        SnapCamera();
        _renderer.Scanlines = true;
#if IOS
        if (SuperSqij.iOS.Program.Autostart) StartNewGame(1986);
#endif
        if (Capturing) BeginCapture();
    }


    // -------------------------------------------------------------- lifecycle
    private void StartNewGame(int seed) => BeginRun(LevelGenerator.Generate(seed, _difficulty));

    private void BeginRun(World world)
    {
        _world = world;
        _difficulty = world.Difficulty;
        _scoreSubmitted = false;
        _lastScore = 0;
        _lastRank = 0;
        _player = new Player();
        _player.Reset(_world.StartPosition);
        _world.VisitRoom(_player.Center);
        _lastRoomX = _lastRoomY = -1;
        _bannerTimer = 0;
        _messageTimer = 0;
        _messageCooldown = 0;
        _runTime = 0;
        _introTimer = 9f;
        _shake = 0;
        SnapCamera();
        ShowMessage("FEED 3 FRUITS TO EACH DISPENSA-MONSTA TO FORGE PASSES", 6.5f);
        _mode = GameMode.Playing;
        _modeTime = 0;
        _audio.StartMusic(MusicTrack.Game);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 1f / 20f);
        _time += dt;
        _modeTime += dt;
        _messageTimer = MathF.Max(0, _messageTimer - dt);
        _messageCooldown = MathF.Max(0, _messageCooldown - dt);
        _bannerTimer = MathF.Max(0, _bannerTimer - dt);
        _shake = MathF.Max(0, _shake - dt * 2.8f);
        _editorStatusTimer = MathF.Max(0, _editorStatusTimer - dt);
        _titleNoticeTimer = MathF.Max(0, _titleNoticeTimer - dt);
        _renderer.DamageFlash = MathF.Max(0, _renderer.DamageFlash - dt * 2.5f);

        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.F11) && !_prevKeys.IsKeyDown(Keys.F11))
            ToggleFullscreen();
        _prevKeys = kb;

        var c = _input.Read();
        // Capture mode swaps in the scripted demo player (see CaptureMode.cs).
        if (Capturing) c = DemoControls(dt);
        switch (_mode)
        {
            case GameMode.Splash:
                _splashTime += dt;
                if (_splashTime > 2.6f || (_splashTime > 1.2f && c.ConfirmPressed)) SetMode(GameMode.Title);
                break;

            case GameMode.Title:
                UpdateTitle(c);
                break;

            case GameMode.Help:
                if (c.ConfirmPressed || c.CancelPressed) SetMode(GameMode.Title);
                break;

            case GameMode.Options:
                UpdateOptions(c);
                break;

            case GameMode.Controls:
                UpdateControls(c);
                break;

            case GameMode.Credits:
                if (c.ConfirmPressed || c.CancelPressed) SetMode(GameMode.Title);
                break;

            case GameMode.HighScores:
                if (c.ConfirmPressed || c.CancelPressed) SetMode(GameMode.Title);
                break;

            case GameMode.Editor:
                UpdateEditor(dt);
                break;

            case GameMode.Playing:
                if (c.MapPressed) _renderer.Minimap = !_renderer.Minimap;
                if (c.PausePressed)
                {
                    _mode = GameMode.Paused;
                    _modeTime = 0;
                    _pauseIndex = 0;
                    _audio.Play("menu");
                    break;
                }
                UpdatePlaying(dt, c);
                break;

            case GameMode.Paused:
                UpdatePaused(c);
                break;

            case GameMode.GameOver:
                if (_modeTime > 0.8f && c.ConfirmPressed) StartNewGame(Random.Shared.Next());
                else if (c.CancelPressed) SetMode(GameMode.Title);
                break;

            case GameMode.Victory:
                if (c.ConfirmPressed || c.CancelPressed) SetMode(GameMode.Title);
                break;

            case GameMode.NameEntry:
                UpdateNameEntry(c);
                break;
        }

        if (_mode == GameMode.Title || _mode == GameMode.Splash)
            UpdateTitleBackdrop(dt);

        base.Update(gameTime);
    }

    private void SetMode(GameMode mode)
    {
        _mode = mode;
        _modeTime = 0;
        _menuIndex = 0;
        if (mode == GameMode.HighScores) _highScoreTable = HighScores.Load();
        if (mode == GameMode.Controls) { _keyIndex = 0; _rebindAction = null; }
        switch (mode)
        {
            case GameMode.Title: _audio.StartMusic(MusicTrack.Game); break;
            case GameMode.HighScores: _audio.StartMusic(MusicTrack.Scores); break;
            case GameMode.NameEntry:
            case GameMode.GameOver:
            case GameMode.Victory: _audio.StartMusic(MusicTrack.GameOver); break;
        }
    }

    private string TitleLabel(string key) =>
        key == "DIFFICULTY" ? $"DIFFICULTY : {_difficulty.Name()}" : key;

    private void CycleDifficulty()
    {
        _difficulty = _difficulty switch
        {
            Difficulty.Easy => Difficulty.Medium,
            Difficulty.Medium => Difficulty.Hard,
            _ => Difficulty.Easy,
        };
        _titleNotice = $"DIFFICULTY SET TO {_difficulty.Name()}";
        _titleNoticeTimer = 2.5f;
    }

    private void UpdateTitle(Controls c)
    {
        if (c.MenuUpPressed) { _menuIndex = (_menuIndex + _titleItems.Length - 1) % _titleItems.Length; _audio.Play("menu"); }
        if (c.MenuDownPressed) { _menuIndex = (_menuIndex + 1) % _titleItems.Length; _audio.Play("menu"); }
        if (c.Tap is { } tap)
        {
            int row = TitleItemAt(tap);
            if (row >= 0)
            {
                _menuIndex = row;
                _audio.Play("pickup");
                ChooseTitleItem();
                return;
            }
            // A tap on the logo or empty background should not fire a menu item.
            if (_mobile) return;
        }
        if (!c.ConfirmPressed) return;
        _audio.Play("pickup");
        ChooseTitleItem();
    }

    private void ChooseTitleItem()
    {
        switch (_titleItems[_menuIndex])
        {
            case "START DESCENT": StartNewGame(Random.Shared.Next()); break;
            case "DIFFICULTY":
                CycleDifficulty();
                _titleNoticeTimer = 2.5f;
                break;
            case "HIGH SCORES": SetMode(GameMode.HighScores); break;
            case "CUSTOM LEVEL": PlaySavedLevel(); break;
            case "HOW TO PLAY": SetMode(GameMode.Help); break;
            case "OPTIONS": SetMode(GameMode.Options); break;
            case "CREDITS": SetMode(GameMode.Credits); break;
            case "LEVEL EDITOR": EnterEditor(); break;
        }
    }

    private int TitleItemAt(Vector2 p)
    {
        if (p.X < Renderer.ViewW / 2f - 120 || p.X > Renderer.ViewW / 2f + 120) return -1;
        for (int i = 0; i < _titleItems.Length; i++)
        {
            float y = 190 + i * 20;
            if (p.Y >= y - 6 && p.Y < y + 14) return i;
        }
        return -1;
    }

    private void UpdateOptions(Controls c)
    {
        if (c.MenuUpPressed) { _optionIndex = (_optionIndex + _optionLabels.Length - 1) % _optionLabels.Length; _audio.Play("menu"); }
        if (c.MenuDownPressed) { _optionIndex = (_optionIndex + 1) % _optionLabels.Length; _audio.Play("menu"); }
        if (c.CancelPressed) { SetMode(GameMode.Title); return; }
        if (c.Tap is { } tap)
        {
            int row = OptionItemAt(tap);
            if (row >= 0)
            {
                _optionIndex = row;
                _audio.Play("menu");
                ChooseOption();
                return;
            }
            if (_mobile) return;
        }
        if (!c.ConfirmPressed) return;
        _audio.Play("menu");
        ChooseOption();
    }

    private void ChooseOption()
    {
        switch (_optionLabels[_optionIndex])
        {
            case "SOUND": _audio.Toggle(); break;
            case "SCANLINES": _renderer.Scanlines = !_renderer.Scanlines; break;
            case "FULLSCREEN": ToggleFullscreen(); break;
            case "CONTROLS": SetMode(GameMode.Controls); break;
            default: SetMode(GameMode.Title); break;
        }
    }

    private int OptionItemAt(Vector2 p)
    {
        if (p.X < Renderer.ViewW / 2f - 130 || p.X > Renderer.ViewW / 2f + 130) return -1;
        for (int i = 0; i < _optionLabels.Length; i++)
        {
            float y = 130 + i * 30;
            if (p.Y >= y - 8 && p.Y < y + 22) return i;
        }
        return -1;
    }

    private void UpdatePaused(Controls c)
    {
        if (c.MenuUpPressed) { _pauseIndex = (_pauseIndex + PauseItems.Length - 1) % PauseItems.Length; _audio.Play("menu"); }
        if (c.MenuDownPressed) { _pauseIndex = (_pauseIndex + 1) % PauseItems.Length; _audio.Play("menu"); }
        if (c.PausePressed) { _mode = GameMode.Playing; _modeTime = 0; return; }
        if (c.Tap is { } tap)
        {
            int row = PauseItemAt(tap);
            if (row >= 0)
            {
                _pauseIndex = row;
                _audio.Play("menu");
                ChoosePauseItem();
                return;
            }
        }
        if (!c.ConfirmPressed && !c.InteractPressed) return;
        _audio.Play("menu");
        ChoosePauseItem();
    }

    private void ChoosePauseItem()
    {
        switch (_pauseIndex)
        {
            case 0: _mode = GameMode.Playing; _modeTime = 0; break;
            case 1: StartNewGame(Random.Shared.Next()); break;
            default: SetMode(GameMode.Title); break;
        }
    }

    private static int PauseItemAt(Vector2 p)
    {
        for (int i = 0; i < PauseItems.Length; i++)
        {
            float y = 170 + i * 28;
            if (p.X >= Renderer.ViewW / 2f - 120 && p.X <= Renderer.ViewW / 2f + 120 &&
                p.Y >= y - 8 && p.Y <= y + 18)
                return i;
        }
        return -1;
    }

    private void UpdateTitleBackdrop(float dt)
    {
        float cx = _world.Altar.Bounds.Center.X;
        float cy = _world.Altar.Bounds.Center.Y;
        float maxX = Math.Max(0, World.MapWidth - Renderer.ViewW);
        float maxY = Math.Max(0, World.MapHeight - Renderer.ViewH);
        var target = new Vector2(
            Math.Clamp(cx - Renderer.ViewW / 2f + MathF.Sin(_time * 0.18f) * 60f, 0, maxX),
            Math.Clamp(cy - Renderer.ViewH / 2f - 30 + MathF.Sin(_time * 0.27f) * 18f, 0, maxY));
        _camera = Vector2.Lerp(_camera, target, 1f - MathF.Exp(-dt * 1.2f));
    }

    // ------------------------------------------------------------------ play
    private void UpdatePlaying(float dt, Controls c)
    {
        _runTime += dt;
        _introTimer = MathF.Max(0, _introTimer - dt);

        _player.Update(_world, c, dt,
            onFlap: p =>
            {
                _audio.Play("flap", 0.55f);
                _world.SpawnParticles(p + new Vector2(10, 16), Palette.Teal, 4, 45f, 0.3f);
            },
            onShoot: (origin, right) =>
            {
                _audio.Play("shot", 0.5f);
                _world.Projectiles.Add(new Projectile
                {
                    Pos = origin + new Vector2(right ? 10 : -10, 0),
                    Vel = new Vector2(right ? 330 : -330, 0),
                    FromPlayer = true,
                    Color = Palette.GreenLight,
                });
                _world.SpawnParticles(origin + new Vector2(right ? 12 : -12, 0), Palette.Gold, 3, 35f, 0.2f);
            });

        if (_player.Dead)
        {
            SubmitScore(false);
            _audio.StopMusic();
            _audio.Play("death");
            _audio.StartMusic(MusicTrack.GameOver);
            _world.SpawnParticles(_player.Center, Palette.Green, 24, 140f, 0.8f);
            if (_mode != GameMode.NameEntry) { _mode = GameMode.GameOver; _modeTime = 0; }
            return;
        }

        UpdateRoomState();
        UpdatePickups();
        UpdateInteractions(c);
        UpdateDoors();
        UpdateEnemies(dt);
        UpdateProjectiles(dt);
        UpdateMonstas(dt);
        _world.UpdateEffectParticles(dt);
        UpdateCamera(dt);
    }

    private void UpdateRoomState()
    {
        var (rx, ry) = _world.RoomAt(_player.Center);
        if (rx != _lastRoomX || ry != _lastRoomY)
        {
            _lastRoomX = rx;
            _lastRoomY = ry;
            var room = _world.Rooms[ry * World.RoomsX + rx];
            room.Visited = true;
            _banner = room.Name;
            _bannerTimer = 2.6f;
        }
    }

    private void UpdatePickups()
    {
        var body = _player.Bounds.Inflate(5);
        foreach (var p in _world.Pickups)
        {
            if (p.Taken || !body.Intersects(p.Bounds)) continue;
            p.Taken = true;
            switch (p.Kind)
            {
                case PickupKind.Fruit:
                    _world.FruitCount++;
                    _audio.Play("pickup", 0.8f);
                    _world.SpawnParticles(p.Pos, Palette.Pink, 8, 55f, 0.5f);
                    break;
                case PickupKind.Fragment:
                    _world.FragmentsCollected++;
                    _audio.Play("fragment", 0.9f);
                    _world.SpawnParticles(p.Pos, Palette.GreenLight, 20, 90f, 0.9f);
                    ShowMessage(_world.TreeComplete
                        ? "THE ENERTREE IS WHOLE - RETURN TO THE GREAT PLATFORM"
                        : $"ENERTREE FRAGMENT {_world.FragmentsCollected}/{World.FragmentTotal}", 3.2f);
                    break;
                case PickupKind.Energy:
                    _player.Energy = MathF.Min(Player.MaxEnergy, _player.Energy + 35);
                    _audio.Play("pickup", 0.7f);
                    _world.SpawnParticles(p.Pos, Palette.Danger, 8, 55f, 0.5f);
                    break;
            }
        }
    }

    private void UpdateInteractions(Controls c)
    {
        _context = "";
        Monsta? nearMonsta = null;
        float best = 58f * 58f;
        foreach (var m in _world.Monstas)
        {
            float d = Vector2.DistanceSquared(m.Bounds.Center, _player.Center);
            if (d < best) { best = d; nearMonsta = m; }
        }
        if (nearMonsta != null)
        {
            if (nearMonsta.Produced)
                _context = "DISPENSA-MONSTA (EMPTY)";
            else
                _context = $"E: FEED 3 DOODLEFOODLES  ({Math.Min(3, _world.FruitCount)}/3)";
            if (c.InteractPressed)
            {
                if (nearMonsta.Produced)
                    ShowMessage("ITS HUNGER IS SPENT", 2f);
                else if (_world.FruitCount < 3)
                    ShowMessage("IT HUNGERS FOR THREE DOODLEFOODLES", 2.4f);
                else
                {
                    _world.FruitCount -= 3;
                    nearMonsta.Fed = 3;
                    nearMonsta.Produced = true;
                    _world.Passes[nearMonsta.ColorA]++;
                    _world.Passes[nearMonsta.ColorB]++;
                    _audio.Play("feed");
                    _world.SpawnParticles(nearMonsta.Bounds.Center, Palette.PurpleLight, 26, 110f, 0.9f);
                    ShowMessage($"IT SPITS TWO PASSES: {Palette.RuneNames[nearMonsta.ColorA]} + {Palette.RuneNames[nearMonsta.ColorB]}", 3.6f);
                }
            }
        }

        var altar = _world.Altar;
        if (altar != null)
        {
            float d = Vector2.DistanceSquared(altar.Bounds.Center, _player.Center);
            if (d < 95f * 95f)
            {
                if (_world.TreeComplete)
                {
                    _context = "E: PLANT THE ENERTREE";
                    if (c.InteractPressed)
                    {
                        SubmitScore(true);
                        if (_mode != GameMode.NameEntry) { _mode = GameMode.Victory; _modeTime = 0; }
                        _audio.StopMusic();
                        _audio.Play("victory");
                        _audio.StartMusic(MusicTrack.GameOver);
                        _world.SpawnParticles(altar.Bounds.Center, Palette.GreenLight, 60, 170f, 1.4f);
                    }
                }
                else
                {
                    int missing = World.FragmentTotal - _world.FragmentsCollected;
                    _context = $"THE GREAT PLATFORM - {missing} FRAGMENTS MISSING";
                }
            }
        }
    }

    private void UpdateDoors()
    {
        foreach (var door in _world.Doors)
        {
            if (door.Open)
            {
                door.OpenAmount = MathF.Min(1f, door.OpenAmount + 0.08f);
                continue;
            }
            float d = Vector2.DistanceSquared(door.Bounds.Center, _player.Center);
            if (d < 70f * 70f && _world.Passes[door.ColorIndex] > 0 &&
                _player.Bounds.Inflate(10).Intersects(door.Bounds))
            {
                _world.Passes[door.ColorIndex]--;
                door.Open = true;
                _world.DoorsOpened++;
                _audio.Play("door");
                _world.SpawnParticles(door.Bounds.Center, Palette.DoorColors[door.ColorIndex], 26, 120f, 0.9f);
                ShowMessage($"{Palette.RuneNames[door.ColorIndex]} DOOR UNSEALED", 2.4f);
            }
            else if (d < 52f * 52f && _messageTimer <= 0 && _messageCooldown <= 0)
            {
                if (_world.Passes[door.ColorIndex] == 0)
                    ShowMessage($"A {Palette.RuneNames[door.ColorIndex]} PASS IS NEEDED", 2.2f);
                _messageCooldown = 2.5f;
            }
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var e in _world.Enemies)
        {
            if (!e.Alive) continue;
            e.Update(_world, _player.Center, _player.Energy, dt, p => _world.Projectiles.Add(p));
            if (e.Bounds.Intersects(_player.Bounds) &&
                _player.Damage(16f * _world.DamageScale, new Vector2(_player.Center.X < e.Pos.X ? -190 : 190, -120)))
            {
                _audio.Play("hurt");
                _renderer.DamageFlash = 1f;
                _shake = 1f;
                _world.SpawnParticles(_player.Center, Palette.Danger, 10, 90f, 0.5f);
            }
        }
        _world.Enemies.RemoveAll(e => !e.Alive);
    }

    private void UpdateProjectiles(float dt)
    {
        for (int i = _world.Projectiles.Count - 1; i >= 0; i--)
        {
            var p = _world.Projectiles[i];
            p.Life -= dt;
            p.Pos += p.Vel * dt;
            bool remove = p.Life <= 0;
            if (!remove && _world.HitsSolid(p.Bounds))
            {
                remove = true;
                _world.SpawnParticles(p.Pos, p.Color, 3, 30f, 0.2f);
            }
            if (!remove && p.FromPlayer)
            {
                foreach (var e in _world.Enemies)
                {
                    if (!e.Alive || !e.Bounds.Intersects(p.Bounds)) continue;
                    e.HP--;
                    remove = true;
                    _world.SpawnParticles(p.Pos, Palette.Gold, 6, 60f, 0.4f);
                    if (e.HP <= 0)
                    {
                        e.Alive = false;
                        _world.SpawnParticles(e.Bounds.Center, new Color(255, 150, 110), 14, 100f, 0.7f);
                        _audio.Play("hurt", 0.35f);
                    }
                    break;
                }
            }
            else if (!remove && !p.FromPlayer)
            {
                if (p.Bounds.Intersects(_player.Bounds) && _player.Damage(12f * _world.DamageScale, new Vector2(0, -90)))
                {
                    remove = true;
                    _audio.Play("hurt", 0.7f);
                    _renderer.DamageFlash = 0.8f;
                    _shake = 0.8f;
                }
            }
            if (remove) _world.Projectiles.RemoveAt(i);
        }
    }

    private void UpdateMonstas(float dt)
    {
        foreach (var m in _world.Monstas) m.Anim += dt;
    }

    private void UpdateCamera(float dt)
    {
        var target = _player.Center + new Vector2(_player.FacingRight ? 34 : -34, -8)
                     - new Vector2(Renderer.ViewW / 2f, Renderer.ViewH / 2f);
        float maxX = Math.Max(0, World.MapWidth - Renderer.ViewW);
        float maxY = Math.Max(0, World.MapHeight - Renderer.ViewH);
        target.X = Math.Clamp(target.X, 0, maxX);
        target.Y = Math.Clamp(target.Y, 0, maxY);
        _camera = Vector2.Lerp(_camera, target, 1f - MathF.Exp(-dt * 5.5f));
    }

    private void SnapCamera()
    {
        _camera = _player.Center - new Vector2(Renderer.ViewW / 2f, Renderer.ViewH / 2f);
        _camera.X = Math.Clamp(_camera.X, 0, Math.Max(0, World.MapWidth - Renderer.ViewW));
        _camera.Y = Math.Clamp(_camera.Y, 0, Math.Max(0, World.MapHeight - Renderer.ViewH));
    }

    private void ShowMessage(string text, float seconds)
    {
        _message = text;
        _messageTimer = seconds;
    }

    private void ToggleFullscreen()
    {
#if DESKTOP
        try
        {
            _fullscreen = !_fullscreen;
            _graphics.ToggleFullScreen();
        }
        catch { }
#else
        _fullscreen = false;
#endif
    }

    // ------------------------------------------------------------------ draw
    protected override void Draw(GameTime gameTime)
    {
        if (!_loggedFirstDraw) { Console.WriteLine("SQIJ: first Draw"); _loggedFirstDraw = true; }
        GraphicsDevice.SetRenderTarget(_scene);
        GraphicsDevice.Clear(new Color(6, 8, 16));
        _renderer.Bind(_spriteBatch);

        if (_mode == GameMode.Splash)
        {
            DrawSplash();
        }
        else if (_mode == GameMode.Editor)
        {
            // The editor drives its own batches (world pass then overlay pass).
            DrawEditor();
        }
        else
        {
            var shakeOffset = _shake > 0
                ? new Vector2((float)(Random.Shared.NextDouble() * 2 - 1) * 5f * _shake,
                    (float)(Random.Shared.NextDouble() * 2 - 1) * 4f * _shake)
                : Vector2.Zero;
            _renderer.Camera = _camera + shakeOffset;
            _renderer.Shake = _shake;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            _renderer.DrawBackground(_time, 0);
            _spriteBatch.End();

            _renderer.BeginWorld();
            _renderer.DrawTiles(_world, _time);
            _renderer.DrawProps(_world, _time);
            _renderer.DrawDoors(_world, _time);
            _renderer.DrawAltar(_world, _time);
            _renderer.DrawMonstas(_world, _time);
            _renderer.DrawPickups(_world, _time);
            _renderer.DrawEnemies(_world, _time);
            if (_mode is GameMode.Playing or GameMode.Paused) _renderer.DrawPlayer(_player, _time);
            _renderer.DrawProjectiles(_world);
            _renderer.DrawParticles(_world);
            _renderer.EndBatch();

            // Vignette/scanlines go under the UI so HUD and touch labels never get
            // swallowed by the dark edges of the screen.
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            _renderer.DrawOverlays();
            _spriteBatch.End();

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            // Only the playfield and pause overlay own the touch controls. Menu screens
            // must go back to plain taps so ConfirmPressed and Tap drive their options.
            _input.TouchControlsVisible = _mobile && _mode is GameMode.Playing or GameMode.Paused;
            if (_mode is GameMode.Playing or GameMode.Paused)
            {
                _input.DrawTouchOverlay(_spriteBatch, _art.White, _art.Glow, _art.Font);
                if (_mode == GameMode.Playing)
                {
                    float pulse = 0.5f + 0.5f * MathF.Sin(_time * 3f);
                    _renderer.DrawHud(_world, _player, _player.Center, _banner, Math.Clamp(_bannerTimer / 0.6f, 0, 1),
                        _messageTimer > 0 ? _message : "", Math.Clamp(_messageTimer / 0.4f, 0, 1), pulse);
                    if (_messageTimer <= 0 && !string.IsNullOrEmpty(_context))
                        DrawCentered(_context, Renderer.ViewW / 2f, Renderer.ViewH - 36, Palette.Paper * 0.9f);
                    if (_introTimer > 0 && _messageTimer <= 0 && string.IsNullOrEmpty(_context))
                        DrawCentered(_mobile
                                ? "STICK MOVES   FLAP / SPIT / USE BUTTONS   PAUSE TOP RIGHT"
                                : $"ARROWS/{_bindings.Name(GameAction.Left)}{_bindings.Name(GameAction.Right)} MOVE   {_bindings.Name(GameAction.Flap)}/UP FLAP   {_bindings.Name(GameAction.Spit)} SPIT   {_bindings.Name(GameAction.Use)} USE   ESC PAUSE",
                            Renderer.ViewW / 2f, Renderer.ViewH - 36,
                            Palette.Teal * Math.Clamp(_introTimer / 2f, 0, 1));
                }
            }

            switch (_mode)
            {
                case GameMode.Title: DrawTitle(); break;
                case GameMode.Help: DrawHelp(); break;
                case GameMode.Options: DrawOptions(); break;
                case GameMode.Controls: DrawControls(); break;
                case GameMode.Credits: DrawCredits(); break;
                case GameMode.HighScores: DrawHighScores(); break;
                case GameMode.Paused: DrawPaused(); break;
                case GameMode.GameOver: DrawGameOver(); break;
                case GameMode.Victory: DrawVictory(); break;
                case GameMode.NameEntry: DrawNameEntry(); break;
            }
            _spriteBatch.End();
        }

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        int bw = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int bh = GraphicsDevice.PresentationParameters.BackBufferHeight;
        // Keep MonoGame's touch scale in step with the real backbuffer. iOS does not
        // initialise these, and a mismatch shifts every on-screen control.
        if (TouchPanel.DisplayWidth != bw) TouchPanel.DisplayWidth = bw;
        if (TouchPanel.DisplayHeight != bh) TouchPanel.DisplayHeight = bh;
        float scale = MathF.Min(bw / (float)Renderer.ViewW, bh / (float)Renderer.ViewH);
        int dw = (int)(Renderer.ViewW * scale);
        int dh = (int)(Renderer.ViewH * scale);
        int dx = (bw - dw) / 2;
        int dy = (bh - dh) / 2;
        if (!_loggedBlit)
        {
            _loggedBlit = true;
            Console.WriteLine($"SQIJ: backbuffer={bw}x{bh} client={Window.ClientBounds.Width}x{Window.ClientBounds.Height} dest={dw}x{dh}+{dx}+{dy}");
        }
        _virtualScale = new Vector2(scale, scale);
        _virtualOffset = new Vector2(dx, dy);
        _input.ScreenSize = new Vector2(bw, bh);
        _input.VirtualScale = _virtualScale;
        _input.VirtualOffset = _virtualOffset;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _spriteBatch.Draw(_scene, new Rectangle(dx, dy, dw, dh), Color.White);
        _spriteBatch.End();

        if (Capturing) CaptureFrame((float)gameTime.ElapsedGameTime.TotalSeconds);

        base.Draw(gameTime);
    }

    private void DrawSplash()
    {
        float fade = Math.Clamp(_splashTime < 0.5f ? _splashTime / 0.5f : (2.6f - _splashTime) / 0.5f, 0f, 1f);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        // The card art already carries the publisher and credit lines, so no extra caption.
        _spriteBatch.Draw(_art.Splash, new Rectangle(140, 0, 360, 360), Color.White * fade);
        _spriteBatch.End();
    }

    private void DrawTitle()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 8, 14) * 0.42f);
        _spriteBatch.Draw(_art.Logo, new Rectangle(40, 10, 560, 176), Color.White);
        for (int i = 0; i < _titleItems.Length; i++)
        {
            bool sel = i == _menuIndex;
            float y = 190 + i * 20;
            if (sel) Rect(Renderer.ViewW / 2f - 120, y - 3, 240, 17, new Color(20, 60, 46) * 0.8f);
            DrawCentered(TitleLabel(_titleItems[i]), Renderer.ViewW / 2f, y,
                sel ? Palette.GreenLight : Palette.Paper * 0.8f, sel ? 1.5f : 1.25f);
        }
        if (_titleNoticeTimer > 0)
            DrawCentered(_titleNotice, Renderer.ViewW / 2f, 344, Palette.Danger * 0.9f);
    }

    private void DrawHelp()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 8, 14) * 0.92f);
        DrawCentered("HOW TO PLAY", Renderer.ViewW / 2f, 24, Palette.GreenLight, 2f);
        var rows = new List<(string Text, Color Tint)>
        {
            ("KIP MUST REBUILD THE ENERTREE.", Palette.Paper * 0.85f),
            ("", Palette.Paper),
            ("COLLECT 24 ENERTREE FRAGMENTS FROM THE VAULTS", Palette.Paper * 0.85f),
            ("SCATTERED ACROSS 256 ROOMS.", Palette.Paper * 0.85f),
            ("", Palette.Paper),
            ("FEED 3 DOODLEFOODLES TO EACH OF THE 12", Palette.Paper * 0.85f),
            ("DISPENSA-MONSTAS. EACH SPITS TWO RUNE PASSES.", Palette.Paper * 0.85f),
            ("MATCH A PASS TO A LASER DOOR TO UNSEAL IT.", Palette.Paper * 0.85f),
            ("DIFFICULTY SETS HAZARDS, CREATURES AND DAMAGE.", Palette.Paper * 0.85f),
            ("", Palette.Paper),
        };

        if (_mobile)
        {
            // Phones have no keyboard or gamepad, so only the touch layout is listed.
            rows.Add(("TOUCH THE LEFT HALF AND SLIDE TO RUN.", Palette.Teal * 0.95f));
            rows.Add(("PUSH IT UP, OR TAP FLAP, TO FLY.", Palette.Teal * 0.95f));
            rows.Add(("SPIT FIRES A SEED. USE FEEDS AND OPENS.", Palette.Teal * 0.95f));
            rows.Add(("PAUSE SITS IN THE TOP RIGHT CORNER.", Palette.Teal * 0.95f));
        }
        else
        {
            string l = _bindings.Name(GameAction.Left), r = _bindings.Name(GameAction.Right);
            string f = _bindings.Name(GameAction.Flap), s = _bindings.Name(GameAction.Spit);
            string u = _bindings.Name(GameAction.Use), m = _bindings.Name(GameAction.Map);
            string p = _bindings.Name(GameAction.Pause);
            rows.Add(($"ARROWS OR {l} {r}  MOVE     UP OR {f}  FLAP", Palette.Teal * 0.95f));
            rows.Add(($"{s}  SPIT                 {u}  USE / FEED", Palette.Teal * 0.95f));
            rows.Add(($"{m}  MAP                  {p}  PAUSE", Palette.Teal * 0.95f));
            rows.Add(("", Palette.Paper));
            rows.Add(("GAMEPAD: STICK MOVE, A FLAP, X SPIT, B USE.", Palette.Paper * 0.85f));
            rows.Add(("REBIND THE KEYS IN OPTIONS - CONTROLS.", Palette.Paper * 0.85f));
        }

        rows.Add(("", Palette.Paper));
        rows.Add(("RETURN TO THE GREAT PLATFORM WITH ALL 24", Palette.Paper * 0.85f));
        rows.Add(("FRAGMENTS TO PLANT THE TREE AND WIN.", Palette.Paper * 0.85f));
        rows.Add(("", Palette.Paper));
        rows.Add((_mobile ? "TAP TO RETURN" : "PRESS ENTER TO RETURN", Palette.Paper * 0.85f));

        float y = 56;
        foreach (var row in rows)
        {
            if (row.Text.Length > 0) DrawCentered(row.Text, Renderer.ViewW / 2f, y, row.Tint);
            y += 13;
        }
    }

    private void DrawOptions()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 8, 14) * 0.92f);
        DrawCentered("OPTIONS", Renderer.ViewW / 2f, 40, Palette.GreenLight, 2f);
        for (int i = 0; i < _optionLabels.Length; i++)
        {
            bool sel = i == _optionIndex;
            string value = _optionLabels[i] switch
            {
                "SOUND" => _audio.Enabled ? "ON" : "OFF",
                "SCANLINES" => _renderer.Scanlines ? "ON" : "OFF",
                "FULLSCREEN" => _fullscreen ? "ON" : "OFF",
                _ => "",
            };
            string label = _optionLabels[i] + (value.Length > 0 ? "  :  " + value : "");
            float y = 130 + i * 30;
            if (sel) Rect(Renderer.ViewW / 2f - 130, y - 5, 260, 22, new Color(20, 60, 46) * 0.85f);
            DrawCentered(label, Renderer.ViewW / 2f, y, sel ? Palette.GreenLight : Palette.Paper * 0.8f, 1.5f);
        }
        DrawCentered(_mobile ? "TAP TO CHANGE - TAP BACK TO RETURN" : "ENTER TO CHANGE - ESC TO RETURN",
            Renderer.ViewW / 2f, 300, Palette.Paper * 0.6f);
    }

    // ------------------------------------------------------------------ controls
    private static string ActionName(GameAction action) => action.ToString().ToUpperInvariant();

    private void DrawControls()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 8, 14) * 0.94f);
        DrawCentered("CONTROLS", Renderer.ViewW / 2f, 30, Palette.GreenLight, 2f);
        for (int i = 0; i < KeyBindings.All.Length; i++)
        {
            var action = KeyBindings.All[i];
            bool sel = i == _keyIndex;
            float y = 76 + i * 24;
            if (sel) Rect(Renderer.ViewW / 2f - 150, y - 4, 300, 18, new Color(20, 60, 46) * 0.85f);
            DrawCentered(ActionName(action) + "  :  " + _bindings.Name(action), Renderer.ViewW / 2f, y,
                sel ? Palette.GreenLight : Palette.Paper * 0.8f, 1.5f);
        }

        if (_rebindAction is { } rebinding)
            DrawCentered("PRESS A KEY FOR " + ActionName(rebinding) + " - ESC CANCELS",
                Renderer.ViewW / 2f, 268, Palette.Gold);
        else
            DrawCentered("ENTER TO REBIND - ESC TO RETURN", Renderer.ViewW / 2f, 268, Palette.Paper * 0.6f);
        DrawCentered("UP ARROW ALWAYS FLAPS. ESC ALWAYS PAUSES.", Renderer.ViewW / 2f, 296, Palette.Paper * 0.45f);
    }

    private void UpdateControls(Controls c)
    {
        if (_rebindAction is { } action)
        {
            var kb = Keyboard.GetState();
            foreach (var key in Enum.GetValues<Keys>())
            {
                if (key == Keys.None || key == Keys.Escape) continue;
                if (!kb.IsKeyDown(key) || _rebindPrevKeys.IsKeyDown(key)) continue;
                _bindings[action] = key;
                _bindings.Save();
                _rebindAction = null;
                _audio.Play("menu");
                break;
            }
            if (_rebindAction != null && c.CancelPressed) _rebindAction = null;
            _rebindPrevKeys = kb;
            return;
        }

        if (c.MenuUpPressed) { _keyIndex = (_keyIndex + KeyBindings.All.Length - 1) % KeyBindings.All.Length; _audio.Play("menu"); }
        if (c.MenuDownPressed) { _keyIndex = (_keyIndex + 1) % KeyBindings.All.Length; _audio.Play("menu"); }
        if (c.CancelPressed) { SetMode(GameMode.Options); return; }
        if (!c.ConfirmPressed) return;
        _audio.Play("menu");
        // Ignore the key that opened listening mode for this frame.
        _rebindPrevKeys = Keyboard.GetState();
        _rebindAction = KeyBindings.All[_keyIndex];
    }

    // ------------------------------------------------------------------ editor
    private void EnterEditor()
    {
        _editorWorld ??= LevelGenerator.Generate(1986);
        var start = _editorWorld.StartPosition;
        _editorCamera = new Vector2(
            Math.Clamp(start.X - Renderer.ViewW / 2f, 0, World.MapWidth - Renderer.ViewW),
            Math.Clamp(start.Y - Renderer.ViewH / 2f, 0, World.MapHeight - Renderer.ViewH));
        _editorHasPrevInput = false;
        SetEditorStatus("LEVEL EDITOR");
        SetMode(GameMode.Editor);
    }

    private void SetEditorStatus(string text)
    {
        _editorStatus = text;
        _editorStatusTimer = 2.5f;
    }

    private void UpdateEditor(float dt)
    {
        var world = _editorWorld;
        if (world == null) { SetMode(GameMode.Title); return; }

        var kb = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (!_editorHasPrevInput)
        {
            _editorPrevKeys = kb;
            _editorPrevMouse = mouse;
            _editorHasPrevInput = true;
        }

        bool Pressed(Keys k) => kb.IsKeyDown(k) && !_editorPrevKeys.IsKeyDown(k);

        var pan = Vector2.Zero;
        if (kb.IsKeyDown(Keys.Left) || kb.IsKeyDown(Keys.A)) pan.X -= 1;
        if (kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D)) pan.X += 1;
        if (kb.IsKeyDown(Keys.Up) || kb.IsKeyDown(Keys.W)) pan.Y -= 1;
        if (kb.IsKeyDown(Keys.Down)) pan.Y += 1;
        _editorCamera += pan * 640f * dt;
        _editorCamera.X = Math.Clamp(_editorCamera.X, 0, World.MapWidth - Renderer.ViewW);
        _editorCamera.Y = Math.Clamp(_editorCamera.Y, 0, World.MapHeight - Renderer.ViewH);

        var virtualMouse = (new Vector2(mouse.X, mouse.Y) - _virtualOffset) / _virtualScale;
        int tx = Math.Clamp((int)MathF.Floor((virtualMouse.X + _editorCamera.X) / World.TileSize), 0, World.MapTilesX - 1);
        int ty = Math.Clamp((int)MathF.Floor((virtualMouse.Y + _editorCamera.Y) / World.TileSize), 0, World.MapTilesY - 1);
        _editorCursor = new Vector2(tx, ty);

        if (mouse.LeftButton == ButtonState.Pressed)
        {
            int cell = PaletteCellAt(virtualMouse);
            if (cell >= 0) _editorBrush = cell;
            else ApplyBrush(world, tx, ty);
        }
        else if (mouse.RightButton == ButtonState.Pressed)
        {
            EraseAt(world, tx, ty);
        }

        int wheel = mouse.ScrollWheelValue - _editorPrevMouse.ScrollWheelValue;
        if (wheel != 0) _editorColor = (_editorColor + Math.Sign(wheel) + 6) % 6;

        for (int i = 0; i < BrushList.Length && i < 9; i++)
            if (Pressed(Keys.D1 + i)) _editorBrush = i;
        if (Pressed(Keys.G)) _editorGrid = !_editorGrid;

        if (Pressed(Keys.P))
        {
            if (world.IsSolid(tx, ty))
            {
                SetEditorStatus("CANNOT PLACE START IN ROCK");
            }
            else
            {
                world.StartPosition = new Vector2(tx * World.TileSize + 2, (ty + 1) * World.TileSize - 20);
                var (rx, ry) = world.RoomAt(world.StartPosition);
                world.StartRoomX = rx;
                world.StartRoomY = ry;
                SetEditorStatus("START MOVED");
            }
        }

        if (Pressed(Keys.F5)) SaveEditorLevel(world);
        if (Pressed(Keys.F9)) LoadEditorLevel();
        if (Pressed(Keys.T))
        {
            var (_, message) = LevelFile.Validate(world);
            SetEditorStatus("TEST " + message);
        }
        if (Pressed(Keys.Enter)) { BeginRun(world); return; }
        if (Pressed(Keys.Escape)) { SetMode(GameMode.Title); return; }

        _editorPrevKeys = kb;
        _editorPrevMouse = mouse;
    }

    private static int PaletteCellAt(Vector2 p)
    {
        const float x0 = 10, y0 = 9;
        for (int i = 0; i < BrushList.Length; i++)
        {
            float x = x0 + i % PaletteCols * PaletteCellW;
            float y = y0 + i / PaletteCols * PaletteCellH;
            if (p.X >= x && p.X < x + PaletteCellW - 4 && p.Y >= y && p.Y < y + PaletteCellH - 4)
                return i;
        }
        return -1;
    }

    private void ApplyBrush(World world, int tx, int ty)
    {
        var centre = new Vector2(tx * World.TileSize + 8, ty * World.TileSize + 8);
        var topLeft = new Vector2(tx * World.TileSize, ty * World.TileSize);
        switch (BrushList[_editorBrush].Brush)
        {
            case EditorBrush.Empty:
                EraseAt(world, tx, ty);
                break;
            case EditorBrush.Rock: world.SetTile(tx, ty, TileKind.Rock); break;
            case EditorBrush.Ledge: world.SetTile(tx, ty, TileKind.Ledge); break;
            case EditorBrush.Lava: world.SetTile(tx, ty, TileKind.Lava); break;
            case EditorBrush.Spikes: world.SetTile(tx, ty, TileKind.Spikes); break;
            case EditorBrush.Fruit: AddPickup(world, centre, PickupKind.Fruit, _editorColor); break;
            case EditorBrush.Fragment: AddPickup(world, centre, PickupKind.Fragment, _editorColor); break;
            case EditorBrush.Pass: AddPickup(world, centre, PickupKind.Pass, _editorColor); break;
            case EditorBrush.Monsta:
                if (!world.Monstas.Any(m => Vector2.DistanceSquared(m.Pos + new Vector2(16, 16), centre) < 24 * 24))
                    world.Monstas.Add(new Monsta
                    {
                        Pos = topLeft,
                        ColorA = _editorColor,
                        ColorB = (_editorColor + 3) % 6,
                    });
                break;
            case EditorBrush.Door:
                if (!world.Doors.Any(d => d.Bounds.Inflate(4).Contains(centre)))
                    world.Doors.Add(new Door
                    {
                        Bounds = new RectF(topLeft.X, topLeft.Y, 2 * World.TileSize, 3 * World.TileSize),
                        ColorIndex = _editorColor,
                        Rune = _editorColor,
                    });
                break;
            case EditorBrush.Torch: AddProp(world, topLeft, PropKind.Torch); break;
            case EditorBrush.Crystal: AddProp(world, topLeft, PropKind.Crystal); break;
            case EditorBrush.Mushroom: AddProp(world, topLeft, PropKind.Mushroom); break;
            case EditorBrush.Altar:
                world.Altar = new Altar { Bounds = new RectF(topLeft.X, topLeft.Y, 96, 64) };
                break;
        }
    }

    private static void AddPickup(World world, Vector2 at, PickupKind kind, int colour)
    {
        if (world.Pickups.Any(p => p.Kind == kind && Vector2.DistanceSquared(p.Pos, at) < 10 * 10)) return;
        world.Pickups.Add(new Pickup { Kind = kind, Pos = at, ColorIndex = colour });
    }

    private static void AddProp(World world, Vector2 at, PropKind kind)
    {
        if (world.Props.Any(p => p.Kind == kind && Vector2.DistanceSquared(p.Pos, at) < 10 * 10)) return;
        world.Props.Add(new Prop { Kind = kind, Pos = at });
    }

    /// <summary>Right click: clear whatever object sits in the tile, and the tile itself.</summary>
    private static void EraseAt(World world, int tx, int ty)
    {
        var centre = new Vector2(tx * World.TileSize + 8, ty * World.TileSize + 8);
        world.Pickups.RemoveAll(p => Vector2.DistanceSquared(p.Pos, centre) < 14 * 14);
        world.Monstas.RemoveAll(m => Vector2.DistanceSquared(m.Pos + new Vector2(16, 16), centre) < 24 * 24);
        world.Doors.RemoveAll(d => d.Bounds.Contains(centre));
        world.Props.RemoveAll(p => Vector2.DistanceSquared(p.Pos + new Vector2(8, 8), centre) < 18 * 18);
        world.SetTile(tx, ty, TileKind.Empty);
    }

    private void SaveEditorLevel(World world)
    {
        try
        {
            LevelFile.Save(world, LevelFile.DefaultPath);
            SetEditorStatus("SAVED " + Path.GetFileName(LevelFile.DefaultPath));
        }
        catch (Exception ex)
        {
            SetEditorStatus("SAVE FAILED: " + ex.Message);
        }
    }

    /// <summary>Title screen: launch whatever the editor last saved to disk.</summary>
    private void PlaySavedLevel()
    {
        try
        {
            var level = LevelFile.Load(LevelFile.DefaultPath);
            _editorWorld = level;
            BeginRun(level);
        }
        catch (FileNotFoundException)
        {
            _titleNotice = "NO SAVED LEVEL - MAKE ONE IN THE EDITOR";
            _titleNoticeTimer = 4f;
        }
        catch (Exception)
        {
            _titleNotice = "SAVED LEVEL COULD NOT BE LOADED";
            _titleNoticeTimer = 4f;
        }
    }

    private void LoadEditorLevel()
    {
        try
        {
            var loaded = LevelFile.Load(LevelFile.DefaultPath);
            _editorWorld = loaded;
            _editorCamera.X = Math.Clamp(_editorCamera.X, 0, World.MapWidth - Renderer.ViewW);
            _editorCamera.Y = Math.Clamp(_editorCamera.Y, 0, World.MapHeight - Renderer.ViewH);
            SetEditorStatus("LOADED " + Path.GetFileName(LevelFile.DefaultPath));
        }
        catch (Exception ex)
        {
            SetEditorStatus("LOAD FAILED: " + ex.Message);
        }
    }

    private void DrawEditor()
    {
        var world = _editorWorld;
        if (world == null) return;

        _renderer.Camera = _editorCamera;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _renderer.DrawBackground(_time, 0);
        _spriteBatch.End();

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null,
            Matrix.CreateTranslation(-MathF.Round(_editorCamera.X), -MathF.Round(_editorCamera.Y), 0f));
        _renderer.DrawTiles(world, _time);
        _renderer.DrawProps(world, _time);
        _renderer.DrawDoors(world, _time);
        _renderer.DrawAltar(world, _time);
        _renderer.DrawMonstas(world, _time);
        _renderer.DrawPickups(world, _time);
        _renderer.DrawEnemies(world, _time);
        if (_editorGrid) DrawEditorGrid();

        Rect(world.StartPosition.X - 3, world.StartPosition.Y - 3, 10, 10, Palette.GreenLight * 0.9f);
        float cx = _editorCursor.X * World.TileSize, cy = _editorCursor.Y * World.TileSize;
        Rect(cx, cy, World.TileSize, 1, Palette.Gold);
        Rect(cx, cy + World.TileSize - 1, World.TileSize, 1, Palette.Gold);
        Rect(cx, cy, 1, World.TileSize, Palette.Gold);
        Rect(cx + World.TileSize - 1, cy, 1, World.TileSize, Palette.Gold);
        _spriteBatch.End();

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
        _renderer.DrawOverlays();

        // brush palette: two rows - terrain on top, objects underneath
        Rect(6, 6, PaletteCols * PaletteCellW + 8, PaletteCellH * 2 + 6, Palette.Ink * 0.82f);
        for (int i = 0; i < BrushList.Length; i++)
        {
            bool sel = i == _editorBrush;
            float x = 10 + i % PaletteCols * PaletteCellW;
            float y = 9 + i / PaletteCols * PaletteCellH;
            if (sel) Rect(x, y, PaletteCellW - 4, PaletteCellH - 4, new Color(20, 60, 46) * 0.95f);
            Rect(x + 3, y + 4, 8, 8, BrushList[i].Tint);
            PixelFont.Draw(_spriteBatch, _art.Font, BrushList[i].Name, new Vector2(x + 15, y + 3),
                sel ? Palette.GreenLight : Palette.Paper * 0.75f);
        }
        var colour = Palette.DoorColors[_editorColor];
        Rect(6, PaletteCellH * 2 + 14, 150, 14, Palette.Ink * 0.82f);
        Rect(10, PaletteCellH * 2 + 17, 8, 8, colour);
        PixelFont.Draw(_spriteBatch, _art.Font, "WHEEL: COLOUR", new Vector2(22, PaletteCellH * 2 + 16),
            Palette.Paper * 0.8f);

        int roomX = (int)(_editorCursor.X * World.TileSize) / (World.RoomTilesX * World.TileSize);
        int roomY = (int)(_editorCursor.Y * World.TileSize) / (World.RoomTilesY * World.TileSize);
        PixelFont.Draw(_spriteBatch, _art.Font,
            $"ROOM {roomX + 1}-{roomY + 1}   TILE {(int)_editorCursor.X},{(int)_editorCursor.Y}",
            new Vector2(12, 84), Palette.Teal * 0.9f);

        if (_editorStatusTimer > 0)
            PixelFont.DrawCentered(_spriteBatch, _art.Font, _editorStatus, Renderer.ViewW / 2f, Renderer.ViewH - 46,
                Palette.Gold, 1f, 1f, true);
        PixelFont.DrawCentered(_spriteBatch, _art.Font,
            "LEFT PAINT   RIGHT ERASE   ARROWS PAN   1-9 BRUSH   P START   G GRID",
            Renderer.ViewW / 2f, Renderer.ViewH - 30, Palette.Paper * 0.7f, 1f, 1f, true);
        PixelFont.DrawCentered(_spriteBatch, _art.Font,
            "F5 SAVE   F9 LOAD   T TEST   ENTER PLAY   ESC BACK",
            Renderer.ViewW / 2f, Renderer.ViewH - 18, Palette.Paper * 0.7f, 1f, 1f, true);
        _spriteBatch.End();
    }

    private void DrawEditorGrid()
    {
        var line = new Color(120, 200, 180) * 0.12f;
        int x0 = (int)(_editorCamera.X / World.TileSize);
        int x1 = (int)((_editorCamera.X + Renderer.ViewW) / World.TileSize) + 1;
        int y0 = (int)(_editorCamera.Y / World.TileSize);
        int y1 = (int)((_editorCamera.Y + Renderer.ViewH) / World.TileSize) + 1;
        for (int tx = x0; tx <= x1; tx++) Rect(tx * World.TileSize, _editorCamera.Y, 1, Renderer.ViewH, line);
        for (int ty = y0; ty <= y1; ty++) Rect(_editorCamera.X, ty * World.TileSize, Renderer.ViewW, 1, line);
    }

    private void DrawCredits()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 8, 14) * 0.92f);
        DrawCentered("CREDITS", Renderer.ViewW / 2f, 34, Palette.GreenLight, 2f);

        DrawCentered("DESIGN, CODE AND PIXELS", Renderer.ViewW / 2f, 92, Palette.Teal);
        DrawCentered("PAUL F. JOHNSON", Renderer.ViewW / 2f, 110, Palette.Gold * 0.9f, 1.5f);
        DrawCentered("2026", Renderer.ViewW / 2f, 134, Palette.Paper * 0.85f);

        DrawCentered("BUILT WITH", Renderer.ViewW / 2f, 184, Palette.Teal);
        DrawCentered("MONOGAME", Renderer.ViewW / 2f, 202, Palette.Gold * 0.9f, 1.5f);
        DrawCentered("ALL SOUND SYNTHESISED IN CODE", Renderer.ViewW / 2f, 226, Palette.Paper * 0.85f);

        DrawCentered("PRESS ENTER TO RETURN", Renderer.ViewW / 2f, 300, Palette.Paper * 0.6f);
    }

    private void DrawPaused()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(3, 6, 12) * 0.78f);
        DrawCentered("PAUSED", Renderer.ViewW / 2f, 80, Palette.GreenLight, 3f);
        for (int i = 0; i < PauseItems.Length; i++)
        {
            bool sel = i == _pauseIndex;
            float y = 170 + i * 28;
            if (sel) Rect(Renderer.ViewW / 2f - 100, y - 5, 200, 20, new Color(20, 60, 46) * 0.85f);
            DrawCentered(PauseItems[i], Renderer.ViewW / 2f, y, sel ? Palette.GreenLight : Palette.Paper * 0.8f, 1.5f);
        }
        DrawCentered(_mobile ? "TAP AN OPTION TO CONTINUE" : "ESC TO RESUME",
            Renderer.ViewW / 2f, 300, Palette.Teal * 0.7f);
    }

    private void DrawGameOver()
    {
        float a = Math.Clamp(_modeTime / 0.6f, 0, 1);
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(20, 4, 8) * (0.7f * a));
        DrawCentered("KIP IS SPENT", Renderer.ViewW / 2f, 110, Palette.Danger * a, 3f);
        DrawCentered($"FRAGMENTS {_world.FragmentsCollected}/{World.FragmentTotal}   PASSES USED {_world.DoorsOpened}",
            Renderer.ViewW / 2f, 170, Palette.Paper * (0.85f * a));
        DrawCentered($"TIME {FormatTime(_runTime)}", Renderer.ViewW / 2f, 190, Palette.Teal * a);
        DrawCentered(ScoreLine(), Renderer.ViewW / 2f, 212, Palette.Gold * a);
        if (_modeTime > 0.8f)
        {
            float blink = 0.55f + 0.45f * MathF.Sin(_time * 5f);
            DrawCentered(_mobile ? "TAP TO TRY AGAIN" : "ENTER TO TRY AGAIN   ESC FOR TITLE",
                Renderer.ViewW / 2f, 250, Palette.Paper * (blink * a));
        }
    }

    private void DrawVictory()
    {
        float a = Math.Clamp(_modeTime / 0.8f, 0, 1);
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 18, 12) * (0.78f * a));
        DrawCentered("THE ENERTREE STANDS", Renderer.ViewW / 2f, 70, Palette.GreenLight * a, 3f);
        DrawCentered("FOREVERMORE", Renderer.ViewW / 2f, 108, Palette.GreenLight * a, 3f);
        DrawCentered($"YOU HAVE REBUILT IT FROM {World.FragmentTotal} FRAGMENTS", Renderer.ViewW / 2f, 160, Palette.Paper * (0.9f * a));
        DrawCentered($"RUN TIME {FormatTime(_runTime)}", Renderer.ViewW / 2f, 186, Palette.Teal * a);
        DrawCentered($"PASSES USED {_world.DoorsOpened}   FRUIT FED {World.FruitTotal - _world.FruitCount}",
            Renderer.ViewW / 2f, 200, Palette.Paper * (0.6f * a));
        DrawCentered(ScoreLine(), Renderer.ViewW / 2f, 218, Palette.Gold * a);
        DrawCentered("THE CAVES OF PYLPAPA ARE AT PEACE", Renderer.ViewW / 2f, 250, Palette.Teal * (0.65f * a));
        if (_modeTime > 1f)
            DrawCentered(_mobile ? "TAP TO CONTINUE" : "PRESS ENTER", Renderer.ViewW / 2f, 300,
                Palette.Paper * ((0.55f + 0.45f * MathF.Sin(_time * 4f)) * a));
    }

    private void SubmitScore(bool victory)
    {
        if (_scoreSubmitted) return;
        _lastScore = HighScores.ScoreFor(_world, _runTime, victory);
        _pendingVictory = victory;
        _pendingEndMode = victory ? GameMode.Victory : GameMode.GameOver;
        if (_lastScore <= 0)
        {
            // A run with nothing to show is not worth a table entry or a name prompt.
            _scoreSubmitted = true;
            return;
        }
        if (HighScores.WouldQualify(_lastScore))
        {
            _nameChars.Clear();
            _nameCursor = 0;
            _namePrevKeys = Keyboard.GetState();
            SetMode(GameMode.NameEntry);
            return;
        }
        CommitScore(HighScores.DefaultName);
    }

    private void CommitScore(string name)
    {
        if (_scoreSubmitted) { SetMode(_pendingEndMode); return; }
        _scoreSubmitted = true;
        HighScores.PlayerName = name;
        _lastRank = HighScores.Submit(new HighScoreEntry
        {
            Name = name,
            Score = _lastScore,
            Fragments = _world.FragmentsCollected,
            Doors = _world.DoorsOpened,
            Fed = World.FruitTotal - _world.FruitCount,
            Difficulty = _world.Difficulty,
            Seconds = _runTime,
            Victory = _pendingVictory,
            When = DateTime.Now,
        });
        _highScoreTable = HighScores.Load();
        SetMode(_pendingEndMode);
    }

    // ------------------------------------------------------------------ name entry
    private static Rectangle NameCellRect(int index) => new(
        NameGridX + index % NameCols * NameCellW,
        NameGridY + index / NameCols * NameCellH,
        NameCellW - 2, NameCellH - 2);

    private static string NameCellLabel(int index) => index < NameLetters.Length
        ? NameLetters[index].ToString()
        : index == NameLetters.Length ? "DEL" : "OK";

    private static int NameCellAt(Vector2 p)
    {
        for (int i = 0; i < NameLetters.Length + 2; i++)
            if (NameCellRect(i).Contains(p)) return i;
        return -1;
    }

    private void MoveNameCursor(int dx, int dy)
    {
        int rows = (NameLetters.Length + 1 + NameCols) / NameCols;
        int row = Math.Clamp(_nameCursor / NameCols, 0, rows - 1);
        int col = _nameCursor % NameCols;
        int lastCol = row == rows - 1 ? 1 : NameCols - 1;
        if (dy != 0)
        {
            row = (row + dy + rows) % rows;
            lastCol = row == rows - 1 ? 1 : NameCols - 1;
            if (col > lastCol) col = lastCol;
        }
        if (dx != 0) col = (col + dx + lastCol + 1) % (lastCol + 1);
        _nameCursor = row * NameCols + col;
    }

    private void ApplyNameCell(int cell)
    {
        if (cell < 0) return;
        if (cell < NameLetters.Length)
        {
            if (_nameChars.Count >= NameMaxLength) { _audio.Play("menu", 0.4f); return; }
            _nameChars.Add(NameLetters[cell]);
        }
        else if (cell == NameLetters.Length)
        {
            if (_nameChars.Count == 0) return;
            _nameChars.RemoveAt(_nameChars.Count - 1);
        }
        else
        {
            CommitScore(_nameChars.Count > 0 ? new string(_nameChars.ToArray()) : HighScores.DefaultName);
            return;
        }
        _audio.Play("menu");
    }

    private void UpdateNameEntry(Controls c)
    {
        int typed = -1;
        if (c.MenuUpPressed) MoveNameCursor(0, -1);
        if (c.MenuDownPressed) MoveNameCursor(0, 1);
        if (c.MenuLeftPressed) MoveNameCursor(-1, 0);
        if (c.MenuRightPressed) MoveNameCursor(1, 0);

        var kb = Keyboard.GetState();
        bool back = (kb.IsKeyDown(Keys.Back) && !_namePrevKeys.IsKeyDown(Keys.Back)) ||
                    (kb.IsKeyDown(Keys.Delete) && !_namePrevKeys.IsKeyDown(Keys.Delete));

        // Typing a letter jumps straight to it, like the original's arcade entry.
        for (int i = 0; i < NameLetters.Length; i++)
        {
            var key = i < 26 ? Keys.A + i : Keys.D0 + (i - 26);
            if (kb.IsKeyDown(key) && !_namePrevKeys.IsKeyDown(key)) { typed = i; break; }
        }
        _namePrevKeys = kb;

        if (c.Tap is { } tap)
        {
            int cell = NameCellAt(tap);
            if (cell >= 0) { _nameCursor = cell; ApplyNameCell(cell); return; }
        }
        if (back)
        {
            if (_nameChars.Count > 0) { _nameChars.RemoveAt(_nameChars.Count - 1); _audio.Play("menu"); }
            return;
        }
        if (typed >= 0) { ApplyNameCell(typed); return; }
        if (c.ConfirmPressed) { ApplyNameCell(_nameCursor); return; }
        if (c.CancelPressed)
            CommitScore(_nameChars.Count > 0 ? new string(_nameChars.ToArray()) : HighScores.DefaultName);
    }

    private string ScoreLine()
    {
        string line = $"SCORE {_lastScore}   {_world.Difficulty.Name()} X{HighScores.Multiplier(_world.Difficulty)}";
        if (_lastRank > 0) line += $"   NEW HIGH SCORE #{_lastRank}";
        return line;
    }

    private void DrawNameEntry()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 8, 14) * 0.93f);
        DrawCentered("NEW HIGH SCORE", Renderer.ViewW / 2f, 30, Palette.Gold, 2f);
        DrawCentered($"{_lastScore}   {_world.Difficulty.Name()} X{HighScores.Multiplier(_world.Difficulty)}",
            Renderer.ViewW / 2f, 66, Palette.Paper * 0.85f);

        const int slotW = 34, slotH = 26, gap = 5;
        float total = NameMaxLength * slotW + (NameMaxLength - 1) * gap;
        float x0 = Renderer.ViewW / 2f - total / 2f;
        for (int i = 0; i < NameMaxLength; i++)
        {
            float x = x0 + i * (slotW + gap);
            bool active = i == _nameChars.Count;
            Rect(x, 96, slotW, slotH, active ? new Color(20, 60, 46) * 0.95f : new Color(10, 18, 28) * 0.85f);
            var rim = active ? Palette.GreenLight * 0.95f : Palette.Teal * 0.35f;
            Rect(x, 96, slotW, 1, rim);
            Rect(x, 96 + slotH - 1, slotW, 1, rim);
            Rect(x, 96, 1, slotH, rim);
            Rect(x + slotW - 1, 96, 1, slotH, rim);
            bool filled = i < _nameChars.Count;
            DrawCentered(filled ? _nameChars[i].ToString() : "_", x + slotW / 2f, 101,
                filled ? Palette.Gold : Palette.Paper * 0.3f, 2f);
        }

        for (int i = 0; i < NameLetters.Length + 2; i++)
        {
            var cell = NameCellRect(i);
            bool sel = i == _nameCursor;
            Rect(cell.X, cell.Y, cell.Width, cell.Height,
                sel ? new Color(20, 60, 46) * 0.95f : new Color(10, 18, 28) * 0.7f);
            if (sel)
            {
                Rect(cell.X, cell.Y, cell.Width, 1, Palette.GreenLight);
                Rect(cell.X, cell.Y + cell.Height - 1, cell.Width, 1, Palette.GreenLight);
                Rect(cell.X, cell.Y, 1, cell.Height, Palette.GreenLight);
                Rect(cell.X + cell.Width - 1, cell.Y, 1, cell.Height, Palette.GreenLight);
            }
            var tint = i < NameLetters.Length
                ? (sel ? Palette.Gold : Palette.Paper * 0.8f)
                : (sel ? Palette.Gold : Palette.Teal * 0.9f);
            DrawCentered(NameCellLabel(i), cell.X + cell.Width / 2f, cell.Y + 4, tint);
        }

        DrawCentered(_mobile
                ? "TAP A LETTER - DEL ERASES - OK SAVES"
                : "TYPE OR ARROWS + ENTER - BACKSPACE ERASES",
            Renderer.ViewW / 2f, 294, Palette.Paper * 0.6f);
        DrawCentered(_mobile ? "TAP OK TO SAVE YOUR SCORE" : "ESC SAVES WITH " + HighScores.DefaultName.ToUpperInvariant(),
            Renderer.ViewW / 2f, 310, Palette.Teal * 0.55f);
    }

    private void DrawHighScores()
    {
        Rect(0, 0, Renderer.ViewW, Renderer.ViewH, new Color(4, 8, 14) * 0.94f);
        DrawCentered("HIGH SCORES", Renderer.ViewW / 2f, 26, Palette.GreenLight, 2f);

        if (_highScoreTable.Count == 0)
        {
            DrawCentered("NO SCORES YET - DESCEND AND PLANT THE TREE", Renderer.ViewW / 2f, 160,
                Palette.Paper * 0.8f);
        }
        else
        {
            DrawText("#", 126, 66, Palette.Teal * 0.9f);
            DrawText("NAME", 148, 66, Palette.Teal * 0.9f);
            DrawText("SCORE", 234, 66, Palette.Teal * 0.9f);
            DrawText("LEVEL", 322, 66, Palette.Teal * 0.9f);
            DrawText("X", 384, 66, Palette.Teal * 0.9f);
            DrawText("FRAG", 420, 66, Palette.Teal * 0.9f);
            DrawText("TIME", 476, 66, Palette.Teal * 0.9f);
            for (int i = 0; i < _highScoreTable.Count; i++)
            {
                var e = _highScoreTable[i];
                float y = 84 + i * 14;
                bool newest = _scoreSubmitted && i + 1 == _lastRank;
                var color = newest ? Palette.Gold : Palette.Paper * 0.85f;
                if (newest) Rect(120, y - 4, 402, 13, new Color(20, 60, 46) * 0.75f);
                DrawText($"{i + 1}", 126, y, color);
                DrawText(NameCell(e.Name), 148, y, color);
                DrawText($"{e.Score}", 234, y, color);
                DrawText(e.Difficulty.Name(), 322, y, color);
                DrawText($"X{HighScores.Multiplier(e.Difficulty)}", 384, y, color);
                DrawText($"{e.Fragments}/{World.FragmentTotal}", 420, y, color);
                DrawText(FormatTime(e.Seconds), 476, y, color);
            }
        }

        DrawCentered(_mobile ? "TAP TO RETURN" : "PRESS ENTER TO RETURN",
            Renderer.ViewW / 2f, 300, Palette.Paper * 0.6f);
    }

    private static string NameCell(string name)
    {
        string trimmed = string.IsNullOrWhiteSpace(name) ? HighScores.DefaultName : name.Trim().ToUpperInvariant();
        return trimmed.Length <= 8 ? trimmed : trimmed[..8];
    }

    private static string FormatTime(float seconds)
    {
        int s = Math.Min(5999, (int)seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }

    private void DrawText(string text, float x, float y, Color color, float scale = 1f)
        => PixelFont.Draw(_spriteBatch, _art.Font, text, new Vector2(x, y), color, scale, 1f, true);

    private void DrawCentered(string text, float cx, float y, Color color, float scale = 1f)
        => PixelFont.DrawCentered(_spriteBatch, _art.Font, text, cx, y, color, scale, 1f, true);

    private void Rect(float x, float y, float w, float h, Color color)
        => _spriteBatch.Draw(_art.White, new Rectangle((int)x, (int)y, (int)w, (int)h), color);

    protected override void UnloadContent()
    {
        _audio.Dispose();
        _art.Dispose();
        _scene.Dispose();
        base.UnloadContent();
    }
}

