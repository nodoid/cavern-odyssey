using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace SuperSqij;

public struct Controls
{
    public float MoveX;
    public bool JumpPressed;
    public bool JumpHeld;
    public bool FirePressed;
    public bool FireHeld;
    public bool InteractPressed;
    public bool PausePressed;
    public bool MapPressed;
    public bool MenuUpPressed;
    public bool MenuDownPressed;
    public bool MenuLeftPressed;
    public bool MenuRightPressed;
    public bool ConfirmPressed;
    public bool CancelPressed;
    public bool TouchUsed;
    /// <summary>Virtual-screen position of the latest tap or click, if any.</summary>
    public Vector2? Tap;
}

/// <summary>
/// Folds keyboard, gamepad and touch into one snapshot. Touch regions are laid out in the
/// 640x360 virtual screen and converted using the current letterbox transform.
/// </summary>
public sealed class InputRouter
{
    private KeyboardState _prevKeyboard;
    private GamePadState _prevPad;
    private MouseState _prevMouse;
    private bool _hasPrev;
    private int _stickTouchId = -1;
    private Vector2 _stickOrigin;
    private bool _touchJump, _touchFire, _touchInteract, _touchPause;
    private bool _touchJumpPrev, _touchFirePrev, _touchInteractPrev, _touchPausePrev;
    public Vector2 VirtualScale { get; set; } = Vector2.One;
    public Vector2 VirtualOffset { get; set; }
    public Vector2 ScreenSize { get; set; } = new(640, 360);
    public bool TouchControlsVisible { get; set; }
    /// <summary>Desktop keyboard map; phones ignore it and use the touch overlay.</summary>
    public KeyBindings Bindings { get; set; } = new();

    public Controls Read()
    {
        var kb = Keyboard.GetState();
        var pad = GamePad.GetState(PlayerIndex.One);
        var mouse = Mouse.GetState();
        bool first = !_hasPrev;

        var c = new Controls();
        var bind = Bindings;
        float left = 0f, right = 0f;
        if (bind.IsDown(kb, GameAction.Left) || kb.IsKeyDown(Keys.Left)) left -= 1f;
        if (bind.IsDown(kb, GameAction.Right) || kb.IsKeyDown(Keys.Right)) right += 1f;
        if (pad.IsButtonDown(Buttons.DPadLeft)) left -= 1f;
        if (pad.IsButtonDown(Buttons.DPadRight)) right += 1f;
        left += -pad.ThumbSticks.Left.X;
        right += pad.ThumbSticks.Left.X;
        c.MoveX = Math.Clamp(right - left, -1f, 1f);
        if (MathF.Abs(c.MoveX) < 0.18f) c.MoveX = 0f;

        bool jumpKb = IsFlapKey(kb);
        bool jumpPad = pad.IsButtonDown(Buttons.A);
        bool fireKb = bind.IsDown(kb, GameAction.Spit);
        bool firePad = pad.IsButtonDown(Buttons.X) || pad.IsButtonDown(Buttons.RightTrigger);
        bool interactKb = IsUseKey(kb);
        bool interactPad = pad.IsButtonDown(Buttons.B) || pad.IsButtonDown(Buttons.Y);

        bool firePressedKb = fireKb && !IsSpitKey(_prevKeyboard);
        bool firePressedPad = pad.IsButtonDown(Buttons.X) && !_prevPad.IsButtonDown(Buttons.X);
        bool enterPressed = (kb.IsKeyDown(Keys.Enter) && !_prevKeyboard.IsKeyDown(Keys.Enter)) ||
                            (pad.IsButtonDown(Buttons.Start) && !_prevPad.IsButtonDown(Buttons.Start));
        bool escPressed = (kb.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape)) ||
                          (pad.IsButtonDown(Buttons.Back) && !_prevPad.IsButtonDown(Buttons.Back));

        c.JumpPressed = first || (jumpKb && !IsFlapKey(_prevKeyboard)) || (jumpPad && !_prevPad.IsButtonDown(Buttons.A));
        c.JumpHeld = jumpKb || jumpPad;
        c.FirePressed = first || firePressedKb || firePressedPad;
        c.FireHeld = fireKb || firePad;
        c.InteractPressed = (interactKb && !IsUseKey(_prevKeyboard)) ||
                            (interactPad && !_prevPad.IsButtonDown(Buttons.B) && !_prevPad.IsButtonDown(Buttons.Y));
        c.PausePressed = escPressed ||
                         (bind.IsDown(kb, GameAction.Pause) && !bind.IsDown(_prevKeyboard, GameAction.Pause));
        c.MapPressed = (bind.IsDown(kb, GameAction.Map) && !bind.IsDown(_prevKeyboard, GameAction.Map)) ||
                       (pad.IsButtonDown(Buttons.Back) && !_prevPad.IsButtonDown(Buttons.Back));
        c.MenuUpPressed = (kb.IsKeyDown(Keys.Up) && !_prevKeyboard.IsKeyDown(Keys.Up)) ||
                          (kb.IsKeyDown(Keys.W) && !_prevKeyboard.IsKeyDown(Keys.W)) ||
                          (pad.IsButtonDown(Buttons.DPadUp) && !_prevPad.IsButtonDown(Buttons.DPadUp));
        c.MenuDownPressed = (kb.IsKeyDown(Keys.Down) && !_prevKeyboard.IsKeyDown(Keys.Down)) ||
                            (kb.IsKeyDown(Keys.S) && !_prevKeyboard.IsKeyDown(Keys.S)) ||
                            (pad.IsButtonDown(Buttons.DPadDown) && !_prevPad.IsButtonDown(Buttons.DPadDown));
        c.MenuLeftPressed = (kb.IsKeyDown(Keys.Left) && !_prevKeyboard.IsKeyDown(Keys.Left)) ||
                            (kb.IsKeyDown(Keys.A) && !_prevKeyboard.IsKeyDown(Keys.A)) ||
                            (pad.IsButtonDown(Buttons.DPadLeft) && !_prevPad.IsButtonDown(Buttons.DPadLeft));
        c.MenuRightPressed = (kb.IsKeyDown(Keys.Right) && !_prevKeyboard.IsKeyDown(Keys.Right)) ||
                             (kb.IsKeyDown(Keys.D) && !_prevKeyboard.IsKeyDown(Keys.D)) ||
                             (pad.IsButtonDown(Buttons.DPadRight) && !_prevPad.IsButtonDown(Buttons.DPadRight));
        c.ConfirmPressed = (kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Space)) &&
                           !(_prevKeyboard.IsKeyDown(Keys.Enter) || _prevKeyboard.IsKeyDown(Keys.Space)) ||
                           (pad.IsButtonDown(Buttons.A) && !_prevPad.IsButtonDown(Buttons.A));
        c.CancelPressed = escPressed;

        // Pointer click is used for menus on desktop as well as touch on phones.
        bool click = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        if (click)
        {
            c.ConfirmPressed = true;
            c.TouchUsed = true;
            c.Tap = (new Vector2(mouse.X, mouse.Y) - VirtualOffset) / VirtualScale;
        }

        if (TouchControlsVisible)
        {
            UpdateTouch(ref c);
            c.TouchUsed = true;
        }
        else
        {
            // taps drive the title and menu screens on phones
            var taps = TouchPanel.GetState();
            foreach (var t in taps)
            {
                if (t.State == TouchLocationState.Pressed)
                {
                    c.ConfirmPressed = true;
                    c.TouchUsed = true;
                    c.Tap = ScreenToVirtual(t.Position);
                }
            }
        }

        _prevKeyboard = kb;
        _prevPad = pad;
        _prevMouse = mouse;
        _hasPrev = true;
        return c;
    }

    // The up arrow always flaps as well, so the arrow cluster stays usable.
    private bool IsFlapKey(KeyboardState k) => Bindings.IsDown(k, GameAction.Flap) || k.IsKeyDown(Keys.Up);

    private bool IsSpitKey(KeyboardState k) => Bindings.IsDown(k, GameAction.Spit);

    private bool IsUseKey(KeyboardState k) =>
        Bindings.IsDown(k, GameAction.Use) || k.IsKeyDown(Keys.Enter);

    private Vector2 ScreenToVirtual(Vector2 screen)
    {
        // iOS can report touches in points while the backbuffer is in pixels. If the
        // touch panel's display size disagrees with the backbuffer, normalise it first.
        float dw = TouchPanel.DisplayWidth, dh = TouchPanel.DisplayHeight;
        if (dw > 0 && dh > 0 && ScreenSize.X > 0 && ScreenSize.Y > 0 &&
            (MathF.Abs(dw - ScreenSize.X) > 1f || MathF.Abs(dh - ScreenSize.Y) > 1f))
        {
            if (MathF.Abs(ScreenSize.X / ScreenSize.Y - dw / dh) >
                MathF.Abs(ScreenSize.X / ScreenSize.Y - dh / dw))
                (dw, dh) = (dh, dw);
            screen = new Vector2(screen.X * ScreenSize.X / dw, screen.Y * ScreenSize.Y / dh);
        }
        return (screen - VirtualOffset) / VirtualScale;
    }

    private void UpdateTouch(ref Controls c)
    {
        _touchJumpPrev = _touchJump;
        _touchFirePrev = _touchFire;
        _touchInteractPrev = _touchInteract;
        _touchPause = false;
        _touchJump = _touchFire = _touchInteract = false;

        var touches = TouchPanel.GetState();
        bool stickFound = false;
        Vector2? tap = null;
        foreach (var touch in touches)
        {
            if (touch.State == TouchLocationState.Invalid || touch.State == TouchLocationState.Released)
                continue;
            var p = ScreenToVirtual(touch.Position);
            if (touch.State == TouchLocationState.Pressed && tap == null) tap = p;
            // If this finger owns the stick, keep steering even when it drifts out
            // of the stick's visual circle.
            if (_stickTouchId == touch.Id)
            {
                Steer(p, ref c);
                stickFound = true;
                continue;
            }
            // Pause button (right, below the inventory)
            if (VirtualRect(572, 76, 64, 28).Contains(p))
            {
                _touchPause = true;
                continue;
            }
            if (p.X < 320 && p.Y > 140)
            {
                if (_stickTouchId == -1 && touch.State == TouchLocationState.Pressed)
                {
                    _stickTouchId = touch.Id;
                    _stickOrigin = p;
                    Steer(p, ref c);
                    stickFound = true;
                }
            }
            else if (VirtualRect(548, 238, 80, 80).Contains(p))
            {
                _touchJump = true;
            }
            else if (VirtualRect(478, 268, 72, 72).Contains(p))
            {
                _touchFire = true;
            }
            else if (VirtualRect(486, 168, 64, 50).Contains(p))
            {
                _touchInteract = true;
            }
        }
        if (!stickFound) _stickTouchId = -1;
        c.Tap = tap;
        if (_touchJump) { c.JumpHeld = true; if (!_touchJumpPrev) c.JumpPressed = true; }
        if (_touchFire) { c.FireHeld = true; if (!_touchFirePrev) c.FirePressed = true; }
        if (_touchInteract && !_touchInteractPrev) c.InteractPressed = true;
        if (_touchPause) c.PausePressed = true;
    }

    private void Steer(Vector2 p, ref Controls c)
    {
        var delta = p - _stickOrigin;
        float len = delta.Length();
        if (len > 46f)
        {
            delta = delta / len * 46f;
            // let the stick origin trail the finger for comfortable steering
            _stickOrigin = p - delta;
        }
        c.MoveX = Math.Clamp(delta.X / 34f, -1f, 1f);
        if (MathF.Abs(c.MoveX) < 0.18f) c.MoveX = 0f;
        _touchJump = delta.Y < -22f;
    }

    private static Rectangle VirtualRect(int x, int y, int w, int h) => new(x, y, w, h);

    public void DrawTouchOverlay(SpriteBatch batch, Texture2D white, Texture2D glow, Texture2D font)
    {
        if (!TouchControlsVisible) return;
        var ink = new Color(8, 14, 22, 255) * 0.35f;
        var rim = new Color(150, 240, 210, 255) * 0.55f;
        batch.Draw(white, new Rectangle(24, 232, 120, 120), ink);
        batch.Draw(glow, new Rectangle(30, 238, 108, 108), rim * 0.35f);
        // stick
        batch.Draw(white, new Rectangle(76, 284, 16, 16), rim);
        // pause: a bordered, labelled button so it reads as a control rather than scenery
        batch.Draw(white, new Rectangle(572, 76, 64, 28), ink);
        batch.Draw(white, new Rectangle(572, 76, 64, 1), rim);
        batch.Draw(white, new Rectangle(572, 103, 64, 1), rim);
        batch.Draw(white, new Rectangle(572, 76, 1, 28), rim);
        batch.Draw(white, new Rectangle(635, 76, 1, 28), rim);
        PixelFont.DrawCentered(batch, font, "PAUSE", 604, 86, new Color(190, 255, 220), 1f, 0.95f);
        // jump
        batch.Draw(glow, new Rectangle(552, 242, 72, 72), rim * 0.45f);
        PixelFont.DrawCentered(batch, font, "FLAP", 588, 272, new Color(200, 255, 230), 1f, 0.95f);
        // fire
        batch.Draw(glow, new Rectangle(482, 272, 64, 64), new Color(255, 190, 110) * 0.45f);
        PixelFont.DrawCentered(batch, font, "SPIT", 514, 296, new Color(255, 226, 170), 1f, 0.95f);
        // interact
        batch.Draw(white, new Rectangle(492, 174, 52, 38), ink);
        PixelFont.DrawCentered(batch, font, "USE", 518, 186, new Color(190, 255, 220), 1f, 0.95f);
    }
}
