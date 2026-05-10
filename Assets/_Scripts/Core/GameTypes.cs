using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// GameTypes.cs — All shared enums and structs used across systems
// Assets/_Scripts/Core/GameTypes.cs

public enum FacingDirection { Left, Right }

public enum AttackType { Punch, Kick, Special, Grab, Taunt }

public enum BoxType { Hitbox, Hurtbox, Pushbox, ProximityGuard }

public enum HitType { High, Mid, Low, Overhead, Projectile, Grab }

public enum HitResult { Hit, Blocked, Trade, Whiff }

public enum DashDirection { Forward, Backward }

public enum FighterStateType
{
    Idle, WalkForward, WalkBackward, Crouching,
    JumpNeutral, JumpForward, JumpBackward, Falling,
    LightPunch, MediumPunch, HeavyPunch,
    LightKick, MediumKick, HeavyKick,
    AirLightPunch, AirHeavyKick,
    SpecialMove,
    BlockingStanding, BlockingCrouching,
    HitStunStanding, HitStunCrouching,
    KnockdownFalling, KnockdownGround, WakeUp,
    Dead, Victory
}

[System.Serializable]
public struct BoxData
{
    public BoxType type;
    public HitType hitType;
    public Vector2 offset;
    public Vector2 size;
    public int damage;
    public int hitstunFrames;
    public int blockstunFrames;
    public float knockback;
    public bool hasConnected;

    public BoxData(BoxType type, Vector2 offset, Vector2 size,
                   int damage = 0, HitType hitType = HitType.Mid,
                   int hitstun = 0, int blockstun = 0, float knockback = 0f)
    {
        this.type = type;
        this.offset = offset;
        this.size = size;
        this.damage = damage;
        this.hitType = hitType;
        this.hitstunFrames = hitstun;
        this.blockstunFrames = blockstun;
        this.knockback = knockback;
        this.hasConnected = false;
    }

    public Rect GetWorldRect(Vector2 origin, FacingDirection facing)
    {
        float flipX = facing == FacingDirection.Right ? 1f : -1f;
        Vector2 worldOffset = new Vector2(offset.x * flipX, offset.y);
        Vector2 worldPos = origin + worldOffset;
        return new Rect(worldPos.x - size.x * 0.5f,
                        worldPos.y - size.y * 0.5f,
                        size.x, size.y);
    }
}

public struct HitEvent
{
    public HitResult result;
    public int attackerIndex;
    public int defenderIndex;
    public BoxData sourceBox;
    public Vector2 contactPoint;
    public int damageDealt;
}