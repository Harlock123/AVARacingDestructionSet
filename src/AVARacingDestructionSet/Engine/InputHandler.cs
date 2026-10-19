using Avalonia.Input;

namespace AVARacingDestructionSet.Engine;

/// <summary>
/// Keyboard and two gamepads boiled down to each driver's controls. Player 1 drives with the arrows (Up is the gas,
/// Down the brake) and drops a weapon with Right Ctrl or Enter; player 2 with W, A, S and D, and Left Shift or
/// Space. The first gamepad is player 1's, the second player 2's. (The mouse, which builds tracks and vehicles,
/// goes straight to the engine.)
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly Dictionary<PhysicalKey, Key> _pressedAs = [];
    private readonly ControllerManager[] _pads = [new(0), new(1)];

    /// <summary>
    /// A key goes down. The key it was pressed as is kept against the physical key, because the release can be
    /// named differently: with the keyboard option shift:both_capslock_cancel (Omarchy's default), letting go of a
    /// Shift arrives as CapsLock, and a Shift that was never let go would keep player 2 firing for good.
    /// </summary>
    public void OnKeyDown(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        if (physical != PhysicalKey.None)
        {
            if (_pressedAs.TryGetValue(physical, out var was) && was != key) _held.Remove(was);
            _pressedAs[physical] = key;
        }
        _held.Remove(key);
        _held.Add(key);
    }

    /// <summary>A key comes up: the key it was pressed as is let go too, whatever the release is called.</summary>
    public void OnKeyUp(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        _held.Remove(key);
        if (physical != PhysicalKey.None && _pressedAs.Remove(physical, out var was)) _held.Remove(was);
    }

    public bool Has(Key key) => _held.Contains(key);
    private bool Any(params Key[] keys) => keys.Any(_held.Contains);

    /// <summary>Let go of everything (the window lost the focus).</summary>
    public void Clear()
    {
        _held.Clear();
        _pressedAs.Clear();
    }

    public void Update()
    {
        foreach (var p in _pads) p.Update();
    }

    /// <summary>A driver's controls: player 0 or 1.</summary>
    public Controls Player(int player)
    {
        var pad = _pads[player];
        var (left, right, gas, brake, fire) = player == 0
            ? (Any(Key.Left), Any(Key.Right), Any(Key.Up), Any(Key.Down), Any(Key.RightCtrl, Key.Enter))
            : (Any(Key.A), Any(Key.D), Any(Key.W), Any(Key.S), Any(Key.LeftShift, Key.Space));
        left |= pad.DPadLeft || pad.LeftStickX < -0.4f;
        right |= pad.DPadRight || pad.LeftStickX > 0.4f;
        gas |= pad.ButtonA || pad.RightTrigger > 0.5f || pad.DPadUp;
        brake |= pad.ButtonB || pad.LeftTrigger > 0.5f || pad.DPadDown;
        fire |= pad.ButtonX || pad.ButtonY || pad.RightBumper || pad.LeftBumper;
        return new Controls((right ? 1 : 0) - (left ? 1 : 0), gas, brake, fire);
    }

    /// <summary>Enter, or Start on either gamepad.</summary>
    public bool Start => Any(Key.Enter) || _pads.Any(p => p.ButtonStart);

    public bool PadUp => _pads[0].DPadUp || _pads[0].LeftStickY < -0.5f;
    public bool PadDown => _pads[0].DPadDown || _pads[0].LeftStickY > 0.5f;
    public bool PadLeft => _pads[0].DPadLeft || _pads[0].LeftStickX < -0.5f;
    public bool PadRight => _pads[0].DPadRight || _pads[0].LeftStickX > 0.5f;
    public bool PadAccept => _pads[0].ButtonA || _pads[0].ButtonX || _pads[0].ButtonB;
    public bool PadDone => _pads[0].ButtonStart;
    public bool PadPause => _pads.Any(p => p.ButtonStart);
    public bool PadBack => _pads.Any(p => p.ButtonBack);

    public void Dispose()
    {
        foreach (var p in _pads) p.Dispose();
    }
}
