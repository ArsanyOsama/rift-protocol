using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum CharState
{
    Idle, WalkForward, WalkBack,
    CrouchIdle,
    JumpAscent, JumpApex, JumpDescent,
    DashForward, DashBack,
    AttackLight, AttackMedium, AttackHeavy,
    CrouchLight, CrouchMedium, CrouchHeavy,
    SpecialStartup, SpecialActive, SpecialRecovery,
    BlockStand, BlockCrouch,
    Hitstun, Blockstun, Knockdown, WakeUp,
    AirHitstun, Launch, KO, Victory
}

public struct HitData
{
    public float damage;
    public int hitFreeze;
    public float knockback;
    public bool causesKnockdown;
    public bool isCounterHit;
    public Vector3 contactPoint;
}

public class CharacterState : MonoBehaviour
{
    // ── Current state ────────────────────────
    public CharState current;
    public CharState previous;
    public int stateFrame;
    public bool facingRight;

    // ── Gameplay values ──────────────────────
    public float hp = 100f;
    public int hitstunFrames;
    public int blockstunFrames;
    public bool isAirborne;
    public bool isBlocking;
    public int comboCount;

    // ── Events ──────────────────────────────
    public UnityEvent<HitData> OnHit;
    public UnityEvent<HitData> OnHeavyHit;
    public UnityEvent OnKO;
    public UnityEvent<HitData> OnBlocked;
    public UnityEvent OnSpecialActivated;
    public UnityEvent<int> OnComboUpdate;

    // ── Reference to opponent ────────────────
    public CharacterState opponent;

    void Start()
    {
        current = CharState.Idle;
    }

    void Update()
    {
        stateFrame++;

        if (hitstunFrames > 0) hitstunFrames--;
        if (blockstunFrames > 0) blockstunFrames--;
    }
}
