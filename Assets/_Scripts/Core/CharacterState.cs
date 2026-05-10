using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    LaunchState,
    Dead, Victory
}

[RequireComponent(typeof(PhysicsBody))]
public class CharacterState : MonoBehaviour
{
    public CharState CurrentState { get; private set; } = CharState.Idle;
    public CharState PreviousState { get; private set; } = CharState.Idle;

    [Header("Health")]
    public int maxHP = 200;
    public int currentHP;

    public event Action<int, int> OnHPChanged;
    public event Action<int> OnDied;

    public event Action<CharState> OnStateChanged;
    public event Action<bool> OnBlockStateChange;

    public FacingDirection Facing { get; private set; } = FacingDirection.Right;

    public int hitstunFramesRemaining { get; private set; }
    public int blockstunFramesRemaining { get; private set; }
    public int hitFreezeFramesRemaining { get; private set; }
    public bool InHitFreeze => hitFreezeFramesRemaining > 0;
    public bool InHitStun => hitstunFramesRemaining > 0;

    public int comboCount { get; private set; }
    public event Action<int> OnComboUpdated;

    [Header("Identity")]
    public int playerIndex = 0;

    private PhysicsBody _body;
    private Animator _animator;
    private StateMachine _machine; // [FIX 1] Added StateMachine Reference
    private bool _dead;
    private bool _wasBlocking;

    void Awake()
    {
        currentHP = maxHP;
        _body = GetComponent<PhysicsBody>();
        _animator = GetComponent<Animator>();
        _machine = GetComponent<StateMachine>(); // [FIX 1] Get Reference

        // [FIX 1] When HP hits 0, drive StateMachine to Dead
        OnDied += (idx) => _machine?.ForceTransition(FighterStateType.Dead);
    }

    public void ForceTransition(CharState next)
    {
        if (CurrentState == next) return;
        PreviousState = CurrentState;
        CurrentState = next;
        OnStateChanged?.Invoke(CurrentState);

        bool isNowBlocking = (next == CharState.BlockingStanding || next == CharState.BlockingCrouching);
        if (isNowBlocking != _wasBlocking)
        {
            OnBlockStateChange?.Invoke(isNowBlocking);
            _wasBlocking = isNowBlocking;
        }
    }

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

    // ADD this method to CharacterState.cs — called by GameManager on round start
    public void ResetHP()
    {
        _dead = false;
        currentHP = maxHP;
        OnHPChanged?.Invoke(currentHP, maxHP);
        ForceTransition(CharState.Idle);
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;
        currentHP = 0;
        ForceTransition(CharState.Dead);
        OnDied?.Invoke(playerIndex);
    }

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

    public void IncrementCombo() { comboCount++; OnComboUpdated?.Invoke(comboCount); }
    public void ResetCombo() { comboCount = 0; OnComboUpdated?.Invoke(0); }

    public void ResetToFull()
    {
        _dead = false;
        currentHP = maxHP;
        hitstunFramesRemaining = blockstunFramesRemaining = hitFreezeFramesRemaining = 0;
        comboCount = 0;
        ForceTransition(CharState.Idle);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void SetFacing(FacingDirection dir) => Facing = dir;

    public void NotifyWakeUp() => ForceTransition(CharState.WakeUp);
    public void NotifyAttackRecoveryEnd() { /* recovery end */ }
}