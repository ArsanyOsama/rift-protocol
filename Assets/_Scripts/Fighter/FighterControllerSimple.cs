using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PhysicsBody))]
[RequireComponent(typeof(StateMachine))]   // Brain 1: Movement/Animation
[RequireComponent(typeof(CharacterState))] // Brain 2: Combat/Stats
[RequireComponent(typeof(Animator))]
public class FighterControllerSimple : MonoBehaviour, IBoxProvider
{
    [Header("Identity")]
    public int playerIndex = 0;

    [Header("Move Data")]
    public MoveData moveLight;
    public MoveData moveMedium;
    public MoveData moveHeavy;
    public MoveData moveSpecial;

    [Header("Input Keys")]
    public KeyCode keyLeft = KeyCode.A;
    public KeyCode keyRight = KeyCode.D;
    public KeyCode keyCrouch = KeyCode.S;
    public KeyCode keyJump = KeyCode.W;
    public KeyCode keyLight = KeyCode.J;
    public KeyCode keyMedium = KeyCode.K;
    public KeyCode keyHeavy = KeyCode.L;
    public KeyCode keyBlock = KeyCode.LeftShift;
    public KeyCode keySpecial = KeyCode.U;

    [Header("Hitboxes")]
    public GameObject hitboxLight;
    public GameObject hitboxMedium;
    public GameObject hitboxHeavy;
    public GameObject hitboxSpecial;
    public GameObject hurtboxStanding;
    public GameObject hurtboxCrouching;

    public HitFlashController hitFlash;

    // The Systems
    private PhysicsBody _physics;
    private StateMachine _machine;     // Brain 1
    private CharacterState _charState; // Brain 2
    private Animator _animator;

    private MoveData _currentMove;
    private GameObject _currentHitbox;
    private readonly List<BoxData> _activeBoxes = new List<BoxData>();

    // --- IBoxProvider Implementation ---
    public int PlayerIndex => playerIndex;
    public Vector2 Position => _physics.Position;
    public FacingDirection Facing { get; private set; } = FacingDirection.Right;
    public bool IsBlocking => _machine.IsBlocking;
    public bool IsCrouching => _machine.CurrentStateType == FighterStateType.Crouching || _machine.CurrentStateType == FighterStateType.BlockingCrouching;
    public IReadOnlyList<BoxData> GetActiveBoxes() => _activeBoxes;

    void Awake()
    {
        _physics = GetComponent<PhysicsBody>();
        _machine = GetComponent<StateMachine>();
        _charState = GetComponent<CharacterState>();
        _animator = GetComponent<Animator>();

        // Ensure CharacterState knows its player index
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
        // 1. Check BOTH Brains for locks
        if (_machine.CurrentStateType == FighterStateType.Dead ||
            _machine.CurrentStateType == FighterStateType.Victory ||
            _charState.InHitFreeze)
            return;

        // 2. Route Inputs
        HandleMovementInput();
        HandleBlockInput();
        HandleCombatInput();

        // 3. Update Visuals
        UpdateFacingDirection();
        UpdateHurtbox();
        SyncAnimator();
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

        if (Input.GetKeyDown(keyJump) && _physics.IsGrounded)
        {
            _physics.RequestJump();
            _machine.TransitionTo(FighterStateType.JumpNeutral);
            _animator.SetTrigger("Jump");
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

    void HandleCombatInput()
    {
        if (_machine.IsStunned || _machine.IsAttacking) return;

        if (Input.GetKeyDown(keyLight)) StartAttack(moveLight, FighterStateType.LightPunch, hitboxLight, "LightAttack");
        else if (Input.GetKeyDown(keyMedium)) StartAttack(moveMedium, FighterStateType.MediumPunch, hitboxMedium, "MediumAttack");
        else if (Input.GetKeyDown(keyHeavy)) StartAttack(moveHeavy, FighterStateType.HeavyPunch, hitboxHeavy, "HeavyAttack");
        else if (Input.GetKeyDown(keySpecial)) StartAttack(moveSpecial, FighterStateType.SpecialMove, hitboxSpecial, "SpecialMove");
    }

    void StartAttack(MoveData move, FighterStateType state, GameObject hitbox, string trigger)
    {
        if (move == null) return;

        _currentMove = move;
        _currentHitbox = hitbox;

        // Route to Brain 1 (StateMachine)
        _machine.TransitionTo(state);

        // Dynamically set state duration based on frame data
        int duration = move.startupFrames + move.activeFrames + move.recoveryFrames;
        var method = state switch
        {
            FighterStateType.LightPunch => (System.Action)(() => _machine.GetState<LightPunchState>()?.SetDuration(duration)),
            FighterStateType.MediumPunch => () => _machine.GetState<MediumPunchState>()?.SetDuration(duration),
            FighterStateType.HeavyPunch => () => _machine.GetState<HeavyPunchState>()?.SetDuration(duration),
            FighterStateType.SpecialMove => () => _machine.GetState<SpecialMoveState>()?.SetDuration(duration),
            _ => () => { }
        };
        method();

        _animator.SetTrigger(trigger);
    }

    // --- The Cooperative Hit Handler ---
    void HandleHitConfirmed(HitEvent e)
    {
        if (e.defenderIndex != playerIndex || _machine.CurrentStateType == FighterStateType.Dead) return;

        bool isBlocked = e.result == HitResult.Blocked;

        // Brain 2 (CharacterState) handles the HP, Hit Freeze, and Combos
        _charState.TakeDamage(
            damage: e.damageDealt,
            hitstun: e.sourceBox.hitstunFrames,
            blockstun: e.sourceBox.blockstunFrames,
            knockback: e.sourceBox.knockback,
            causesKnockdown: e.sourceBox.damage >= 22,
            isBlocked: isBlocked,
            hitFreezeFrames: e.sourceBox.damage >= 22 ? 4 : 2
        );

        // Brain 1 (StateMachine) handles the Stun/Block Animations
        _machine.NotifyHitReceived(e);

        // Visuals
        hitFlash?.StartFlash();
        StartCoroutine(HitFreeze(e.sourceBox.damage >= 22 ? 4 : 2));
    }

    private IEnumerator HitFreeze(int frames)
    {
        Time.timeScale = 0.05f;
        for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
        Time.timeScale = 1f;
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
        FighterControllerSimple[] fighters = FindObjectsOfType<FighterControllerSimple>();
        foreach (var other in fighters)
        {
            if (other == this) continue;
            bool opponentRight = other.transform.position.x > transform.position.x;
            Facing = opponentRight ? FacingDirection.Right : FacingDirection.Left;
            transform.rotation = Quaternion.Euler(0f, Facing == FacingDirection.Right ? 0f : 180f, 0f);
        }
    }

    void SyncAnimator()
    {
        _animator.SetFloat("Speed", Mathf.Abs(_physics.Velocity.x));
        _animator.SetBool("IsGrounded", _physics.IsGrounded);
        _animator.SetBool("IsCrouching", IsCrouching);
        _animator.SetBool("IsBlocking", IsBlocking);
        _animator.SetBool("IsAirborne", _machine.IsAirborne);
    }
}