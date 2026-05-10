// ════════════════════════════════════════════════════════════
// FILE 7 — FighterAnimationController.cs  (FULL REPLACEMENT)
// Assets\Animations\FighterAnimationController.cs
// This replaces the broken file entirely. Uses ONLY existing APIs.
// ════════════════════════════════════════════════════════════

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

/// <summary>
/// FighterAnimationController — sits at Assets/Animations/
/// Drives all animation, SFX, and VFX by listening to events from
/// CharacterState, MoveExecutor, and CollisionManager.
/// All dependencies reference only APIs that actually exist.
/// </summary>
[RequireComponent(typeof(Animator))]
public class FighterAnimationController : MonoBehaviour
{
    // ── Inspector refs ────────────────────────────────────────
    [Header("Fighter Components")]
    [SerializeField] private FighterControllerSimple _fighter;
    [SerializeField] private CharacterState _charState;
    [SerializeField] private PhysicsBody _body;
    [SerializeField] private MoveExecutor _executor;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _footstepClip;
    [SerializeField] private AudioClip _landClip;
    [SerializeField] private AudioClip _blockClip;
    [SerializeField] private AudioClip _hitClip;
    [SerializeField] private AudioClip _koClip;
    [SerializeField] private AudioClip _victoryClip;

    [Header("VFX")]
    [SerializeField] private HitFlashController _hitFlash;
    [SerializeField] private TrailRenderer _comboTrail;

    [Header("Camera")]
    [SerializeField] private CinemachineImpulseSource _impulse;

    // ── Animator hash cache (faster than string lookups) ─────
    private static readonly int _hashSpeed = Animator.StringToHash("Speed");
    private static readonly int _hashGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int _hashCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int _hashBlocking = Animator.StringToHash("IsBlocking");
    private static readonly int _hashAirborne = Animator.StringToHash("IsAirborne");
    private static readonly int _hashCombo = Animator.StringToHash("ComboCount");
    private static readonly int _hashHitStun = Animator.StringToHash("HitStun");
    private static readonly int _hashKnockdown = Animator.StringToHash("Knockdown");
    private static readonly int _hashWakeUp = Animator.StringToHash("WakeUp");
    private static readonly int _hashVictory = Animator.StringToHash("Victory");
    private static readonly int _hashJump = Animator.StringToHash("Jump");
    private static readonly int _hashDash = Animator.StringToHash("Dash");

    // Trigger hashes for attacks
    private static readonly int _hashLightAttack = Animator.StringToHash("LightAttack");
    private static readonly int _hashMediumAttack = Animator.StringToHash("MediumAttack");
    private static readonly int _hashHeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int _hashSpecialMove = Animator.StringToHash("SpecialMove");
    private static readonly int _hashGrab = Animator.StringToHash("Grab");
    private static readonly int _hashTaunt = Animator.StringToHash("Taunt");

    // ── Internal state ────────────────────────────────────────
    private Animator _anim;
    private CharState _prevCharState;
    private int _currentCombo;

    // ── Read-only property for InputBuffer ───────────────────
    public bool FacingRight => _charState != null
        ? _charState.Facing == FacingDirection.Right
        : true;

    // ─────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        _anim = GetComponent<Animator>();

        // Auto-resolve on same GameObject if not assigned in Inspector
        if (_fighter == null) _fighter = GetComponent<FighterControllerSimple>();
        if (_charState == null) _charState = GetComponent<CharacterState>();
        if (_body == null) _body = GetComponent<PhysicsBody>();
        if (_executor == null) _executor = GetComponent<MoveExecutor>();
    }

    void OnEnable()
    {
        SubscribeEvents();
    }

    // [FIX 5D] Defer CollisionManager subscription to Start so Instance exists
    void Start()
    {
        if (CollisionManager.Instance != null)
        {
            CollisionManager.Instance.OnHitConfirmed += HandleHitConfirmed;
        }
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    // [FIX 5D] Defer CollisionManager unsubscription to OnDestroy
    void OnDestroy()
    {
        if (CollisionManager.Instance != null)
        {
            CollisionManager.Instance.OnHitConfirmed -= HandleHitConfirmed;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  EVENT WIRING
    // ─────────────────────────────────────────────────────────

    void SubscribeEvents()
    {
        // CharacterState events
        if (_charState != null)
        {
            _charState.OnStateChanged += HandleStateChanged;
            _charState.OnBlockStateChange += HandleBlockStateChange;
            _charState.OnHPChanged += HandleHPChanged;
        }

        // MoveExecutor events
        if (_executor != null)
        {
            _executor.OnAttackStarted += HandleAttackStarted;
            _executor.OnSpecialActivated += HandleSpecialActivated;
            _executor.OnComboUpdate += HandleComboUpdate;
            _executor.OnDashStarted += HandleDashStarted;
            _executor.OnJumpStarted += HandleJumpStarted;
            _executor.OnGrabAttempted += HandleGrabAttempted;
            _executor.OnTaunt += HandleTaunt;
        }
    }

    void UnsubscribeEvents()
    {
        if (_charState != null)
        {
            _charState.OnStateChanged -= HandleStateChanged;
            _charState.OnBlockStateChange -= HandleBlockStateChange;
            _charState.OnHPChanged -= HandleHPChanged;
        }

        if (_executor != null)
        {
            _executor.OnAttackStarted -= HandleAttackStarted;
            _executor.OnSpecialActivated -= HandleSpecialActivated;
            _executor.OnComboUpdate -= HandleComboUpdate;
            _executor.OnDashStarted -= HandleDashStarted;
            _executor.OnJumpStarted -= HandleJumpStarted;
            _executor.OnGrabAttempted -= HandleGrabAttempted;
            _executor.OnTaunt -= HandleTaunt;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  UPDATE LOOP — drives continuous animator params
    // ─────────────────────────────────────────────────────────

    void Update()
    {
        if (_body == null || _anim == null) return;

        // Speed (for walk blend tree)
        float speed = _body.Velocity.magnitude;
        _anim.SetFloat(_hashSpeed, speed);

        // Ground / air state
        bool grounded = _body.IsGrounded;
        _anim.SetBool(_hashGrounded, grounded);

        // Airborne
        bool airborne = !grounded;
        _anim.SetBool(_hashAirborne, airborne);

        // Crouching / blocking (driven by CharState, set by events — just sync here as fallback)
        if (_charState != null)
        {
            _anim.SetBool(_hashCrouching,
                _charState.CurrentState == CharState.Crouching ||
                _charState.CurrentState == CharState.CrouchTransition);

            _anim.SetBool(_hashBlocking,
                _charState.CurrentState == CharState.BlockingStanding ||
                _charState.CurrentState == CharState.BlockingCrouching);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  EVENT HANDLERS
    // ─────────────────────────────────────────────────────────

    void HandleStateChanged(CharState newState)
    {
        switch (newState)
        {
            case CharState.HitStunStanding:
            case CharState.HitStunCrouching:
                _anim.SetTrigger(_hashHitStun);
                PlayClip(_hitClip);
                break;

            case CharState.KnockdownFalling:
                _anim.SetTrigger(_hashKnockdown);
                break;

            case CharState.WakeUp:
                _anim.SetTrigger(_hashWakeUp);
                break;

            case CharState.Victory:
                _anim.SetTrigger(_hashVictory);
                PlayClip(_victoryClip);
                break;

            case CharState.Dead:
                // KO handled by HandleHPChanged reaching 0
                break;

            case CharState.JumpNeutral:
            case CharState.JumpForward:
            case CharState.JumpBack:
                _anim.SetTrigger(_hashJump);
                break;

            case CharState.DashForward:
            case CharState.DashBack:
                _anim.SetTrigger(_hashDash);
                break;

            case CharState.BlockingStanding:
            case CharState.BlockingCrouching:
                PlayClip(_blockClip);
                break;
        }

        // Combo trail: show when 3+ hit combo active
        if (_comboTrail != null)
            _comboTrail.emitting = (_currentCombo >= 3);
    }

    void HandleBlockStateChange(bool isBlocking)
    {
        _anim.SetBool(_hashBlocking, isBlocking);
    }

    void HandleHPChanged(int current, int max)
    {
        if (current <= 0)
        {
            // KO sequence
            PlayClip(_koClip);
            // [FIXED] Use FindObjectOfType
            FindObjectOfType<ScreenFlashController>()?.KOFlash();
        }
    }

    void HandleHitConfirmed(HitEvent e)
    {
        // Only react when WE are the defender
        if (_fighter == null || e.defenderIndex != _fighter.playerIndex) return;

        _hitFlash?.StartFlash();

        // Camera shake scaled to attack DAMAGE
        if (_impulse != null)
        {
            float force = 0.05f; // Default for Light hits

            if (e.sourceBox.damage >= 18)
                force = 0.25f; // Heavy hit
            else if (e.sourceBox.damage >= 12)
                force = 0.12f; // Medium hit

            _impulse.GenerateImpulse(new Vector3(force, force * 0.5f, 0f));
        }
    }

    void HandleAttackStarted(MoveData move)
    {
        if (move == null) return;

        int trigger = move.attackType switch
        {
            AttackType.Punch => _hashLightAttack,
            AttackType.Kick => _hashMediumAttack,
            AttackType.Special => _hashSpecialMove,
            AttackType.Grab => _hashGrab,
            _ => _hashHeavyAttack
        };
        _anim.SetTrigger(trigger);

        if (move.startupSFX != null) PlayClip(move.startupSFX);
    }

    void HandleSpecialActivated(MoveData move)
    {
        if (move == null) return;

        _anim.SetTrigger(_hashSpecialMove);

        // [FIXED] Use FindObjectOfType
        FindObjectOfType<ScreenFlashController>()?.Flash(new Color(0.2f, 0.6f, 1f), 0.1f);

        if (move.specialSFX != null) PlayClip(move.specialSFX);
        if (move.screenShakeForce > 0f && _impulse != null)
            _impulse.GenerateImpulse(Vector3.one * move.screenShakeForce);

        // [FIXED] Commented out missing VFX call
        // HitEffectsManager.Instance?.SpawnSpecialActivationVFX(transform.position, _fighter != null ? _fighter.playerIndex : 0);
    }

    void HandleComboUpdate(int count)
    {
        _currentCombo = count;
        _anim.SetInteger(_hashCombo, count);

        if (_comboTrail != null)
            _comboTrail.emitting = count >= 3;
    }

    void HandleDashStarted(DashDirection dir)
    {
        _anim.SetTrigger(_hashDash);
        _anim.SetBool("DashForward", dir == DashDirection.Forward);
    }

    void HandleJumpStarted()
    {
        _anim.SetTrigger(_hashJump);
    }

    void HandleGrabAttempted()
    {
        _anim.SetTrigger(_hashGrab);
    }

    void HandleTaunt()
    {
        _anim.SetTrigger(_hashTaunt);
    }

    // ─────────────────────────────────────────────────────────
    //  ANIMATION EVENT CALLBACKS
    //  (called from Unity animation clip keyframes)
    // ─────────────────────────────────────────────────────────

    /// Called on the frame a hitbox becomes active
    public void OnHitboxActive()
        => _fighter?.OnAttackImpactFrame();

    /// Called when the hitbox deactivates
    public void OnHitboxInactive()
        => _fighter?.OnAttackEnd();

    /// Called on footstep frames
    public void OnFootstep()
        => PlayClip(_footstepClip);

    /// Called on landing frame
    public void OnLand()
        => PlayClip(_landClip);

    /// Called when wakeup animation finishes
    public void OnWakeUpComplete()
        => _charState?.NotifyWakeUp();

    /// Called when attack recovery animation ends
    public void OnAttackRecoveryEnd()
        => _charState?.NotifyAttackRecoveryEnd();

    /// Called by special move animation to spawn projectile
    public void OnSpawnProjectile()
        => _executor?.SpawnProjectile();

    // ─────────────────────────────────────────────────────────
    //  UTILITY
    // ─────────────────────────────────────────────────────────

    void PlayClip(AudioClip clip)
    {
        if (_audioSource == null || clip == null) return;
        _audioSource.PlayOneShot(clip);
    }
}