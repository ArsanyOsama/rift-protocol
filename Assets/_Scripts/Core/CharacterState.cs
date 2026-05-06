using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum CharState
{
    Idle, WalkForward, WalkBack,
    JumpNeutral, JumpForward, JumpBack, Airborne,
    CrouchTransition, Crouching,
    DashForward, DashBack,
    LightAttack, MediumAttack, HeavyAttack,
    CrouchLightAttack, CrouchMediumAttack, CrouchHeavyAttack,
    AirLightAttack, AirMediumAttack, AirHeavyAttack,
    SpecialMove,
    BlockingStanding, BlockingCrouching,
    HitStunStanding, HitStunCrouching,
    KnockdownFalling, KnockdownGround, WakeUp,
    LaunchState,    // airborne, vulnerable to air juggle
    Dead, Victory
}



[RequireComponent(typeof(PhysicsBody))]
public class CharacterState : MonoBehaviour
{
    // ── Current State ─────────────────────────────────────────
    public CharState CurrentState { get; private set; } = CharState.Idle;
    public CharState PreviousState { get; private set; } = CharState.Idle;

    // ── Health ─────────────────────────────────────────────────
    [Header("Health")]
    public int maxHP = 200;
    public int currentHP;
    public event Action<int, int> OnHPChanged;   // (current, max)
    public event Action<int> OnDied;           // playerIndex

    // ── Facing ─────────────────────────────────────────────────
    public FacingDirection Facing { get; private set; } = FacingDirection.Right;

    // ── Frame Counters ─────────────────────────────────────────
    public int hitstunFramesRemaining { get; private set; }
    public int blockstunFramesRemaining { get; private set; }
    public int hitFreezeFramesRemaining { get; private set; }
    public bool InHitFreeze => hitFreezeFramesRemaining > 0;
    public bool InHitStun => hitstunFramesRemaining > 0;

    // ── Combo ──────────────────────────────────────────────────
    public int comboCount { get; private set; }
    public event Action<int> OnComboUpdated;

    // ── Player Identity ────────────────────────────────────────
    [Header("Identity")]
    public int playerIndex = 0;   // 0 = P1, 1 = P2

    // ── Internal ───────────────────────────────────────────────
    private PhysicsBody _body;
    private Animator _animator;
    private bool _dead;

    void Awake()
    {
        currentHP = maxHP;
        _body = GetComponent<PhysicsBody>();
        _animator = GetComponent<Animator>();
    }

    // ── Transition ─────────────────────────────────────────────
    public void ForceTransition(CharState next)
    {
        if (CurrentState == next) return;
        PreviousState = CurrentState;
        CurrentState = next;
    }

    // ── Take Damage (called by CollisionManager via GameManager) ─
    public void TakeDamage(int damage, int hitstun, int blockstun,
                           float knockback, bool causesKnockdown,
                           bool isBlocked, int hitFreezeFrames = 2)
    {
        if (_dead) return;

        hitFreezeFramesRemaining = hitFreezeFrames;

        if (isBlocked)
        {
            int chip = Mathf.RoundToInt(damage * 0.2f);
            currentHP = Mathf.Max(1, currentHP - chip);
            blockstunFramesRemaining = blockstun;
            ForceTransition(CurrentState == CharState.BlockingCrouching
                ? CharState.BlockingCrouching
                : CharState.BlockingStanding);
        }
        else
        {
            currentHP -= damage;
            hitstunFramesRemaining = hitstun;

            if (causesKnockdown)
            {
                ForceTransition(CharState.KnockdownFalling);
                _body?.ApplyKnockback(knockback);
            }
            else if (_body != null && !_body.IsGrounded)
            {
                ForceTransition(CharState.LaunchState);
            }
            else
            {
                ForceTransition(CharState.HitStunStanding);
                _body?.ApplyKnockback(knockback);
            }
        }

        OnHPChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0) Die();
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;
        currentHP = 0;
        ForceTransition(CharState.Dead);
        OnDied?.Invoke(playerIndex);
    }

    // ── Fixed Update — decrement frame counters ────────────────
    void FixedUpdate()
    {
        if (hitFreezeFramesRemaining > 0) hitFreezeFramesRemaining--;
        if (hitstunFramesRemaining > 0)
        {
            hitstunFramesRemaining--;
            if (hitstunFramesRemaining == 0 && CurrentState == CharState.HitStunStanding)
                ForceTransition(CharState.Idle);
        }
        if (blockstunFramesRemaining > 0)
        {
            blockstunFramesRemaining--;
            if (blockstunFramesRemaining == 0)
                ForceTransition(CharState.Idle);
        }
    }

    // ── Combo Tracking ─────────────────────────────────────────
    public void IncrementCombo() { comboCount++; OnComboUpdated?.Invoke(comboCount); }
    public void ResetCombo() { comboCount = 0; OnComboUpdated?.Invoke(0); }

    // ── Full HP Reset (called between rounds) ─────────────────
    public void ResetToFull()
    {
        _dead = false;
        currentHP = maxHP;
        hitstunFramesRemaining = blockstunFramesRemaining = hitFreezeFramesRemaining = 0;
        comboCount = 0;
        ForceTransition(CharState.Idle);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // ── Facing update (called by MovementController) ──────────
    public void SetFacing(FacingDirection dir) => Facing = dir;
}
