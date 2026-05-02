// FighterControllerSimple.cs
// Assets/_Scripts/Fighter/FighterControllerSimple.cs
// SHORT-TERM DEMO VERSION — wires P4's working gameplay 
// into the PhysicsBody and StateMachine systems

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PhysicsBody))]
[RequireComponent(typeof(StateMachine))]
[RequireComponent(typeof(Animator))]
public class FighterControllerSimple : MonoBehaviour
{
    // ── Identity ──────────────────────────────────────────────────────────────
    [Header("Identity")]
    public int playerIndex = 0;          // 0 = P1, 1 = P2
    public enum CharacterType { Kael, Sira }
    public CharacterType characterType;

    // ── Move Data ─────────────────────────────────────────────────────────────
    [Header("Move Data (assign in Inspector)")]
    public MoveData moveLight;
    public MoveData moveMedium;
    public MoveData moveHeavy;
    public MoveData moveSpecial;

    // ── Input Keys ────────────────────────────────────────────────────────────
    [Header("Input Keys")]
    public KeyCode keyLeft = KeyCode.A;
    public KeyCode keyRight = KeyCode.D;
    public KeyCode keyCrouch = KeyCode.S;
    public KeyCode keyJump = KeyCode.W;
    public KeyCode keyLight = KeyCode.J;
    public KeyCode keyMedium = KeyCode.K;
    public KeyCode keyHeavy = KeyCode.L;
    public KeyCode keySpecial = KeyCode.U;

    // ── References ────────────────────────────────────────────────────────────
    private PhysicsBody _physics;
    private StateMachine _machine;
    private Animator _animator;

    // ── Runtime State ─────────────────────────────────────────────────────────
    public FacingDirection Facing { get; private set; } = FacingDirection.Right;
    public bool IsCrouching => _machine.CurrentStateType == FighterStateType.Crouching;
    public bool IsBlocking => _machine.IsBlocking;
    private MoveData _currentMove;

    // ── Events ────────────────────────────────────────────────────────────────
    public System.Action<int, int> OnHPChanged;  // (currentHP, maxHP)
    private int _maxHP = 100;
    private int _currentHP;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _physics = GetComponent<PhysicsBody>();
        _machine = GetComponent<StateMachine>();
        _animator = GetComponent<Animator>();

        _currentHP = _maxHP;

        // P2 sets character speed via PhysicsBody
        if (characterType == CharacterType.Kael)
            _physics.SetMoveInput(0f); // will be set each frame
        // (SIRA is naturally faster — handled by PhysicsBody _moveSpeed field)
    }

    void Start()
    {
        // Subscribe to CollisionManager hit events
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnHitConfirmed += HandleHitConfirmed;

        // Set starting facing direction
        Facing = playerIndex == 0 ? FacingDirection.Right : FacingDirection.Left;
    }

    void OnDestroy()
    {
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnHitConfirmed -= HandleHitConfirmed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (_machine.CurrentStateType == FighterStateType.Dead) return;

        HandleMovementInput();
        HandleCombatInput();
        UpdateFacingDirection();
        SyncAnimator();
    }

    // ── Movement ──────────────────────────────────────────────────────────────
    void HandleMovementInput()
    {
        if (_machine.IsStunned || _machine.IsAttacking)
        {
            _physics.SetMoveInput(0f);
            return;
        }

        float input = 0f;
        if (Input.GetKey(keyRight)) input = +1f;
        if (Input.GetKey(keyLeft)) input = -1f;
        _physics.SetMoveInput(input);

        if (Input.GetKey(keyCrouch) && _physics.IsGrounded)
            _machine.TransitionTo(FighterStateType.Crouching);
        else if (_machine.CurrentStateType == FighterStateType.Crouching && !Input.GetKey(keyCrouch))
            _machine.TransitionTo(FighterStateType.Idle);

        if (Input.GetKeyDown(keyJump) && _physics.IsGrounded)
            _physics.RequestJump();
    }

    // ── Combat ────────────────────────────────────────────────────────────────
    void HandleCombatInput()
    {
        if (_machine.IsStunned || _machine.IsAttacking) return;

        if (Input.GetKeyDown(keyLight)) StartAttack(moveLight, FighterStateType.LightPunch, "LightAttack");
        if (Input.GetKeyDown(keyMedium)) StartAttack(moveMedium, FighterStateType.MediumPunch, "MediumAttack");
        if (Input.GetKeyDown(keyHeavy)) StartAttack(moveHeavy, FighterStateType.HeavyPunch, "HeavyAttack");
        if (Input.GetKeyDown(keySpecial)) StartAttack(moveSpecial, FighterStateType.SpecialMove, "SpecialMove");
    }

    void StartAttack(MoveData move, FighterStateType state, string triggerName)
    {
        if (move == null) return;
        _currentMove = move;
        _machine.GetState<AttackingState>()?.SetDuration(
            move.startupFrames + move.activeFrames + move.recoveryFrames);
        _machine.TransitionTo(state);
        _animator.SetTrigger(triggerName);
    }

    // ── Called by Animation Event on impact frame ─────────────────────────────
    public void OnAttackImpactFrame()
    {
        // CollisionManager handles the detection — this just enables the hitbox
        // For the demo, the hitbox GameObjects are enabled/disabled here
        // In the full system, this fires through Animation Events on the Animator
    }

    // ── Hit Received ──────────────────────────────────────────────────────────
    void HandleHitConfirmed(HitEvent e)
    {
        if (e.defenderIndex != playerIndex) return;
        if (_machine.CurrentStateType == FighterStateType.Dead) return;

        // Apply damage
        _currentHP = Mathf.Max(0, _currentHP - e.damageDealt);
        OnHPChanged?.Invoke(_currentHP, _maxHP);

        // Notify state machine
        _machine.NotifyHitReceived(e);

        // Apply knockback via PhysicsBody
        float knockDir = e.attackerIndex == 0 ? 1f : -1f;
        if (_currentMove != null)
            _physics.SetVelocity(new Vector2(knockDir * _currentMove.knockback * 3f,
                                             _currentMove.causesKnockdown ? 2f : 0f));

        // Hit freeze
        StartCoroutine(HitFreezeCoroutine(e.sourceBox.hitstunFrames > 0
            ? (_currentMove?.hitFreezeFrames ?? 2) : 2));

        // Death check
        if (_currentHP <= 0)
            _machine.ForceTransition(FighterStateType.Dead);
    }

    IEnumerator HitFreezeCoroutine(int frames)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(frames / 60f);
        Time.timeScale = 1f;
    }

    // ── Facing Direction ──────────────────────────────────────────────────────
    void UpdateFacingDirection()
    {
        // Find opponent and face them
        FighterControllerSimple[] fighters =
            FindObjectsOfType<FighterControllerSimple>();
        foreach (var other in fighters)
        {
            if (other == this) continue;
            bool opponentIsRight = other.transform.position.x > transform.position.x;
            Facing = opponentIsRight ? FacingDirection.Right : FacingDirection.Left;
            transform.rotation = Quaternion.Euler(0, Facing == FacingDirection.Right ? 0 : 180, 0);
        }
    }

    // ── Animator Sync ─────────────────────────────────────────────────────────
    void SyncAnimator()
    {
        _animator.SetFloat("Speed", Mathf.Abs(_physics.Velocity.x));
        _animator.SetBool("IsGrounded", _physics.IsGrounded);
        _animator.SetBool("IsCrouching", IsCrouching);
        _animator.SetBool("IsBlocking", IsBlocking);
    }

    // Public getter for UI
    public int CurrentHP => _currentHP;
    public int MaxHP => _maxHP;
}