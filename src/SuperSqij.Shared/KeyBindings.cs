using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Input;

namespace SuperSqij;

/// <summary>The gameplay actions a desktop player can bind to a key.</summary>
public enum GameAction { Left, Right, Flap, Spit, Use, Map, Pause }

/// <summary>
/// Desktop keyboard map. Escape is reserved for pause/cancel and never binds to an
/// action, but pressing it still pauses regardless of the Pause binding.
/// </summary>
public sealed class KeyBindings
{
    public static readonly GameAction[] All =
    {
        GameAction.Left, GameAction.Right, GameAction.Flap, GameAction.Spit,
        GameAction.Use, GameAction.Map, GameAction.Pause,
    };

    private static readonly Keys[] Defaults =
    {
        Keys.Z, Keys.X, Keys.M, Keys.Space, Keys.E, Keys.N, Keys.Escape,
    };

    private readonly Dictionary<GameAction, Keys> _map = new();

    public KeyBindings() => Reset();

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SuperSqij", "keys.txt");

    public Keys this[GameAction action]
    {
        get => _map.TryGetValue(action, out var key) ? key : Keys.None;
        set
        {
            // Escape stays a menu key, so it can never be stolen by an action.
            if (value == Keys.Escape) return;
            foreach (var other in All)
                if (other != action && _map[other] == value) _map[other] = Keys.None;
            _map[action] = value;
        }
    }

    public string Name(GameAction action) => Label(this[action]);

    public static string Label(Keys key) => key switch
    {
        Keys.None => "NONE",
        Keys.Space => "SPACE",
        Keys.Escape => "ESC",
        Keys.Enter => "ENTER",
        Keys.Back => "BACKSPACE",
        Keys.OemComma => ",",
        Keys.OemPeriod => ".",
        Keys.OemPlus => "PLUS",
        Keys.OemMinus => "MINUS",
        Keys.LeftShift or Keys.RightShift => "SHIFT",
        Keys.LeftControl or Keys.RightControl => "CTRL",
        Keys.LeftAlt or Keys.RightAlt => "ALT",
        _ => key.ToString().ToUpperInvariant(),
    };

    public void Reset()
    {
        _map.Clear();
        for (int i = 0; i < All.Length; i++) _map[All[i]] = Defaults[i];
    }

    public bool IsDown(KeyboardState kb, GameAction action)
    {
        var key = this[action];
        return key != Keys.None && kb.IsKeyDown(key);
    }

    public static KeyBindings Load()
    {
        var bindings = new KeyBindings();
        try
        {
            if (!File.Exists(DefaultPath)) return bindings;
            foreach (var line in File.ReadAllLines(DefaultPath))
            {
                int split = line.IndexOf('=');
                if (split <= 0) continue;
                if (!Enum.TryParse<GameAction>(line[..split].Trim(), true, out var action)) continue;
                if (!Enum.TryParse<Keys>(line[(split + 1)..].Trim(), true, out var key)) continue;
                bindings[action] = key;
            }
        }
        catch { /* fall back to the defaults */ }
        return bindings;
    }

    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(DefaultPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var sb = new StringBuilder();
            foreach (var action in All)
                sb.AppendLine(action.ToString().ToLowerInvariant() + "=" + this[action]);
            File.WriteAllText(DefaultPath, sb.ToString());
        }
        catch { /* a read-only profile still plays with the in-memory map */ }
    }
}
