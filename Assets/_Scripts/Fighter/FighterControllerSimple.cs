using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(PhysicsBody))]
[RequireComponent(typeof(StateMachine))]
[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(InputBuffer))]
public class FighterControllerSimple : MonoBehaviour, IBoxProvider
{
    [Header("Components")]
    public CinemachineImpulseSource impulseSource;

    [Header("Identity")]
    public int playerIndex = 0;

    [HideInInspector] public bool inputLocked = false;

    [Header("Combat Config - Standing")]
    public MoveData moveLight;
    public MoveData moveMedium;
    public MoveData moveHeavy;
    public MoveData moveSpecial;

    [Header("Combat Config - Crouching")]
    public MoveData moveCrouchLight;
    public MoveData moveCrouchMedium;
    public MoveData moveCrouchHeavy;

    [Header("Combat Config - Aerial")]
    public MoveData moveAirLight;
    public MoveData moveAirMedium;
    public MoveData moveAirHeavy;

    [Header("Input Keys")]
    public KeyCode keyLeft = KeyCode.A;
    public KeyCode keyRight = KeyCode.D;
    public KeyCode keyCrouch = KeyCode.S;
    public KeyCode keyJump = KeyCode.W;
    public KeyCode keyBlock = KeyCode.LeftShift;

    [Header("Hitboxes")]
    public GameObject hitboxLight;
    public GameObject hitboxMedium;
    public GameObject hitboxHeavy;
    public GameObject hitboxSpecial;
    public GameObject hurtboxStanding;
    public GameObject hurtboxCrouching;

    public HitFlashController hitFlash;

    private PhysicsBody _physics;
    private StateMachine _machine;
    private CharacterState _charState;
    private Animator _animator;
    private InputBuffer _inputBuffer;

    private MoveData _currentMove;
    private GameObject _currentHitbox;
    private readonly List<BoxData> _activeBoxes = new List<BoxData>();

    // [FIX 4] Combo Tracking Fields
    private int _comboCounter = 0;
    private float _comboResetTimer = 0f;
    private const float COMBO_RESET = 2.0f;

    public void SpawnHitSpark(Vector3 position, bool isHeavy) { }
    public void SpawnAtHand(string key, Transform character) { }
    public void TriggerSpecialFlash() { }
    public void TriggerKOFlash() { }
    public void TriggerVictoryAura(MonoBehaviour fighter) { }
    public void SetComboTrail(bool active, int comboCount) { }
    public void Play(string key) { }

    public int PlayerIndex => playerIndex;
    public Vector2 Position => _physics.Position;
    public FacingDirection Facing { get; private set; } = FacingDirection.Right;
    public bool IsBlocking => _machine.IsBlocking;
    public bool IsCrouching => _machine.CurrentStateType == FighterStateType.Crouching || _machine.CurrentStateType == FighterStateType.BlockingCrouching;
    public IReadOnlyList<BoxData> GetActiveBoxes() => _activeBoxes;

    private Transform _opponentTarget;

    void Awake()
    {
        _physics = GetComponent<PhysicsBody>();
        _machine = GetComponent<StateMachine>();
        _charState = GetComponent<CharacterState>();
        _animator = GetComponent<Animator>();
        _inputBuffer = GetComponent<InputBuffer>();

        _charState.playerIndex = this.playerIndex;
    }

    void Start()
    {
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnHitConfirmed += HandleHitConfirmed;

        Facing = playerIndex == 0 ? FacingDirection.Right : FacingDirection.Left;
        DisableAllHitboxes();
    }

    void OnDestroy()
    {
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnHitConfirmed -= HandleHitConfirmed;
    }

    void Update()
    {
        if (_machine.CurrentStateType == FighterStateType.Dead ||
            _machine.CurrentStateType == FighterStateType.Victory ||
            _charState.InHitFreeze ||
            inputLocked)
            return;

        if (GetComponent<BasicAI>() == null)
        {
            HandleMovementInput();
            HandleBlockInput();
            HandleCombatInput();
        }

        UpdateFacingDirection();
        UpdateHurtbox();

        // [FIX 4] Drop the combo if too much time passes
        if (_comboResetTimer > 0f)
        {
            _comboResetTimer -= Time.deltaTime;
            if (_comboResetTimer <= 0f)
            {
                _comboCounter = 0;
                GetComponent<MoveExecutor>()?.NotifyCombo(0);
                HUDController.Instance?.UpdatePower(playerIndex, 0f);
            }
        }
    }

    void HandleMovementInput()
    {
        if (_machine.IsStunned || _machine.IsAttacking)
        {
            _physics.SetMoveInput(0f);
            return;
        }

        float input = 0f;
        if (Input.GetKey(keyRight)) input = 1f;
        if (Input.GetKey(keyLeft)) input = -1f;
        _physics.SetMoveInput(input);

        if (Input.GetKey(keyCrouch) && _physics.IsGrounded)
        {
            if (_machine.CurrentStateType != FighterStateType.Crouching)
                _machine.TransitionTo(FighterStateType.Crouching);
        }
        else if (_machine.CurrentStateType == FighterStateType.Crouching)
        {
            _machine.TransitionTo(FighterStateType.Idle);
        }
    }

    void HandleBlockInput()
    {
        bool wantsBlock = Input.GetKey(keyBlock);
        if (wantsBlock && _physics.IsGrounded && !_machine.IsAttacking)
        {
            FighterStateType blockState = IsCrouching ? FighterStateType.BlockingCrouching : FighterStateType.BlockingStanding;
            if (_machine.CurrentStateType != blockState)
                _machine.TransitionTo(blockState);
        }
    }

    private void HandleCombatInput()
    {
        if (_inputBuffer == null) return;
        if (inputLocked) return;

        var inp = _inputBuffer.Current;
        var exec = GetComponent<MoveExecutor>();

        // ── Specials FIRST (QCF/QCB must beat normals) ─────────────────
        if (!_machine.IsAirborne)
        {
            if (_inputBuffer.CheckQCF("light") || _inputBuffer.CheckQCF("heavy"))
            {
                if (StartAttack(moveSpecial, FighterStateType.SpecialMove, hitboxSpecial, "SpecialMove"))
                {
                    exec?.NotifySpecialActivated(moveSpecial);
                    return;
                }
            }
            if (_inputBuffer.CheckQCB("light") || _inputBuffer.CheckQCB("heavy"))
            {
                if (StartAttack(moveSpecial, FighterStateType.SpecialMove, hitboxSpecial, "SpecialMove"))
                {
                    exec?.NotifySpecialActivated(moveSpecial);
                    return;
                }
            }
        }

        // ── Dash ──────────────────────────────────────────────────────
        if (_inputBuffer.CheckDash(out DashDirection dashDir) &&
            _machine.CurrentStateType == FighterStateType.Idle)
        {
            exec?.NotifyDash(dashDir);
            return;
        }

        // ── Normal attacks ────────────────────────────────────────────
        if (inp.lightPressed)
        {
            MoveData move = _machine.IsAirborne ? moveAirLight : IsCrouching ? moveCrouchLight : moveLight;
            FighterStateType st = _machine.IsAirborne ? FighterStateType.AirLightPunch : IsCrouching ? FighterStateType.LightKick : FighterStateType.LightPunch;
            string trigger = _machine.IsAirborne ? "AirLight" : IsCrouching ? "CrouchLight" : "LightAttack";

            if (StartAttack(move, st, hitboxLight, trigger))
                exec?.NotifyAttackStarted(move);
        }
        else if (inp.medPressed) // FIXED to medPressed
        {
            MoveData move = _machine.IsAirborne ? moveAirMedium : IsCrouching ? moveCrouchMedium : moveMedium;
            FighterStateType st = _machine.IsAirborne ? FighterStateType.AirHeavyKick : IsCrouching ? FighterStateType.MediumKick : FighterStateType.MediumPunch;
            string trigger = _machine.IsAirborne ? "AirMedium" : IsCrouching ? "CrouchMedium" : "MediumAttack";

            if (StartAttack(move, st, hitboxMedium, trigger))
                exec?.NotifyAttackStarted(move);
        }
        else if (inp.heavyPressed)
        {
            MoveData move = _machine.IsAirborne ? moveAirHeavy : IsCrouching ? moveCrouchHeavy : moveHeavy;
            FighterStateType st = _machine.IsAirborne ? FighterStateType.AirHeavyKick : IsCrouching ? FighterStateType.HeavyKick : FighterStateType.HeavyPunch;
            string trigger = _machine.IsAirborne ? "AirHeavy" : IsCrouching ? "CrouchHeavy" : "HeavyAttack";

            if (StartAttack(move, st, hitboxHeavy, trigger))
                exec?.NotifyAttackStarted(move);
        }

        // ── Block ─────────────────────────────────────────────────────
        if (inp.back && _physics.IsGrounded && !_machine.IsAttacking)
        {
            _machine.TransitionTo(IsCrouching
                ? FighterStateType.BlockingCrouching
                : FighterStateType.BlockingStanding);
        }

        // ── Jump ──────────────────────────────────────────────────────
        if (inp.up && _physics.IsGrounded && !_machine.IsStunned)
        {
            _physics.RequestJump();
            _machine.TransitionTo(FighterStateType.JumpNeutral);
            GetComponent<MoveExecutor>()?.NotifyJump();
        }

        // ── Taunt ────────────────────────────────────────────────────
        if (inp.tauntPressed && _machine.CurrentStateType == FighterStateType.Idle)
        {
            GetComponent<MoveExecutor>()?.NotifyTaunt();
            _animator.SetTrigger("Taunt");
        }
    }

    bool StartAttack(MoveData move, FighterStateType state, GameObject hitbox, string trigger)
    {
        if (move == null) return false;

        _currentMove = move;
        _currentHitbox = hitbox;

        bool success = _machine.TransitionTo(state);
        if (!success) return false;

        int duration = move.startupFrames + move.activeFrames + move.recoveryFrames;

        var method = state switch
        {
            FighterStateType.LightPunch => (System.Action)(() => _machine.GetState<LightPunchState>()?.SetDuration(duration)),
            FighterStateType.MediumPunch => () => _machine.GetState<MediumPunchState>()?.SetDuration(duration),
            FighterStateType.HeavyPunch => () => _machine.GetState<HeavyPunchState>()?.SetDuration(duration),
            FighterStateType.LightKick => () => _machine.GetState<LightKickState>()?.SetDuration(duration),
            FighterStateType.MediumKick => () => _machine.GetState<MediumKickState>()?.SetDuration(duration),
            FighterStateType.HeavyKick => () => _machine.GetState<HeavyKickState>()?.SetDuration(duration),
            FighterStateType.SpecialMove => () => _machine.GetState<SpecialMoveState>()?.SetDuration(duration),
            FighterStateType.AirLightPunch => () => _machine.GetState<AirLightPunchState>()?.SetDuration(duration),
            FighterStateType.AirHeavyKick => () => _machine.GetState<AirHeavyKickState>()?.SetDuration(duration),
            _ => () => { }
        };
        method();

        _animator.SetTrigger(trigger);
        return true;
    }

    void HandleHitConfirmed(HitEvent e)
    {
        // [FIX 4] Defender side (receiving the hit)
        if (e.defenderIndex == playerIndex && _machine.CurrentStateType != FighterStateType.Dead)
        {
            bool isBlocked = e.result == HitResult.Blocked;

            _charState.TakeDamage(
                damage: e.damageDealt,
                hitstun: e.sourceBox.hitstunFrames,
                blockstun: e.sourceBox.blockstunFrames,
                knockback: e.sourceBox.knockback,
                causesKnockdown: e.sourceBox.damage >= 22,
                isBlocked: isBlocked,
                hitFreezeFrames: e.sourceBox.damage >= 22 ? 4 : 2
            );

            _machine.NotifyHitReceived(e);
            StartCoroutine(HitFreeze(e.sourceBox.damage >= 22 ? 4 : 2));
        }

        // [FIX 4] Attacker side — combo tracking
        if (e.attackerIndex == playerIndex && e.result == HitResult.Hit)
        {
            _comboCounter++;
            _comboResetTimer = COMBO_RESET;

            GetComponent<MoveExecutor>()?.NotifyCombo(_comboCounter);

            float powerNorm = Mathf.Clamp01(_comboCounter / 10f);
            HUDController.Instance?.UpdatePower(playerIndex, powerNorm);
        }
    }

    private IEnumerator HitFreeze(int freezeFrames)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(freezeFrames / 60f);
        Time.timeScale = 1.0f;
    }

    public void OnAttackImpactFrame()
    {
        DisableAllHitboxes();
        if (_currentHitbox != null && _currentMove != null)
        {
            _currentHitbox.SetActive(true);
            _activeBoxes.Clear();
            _activeBoxes.Add(new BoxData(BoxType.Hitbox, Vector2.zero, new Vector2(1f, 1f), _currentMove.damage, _currentMove.hitLevel, _currentMove.hitstunFrames, _currentMove.blockstunFrames, _currentMove.knockback));
        }
    }

    public void OnAttackEnd()
    {
        DisableAllHitboxes();
        _activeBoxes.Clear();
    }

    void DisableAllHitboxes()
    {
        if (hitboxLight) hitboxLight.SetActive(false);
        if (hitboxMedium) hitboxMedium.SetActive(false);
        if (hitboxHeavy) hitboxHeavy.SetActive(false);
        if (hitboxSpecial) hitboxSpecial.SetActive(false);
    }

    void UpdateHurtbox()
    {
        bool crouching = IsCrouching;
        if (hurtboxStanding) hurtboxStanding.SetActive(!crouching);
        if (hurtboxCrouching) hurtboxCrouching.SetActive(crouching);
    }

    void UpdateFacingDirection()
    {
        if (_opponentTarget == null)
        {
            FighterControllerSimple[] players = FindObjectsOfType<FighterControllerSimple>();
            foreach (var player in players)
            {
                if (player != this) _opponentTarget = player.transform;
            }

            if (_opponentTarget == null) return;
        }

        bool opponentRight = _opponentTarget.position.x > transform.position.x;
        Facing = opponentRight ? FacingDirection.Right : FacingDirection.Left;
        transform.rotation = Quaternion.Euler(0f, Facing == FacingDirection.Right ? 90f : -90f, 0f);
    }

    void OnSpecialActivated()
    {
        StartCoroutine(SpecialActivationBeat());
    }

    IEnumerator SpecialActivationBeat()
    {
        Time.timeScale = 0.4f;
        yield return new WaitForSecondsRealtime(0.08f);
        Time.timeScale = 1.0f;

        impulseSource?.GenerateImpulse(new Vector3(0f, -0.3f, 0f));

        // Hook up your actual particle spawner here later!
        // HitEffectsManager.Instance?.SpawnSpecialActivationVFX(transform.position, playerIndex);
    }

    // ── AI SIMULATION METHODS ──
    public void SimulateJump()
    {
        if (_physics.IsGrounded)
        {
            _physics.RequestJump();
            _machine.TransitionTo(FighterStateType.JumpNeutral);
        }
    }

    public void SimulateBlock(bool block)
    {
        if (block && _physics.IsGrounded && !_machine.IsAttacking)
            _machine.TransitionTo(IsCrouching ? FighterStateType.BlockingCrouching : FighterStateType.BlockingStanding);
    }

    public void SimulateAttack(AttackWeight weight)
    {
        if (weight == AttackWeight.Light) StartAttack(moveLight, FighterStateType.LightPunch, hitboxLight, "LightAttack");
        else if (weight == AttackWeight.Medium) StartAttack(moveMedium, FighterStateType.MediumPunch, hitboxMedium, "MediumAttack");
        else StartAttack(moveHeavy, FighterStateType.HeavyPunch, hitboxHeavy, "HeavyAttack");
    }

    public void SimulateSpecial()
    {
        StartAttack(moveSpecial, FighterStateType.SpecialMove, hitboxSpecial, "SpecialMove");
    }
}