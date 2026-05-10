// Assets/_Scripts/Input/InputBuffer.cs (New Input System version)
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputBuffer : MonoBehaviour
{
    [Header("Config")]
    public int bufferSize = 20;
    public int motionLeniency = 10;

    private FrameInput[] _buffer;
    private int _head = 0;
    private bool _facingRight = true;

    // Current frame raw values (read by PlayerInput callbacks)
    private float _moveH, _moveV;
    private bool _lightDown, _medDown, _heavyDown, _tauntDown;

    public struct FrameInput
    {
        public bool forward, back, up, down, downForward, downBack;
        public bool light, medium, heavy, taunt, block, jump;
        public bool lightPressed, medPressed, heavyPressed, tauntPressed, jumpPressed;
        public bool consumed;
    }

    void Awake() => _buffer = new FrameInput[bufferSize];

    void FixedUpdate() => RecordFrame();

    void RecordFrame()
    {
        _head = (_head + 1) % bufferSize;
        _facingRight = GetComponent<FighterAnimationController>()?.FacingRight ?? true;

        bool rawFwd = _facingRight ? _moveH > 0.5f : _moveH < -0.5f;
        bool rawBack = _facingRight ? _moveH < -0.5f : _moveH > 0.5f;
        bool down = _moveV < -0.5f;
        bool up = _moveV > 0.5f;

        var prev = _buffer[(_head - 1 + bufferSize) % bufferSize];

        _buffer[_head] = new FrameInput
        {
            forward = rawFwd,
            back = rawBack,
            up = up,
            down = down,
            downForward = down && rawFwd,
            downBack = down && rawBack,
            light = _lightDown,
            medium = _medDown,
            heavy = _heavyDown,
            taunt = _tauntDown,
            block = rawBack && !up,
            jump = up,
            lightPressed = _lightDown && !prev.light,
            medPressed = _medDown && !prev.medium,
            heavyPressed = _heavyDown && !prev.heavy,
            tauntPressed = _tauntDown && !prev.taunt,
            jumpPressed = up && !prev.up,
            consumed = false
        };
    }

    // ── PlayerInput callbacks (wire via Inspector Unity Events) ──
    public void OnMoveHorizontal(InputValue v) => _moveH = v.Get<float>();
    public void OnMoveVertical(InputValue v) => _moveV = v.Get<float>();
    public void OnAttackLight(InputValue v) => _lightDown = v.isPressed;
    public void OnAttackMedium(InputValue v) => _medDown = v.isPressed;
    public void OnAttackHeavy(InputValue v) => _heavyDown = v.isPressed;
    public void OnTaunt(InputValue v) => _tauntDown = v.isPressed;

    // ── Public API ─────────────────────────────────────────────
    public FrameInput Current => _buffer[_head];

    public bool CheckQCF(string btn)
    {
        int i = _head;
        if (!FindButtonPress(ref i, btn)) return false;
        if (_buffer[i].consumed) return false;
        int fwd = i; if (!FindDirection(ref fwd, "forward")) return false;
        int df = fwd; if (!FindDirection(ref df, "downForward")) return false;
        int dn = df; if (!FindDirection(ref dn, "down")) return false;
        _buffer[i].consumed = true; return true;
    }

    public bool CheckQCB(string btn)
    {
        int i = _head;
        if (!FindButtonPress(ref i, btn)) return false;
        if (_buffer[i].consumed) return false;
        int bk = i; if (!FindDirection(ref bk, "back")) return false;
        int db = bk; if (!FindDirection(ref db, "downBack")) return false;
        int dn = db; if (!FindDirection(ref dn, "down")) return false;
        _buffer[i].consumed = true; return true;
    }

    public bool CheckDash(out DashDirection dir)
    {
        dir = DashDirection.Forward;
        for (int j = 1; j < motionLeniency; j++)
        {
            var cur = GetFrameAt(0);
            var prev = GetFrameAt(j);
            if (cur.forward && !prev.forward) { dir = DashDirection.Forward; return true; }
            if (cur.back && !prev.back) { dir = DashDirection.Backward; return true; }
        }
        return false;
    }

    FrameInput GetFrameAt(int offset)
        => _buffer[((_head - offset) % bufferSize + bufferSize) % bufferSize];

    bool FindButtonPress(ref int startFrame, string btn)
    {
        for (int j = 0; j < motionLeniency; j++)
        {
            var f = GetFrameAt(startFrame + j);
            bool p = btn switch
            {
                "light" => f.lightPressed,
                "medium" => f.medPressed,
                "heavy" => f.heavyPressed,
                _ => false
            };
            if (p) { startFrame += j; return true; }
        }
        return false;
    }

    bool FindDirection(ref int offset, string dir)
    {
        for (int j = 0; j < motionLeniency; j++)
        {
            var f = GetFrameAt(offset + j);
            bool found = dir switch
            {
                "forward" => f.forward,
                "back" => f.back,
                "down" => f.down,
                "downForward" => f.downForward,
                "downBack" => f.downBack,
                _ => false
            };
            if (found) { offset += j; return true; }
        }
        return false;
    }
}