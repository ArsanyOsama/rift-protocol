// Assets/Scripts/Core/StateMachine.cs

using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  BASE CLASS
// ─────────────────────────────────────────────────────────────────────────────
public abstract class FighterStateBase
{
    protected StateMachine Machine { get; private set; }
    protected PhysicsBody Physics { get; private set; }
    protected FighterControllerSimple Fighter { get; private set; }

    public abstract FighterStateType StateType { get; }

    public void Init(StateMachine machine, PhysicsBody physics, FighterControllerSimple fighter)
    {
        Machine = machine;
        Physics = physics;
        Fighter = fighter;
    }

    public virtual void OnEnter(FighterStateType previousState) { }
    public virtual void OnUpdate(float deltaTime) { }
    public virtual void OnExit(FighterStateType nextState) { }
    public virtual void OnHitReceived(HitEvent hitEvent) { }
}

// ─────────────────────────────────────────────────────────────────────────────
//  MOVEMENT STATES
// ─────────────────────────────────────────────────────────────────────────────
public class IdleState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Idle;

    public override void OnEnter(FighterStateType previousState) { Physics.StopHorizontal(); }

    public override void OnHitReceived(HitEvent hitEvent)
    {
        Machine.TransitionTo(hitEvent.result == HitResult.Blocked ? FighterStateType.BlockingStanding : FighterStateType.HitStunStanding);
    }
}

public class WalkForwardState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.WalkForward;

    public override void OnUpdate(float deltaTime)
    {
        if (Mathf.Abs(Physics.Velocity.x) < 0.05f) Machine.TransitionTo(FighterStateType.Idle);
        if (!Physics.IsGrounded) Machine.TransitionTo(FighterStateType.Falling);
    }

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(e.result == HitResult.Blocked ? FighterStateType.BlockingStanding : FighterStateType.HitStunStanding);
    }
}

public class WalkBackwardState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.WalkBackward;

    public override void OnUpdate(float deltaTime)
    {
        if (Mathf.Abs(Physics.Velocity.x) < 0.05f) Machine.TransitionTo(FighterStateType.Idle);
        if (!Physics.IsGrounded) Machine.TransitionTo(FighterStateType.Falling);
    }

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(e.result == HitResult.Blocked ? FighterStateType.BlockingStanding : FighterStateType.HitStunStanding);
    }
}

public class CrouchingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Crouching;

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(e.result == HitResult.Blocked ? FighterStateType.BlockingCrouching : FighterStateType.HitStunCrouching);
    }
}

public class JumpNeutralState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.JumpNeutral;

    public override void OnUpdate(float deltaTime)
    {
        if (Physics.Velocity.y < 0f) Machine.TransitionTo(FighterStateType.Falling);
        if (Physics.IsGrounded) Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e) { Machine.TransitionTo(FighterStateType.KnockdownFalling); }
}

public class FallingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Falling;

    public override void OnUpdate(float deltaTime)
    {
        if (Physics.IsGrounded) Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e) { Machine.TransitionTo(FighterStateType.KnockdownFalling); }
}

// ─────────────────────────────────────────────────────────────────────────────
// ATTACK STATES 
// ─────────────────────────────────────────────────────────────────────────────
public abstract class TimedAttackState : FighterStateBase
{
    protected int _totalDuration;
    protected int _frameCounter;

    public void SetDuration(int frames) => _totalDuration = frames;

    public override void OnEnter(FighterStateType previousState)
    {
        _frameCounter = 0; // Reset the stopwatch every time an attack starts!
        Physics.StopHorizontal();
    }

    public override void OnUpdate(float deltaTime)
    {
        _frameCounter++;

        // 1. THE LANDING CANCEL MECHANIC
        if ((StateType == FighterStateType.AirLightPunch || StateType == FighterStateType.AirHeavyKick) && Physics.IsGrounded)
        {
            Machine.TransitionTo(FighterStateType.Idle);
            return;
        }

        // 2. NORMAL RECOVERY
        if (_frameCounter >= _totalDuration)
        {
            Machine.TransitionTo(Physics.IsGrounded ? FighterStateType.Idle : FighterStateType.Falling);
        }
    }

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(FighterStateType.HitStunStanding);
    }
}

public class LightPunchState : TimedAttackState { public override FighterStateType StateType => FighterStateType.LightPunch; }
public class MediumPunchState : TimedAttackState { public override FighterStateType StateType => FighterStateType.MediumPunch; }
public class HeavyPunchState : TimedAttackState { public override FighterStateType StateType => FighterStateType.HeavyPunch; }
public class LightKickState : TimedAttackState { public override FighterStateType StateType => FighterStateType.LightKick; }
public class MediumKickState : TimedAttackState { public override FighterStateType StateType => FighterStateType.MediumKick; }
public class HeavyKickState : TimedAttackState { public override FighterStateType StateType => FighterStateType.HeavyKick; }
public class SpecialMoveState : TimedAttackState { public override FighterStateType StateType => FighterStateType.SpecialMove; }
public class AirLightPunchState : TimedAttackState { public override FighterStateType StateType => FighterStateType.AirLightPunch; }
public class AirHeavyKickState : TimedAttackState { public override FighterStateType StateType => FighterStateType.AirHeavyKick; }

// ─────────────────────────────────────────────────────────────────────────────
// REACTIONS & TERMINAL STATES
// ─────────────────────────────────────────────────────────────────────────────
public class HitStunStandingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.HitStunStanding;
    private int _stunFramesRemaining;

    public void SetStunDuration(int frames) => _stunFramesRemaining = frames;

    public override void OnEnter(FighterStateType previousState) { Physics.StopHorizontal(); }

    public override void OnUpdate(float deltaTime)
    {
        _stunFramesRemaining--;
        if (_stunFramesRemaining <= 0) Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e) { SetStunDuration(e.sourceBox.hitstunFrames); }
}

public class HitStunCrouchingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.HitStunCrouching;
    private int _stunFramesRemaining;

    public void SetStunDuration(int frames) => _stunFramesRemaining = frames;

    public override void OnUpdate(float deltaTime)
    {
        _stunFramesRemaining--;
        if (_stunFramesRemaining <= 0) Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e) { SetStunDuration(e.sourceBox.hitstunFrames); }
}

public class BlockingStandingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.BlockingStanding;
    private int _blockstunRemaining;

    public void SetBlockstunDuration(int frames) => _blockstunRemaining = frames;

    public override void OnEnter(FighterStateType previousState) { Physics.StopHorizontal(); }

    public override void OnUpdate(float deltaTime)
    {
        _blockstunRemaining--;
        if (_blockstunRemaining <= 0) Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e)
    {
        if (e.result == HitResult.Blocked) SetBlockstunDuration(e.sourceBox.blockstunFrames);
        else Machine.TransitionTo(FighterStateType.HitStunStanding);
    }
}

public class BlockingCrouchingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.BlockingCrouching;
    private int _blockstunRemaining;

    public void SetBlockstunDuration(int frames) => _blockstunRemaining = frames;

    public override void OnUpdate(float deltaTime)
    {
        _blockstunRemaining--;
        if (_blockstunRemaining <= 0) Machine.TransitionTo(FighterStateType.Crouching);
    }

    public override void OnHitReceived(HitEvent e)
    {
        if (e.result == HitResult.Blocked) SetBlockstunDuration(e.sourceBox.blockstunFrames);
        else Machine.TransitionTo(FighterStateType.HitStunCrouching);
    }
}

public class KnockdownFallingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.KnockdownFalling;

    public override void OnUpdate(float deltaTime)
    {
        if (Physics.IsGrounded) Machine.TransitionTo(FighterStateType.KnockdownGround);
    }
}

public class KnockdownGroundState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.KnockdownGround;
    [SerializeField] private int _wakeupFrames = 45;
    private int _frameCounter;

    public override void OnEnter(FighterStateType previousState)
    {
        _frameCounter = 0;
        Physics.StopAll();
    }

    public override void OnUpdate(float deltaTime)
    {
        _frameCounter++;
        if (_frameCounter >= _wakeupFrames) Machine.TransitionTo(FighterStateType.WakeUp);
    }
}

public class WakeUpState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.WakeUp;
    private readonly int _wakeupDuration = 10;
    private int _frameCounter;

    public override void OnEnter(FighterStateType previousState) => _frameCounter = 0;

    public override void OnUpdate(float deltaTime)
    {
        _frameCounter++;
        if (_frameCounter >= _wakeupDuration) Machine.TransitionTo(FighterStateType.Idle);
    }
}

public class DeadState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Dead;

    public override void OnEnter(FighterStateType previousState)
    {
        Physics.StopAll();
        StateMachine.RaiseFighterDied(Fighter.playerIndex);
    }
}

public class VictoryState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Victory;

    public override void OnEnter(FighterStateType previousState)
    {
        Physics.StopAll();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  THE STATE MACHINE
// ─────────────────────────────────────────────────────────────────────────────
public class StateMachine : MonoBehaviour
{
    public static event Action<int> OnFighterDied;
    public static void RaiseFighterDied(int playerIndex) => OnFighterDied?.Invoke(playerIndex);

    [Header("Starting state")]
    [SerializeField] private FighterStateType _initialState = FighterStateType.Idle;

    public FighterStateType CurrentStateType { get; private set; }
    public int FramesInCurrentState { get; private set; }
    public FighterStateType PreviousStateType { get; private set; }

    private FighterStateBase _currentState;
    private Dictionary<FighterStateType, FighterStateBase> _states;

    private PhysicsBody _physics;
    private FighterControllerSimple _fighter;
    private CharacterState _charState;

    private void Awake()
    {
        _physics = GetComponent<PhysicsBody>();
        _fighter = GetComponent<FighterControllerSimple>();
        _charState = GetComponent<CharacterState>();

        BuildStateRegistry();
    }

    private void Start() { ForceTransition(_initialState); }

    private void FixedUpdate()
    {
        _currentState?.OnUpdate(Time.fixedDeltaTime);
        FramesInCurrentState++;
    }

    // REPLACE the entire TakeDamage method in StateMachine.cs with:
    public void TakeDamage(int amount)
    {
        // Delegate entirely — CharacterState is the HP authority
        _charState?.TakeDamage(
            damage: amount,
            hitstun: 12,
            blockstun: 8,
            knockback: 1.5f,
            causesKnockdown: amount >= 22,
            isBlocked: false,
            hitFreezeFrames: 2
        );
    }

    private void BuildStateRegistry()
    {
        _states = new Dictionary<FighterStateType, FighterStateBase>();

        RegisterState(new IdleState());
        RegisterState(new WalkForwardState());
        RegisterState(new WalkBackwardState());
        RegisterState(new CrouchingState());
        RegisterState(new JumpNeutralState());
        RegisterState(new FallingState());

        RegisterState(new LightPunchState());
        RegisterState(new MediumPunchState());
        RegisterState(new HeavyPunchState());
        RegisterState(new LightKickState());
        RegisterState(new MediumKickState());
        RegisterState(new HeavyKickState());
        RegisterState(new SpecialMoveState());
        RegisterState(new AirLightPunchState());
        RegisterState(new AirHeavyKickState());

        RegisterState(new HitStunStandingState());
        RegisterState(new HitStunCrouchingState());
        RegisterState(new BlockingStandingState());
        RegisterState(new BlockingCrouchingState());
        RegisterState(new KnockdownFallingState());
        RegisterState(new KnockdownGroundState());
        RegisterState(new WakeUpState());
        RegisterState(new DeadState());
        RegisterState(new VictoryState());
    }

    private void RegisterState(FighterStateBase state)
    {
        state.Init(this, _physics, _fighter);
        _states[state.StateType] = state;
    }

    public bool TransitionTo(FighterStateType targetType)
    {
        if (targetType == CurrentStateType) return false;
        if (!IsTransitionAllowed(CurrentStateType, targetType)) return false;
        if (!_states.TryGetValue(targetType, out FighterStateBase nextState)) return false;

        _currentState?.OnExit(targetType);
        PreviousStateType = CurrentStateType;
        CurrentStateType = targetType;
        _currentState = nextState;
        FramesInCurrentState = 0;

        _currentState.OnEnter(PreviousStateType);
        return true;
    }

    public void ForceTransition(FighterStateType targetType)
    {
        if (!_states.TryGetValue(targetType, out FighterStateBase nextState)) return;

        _currentState?.OnExit(targetType);
        PreviousStateType = CurrentStateType;
        CurrentStateType = targetType;
        _currentState = nextState;
        FramesInCurrentState = 0;

        _currentState.OnEnter(PreviousStateType);
    }

    public void NotifyHitReceived(HitEvent hitEvent) { _currentState?.OnHitReceived(hitEvent); }

    public FighterStateBase GetState(FighterStateType type)
    {
        return _states.TryGetValue(type, out var state) ? state : null;
    }

    public T GetState<T>() where T : FighterStateBase
    {
        foreach (var state in _states.Values)
            if (state is T typed) return typed;
        return null;
    }

    private bool IsTransitionAllowed(FighterStateType from, FighterStateType to)
    {
        if (from == FighterStateType.Dead) return false;
        if (to == FighterStateType.Dead) return true;

        if (from == FighterStateType.HitStunStanding || from == FighterStateType.HitStunCrouching)
            return to == FighterStateType.Idle || to == FighterStateType.HitStunStanding ||
                   to == FighterStateType.HitStunCrouching || to == FighterStateType.KnockdownFalling;

        if (from == FighterStateType.BlockingStanding || from == FighterStateType.BlockingCrouching)
            return to == FighterStateType.Idle || to == FighterStateType.Crouching ||
                   to == FighterStateType.BlockingStanding || to == FighterStateType.BlockingCrouching ||
                   to == FighterStateType.HitStunStanding || to == FighterStateType.HitStunCrouching;

        if (from == FighterStateType.KnockdownFalling) return to == FighterStateType.KnockdownGround;
        if (from == FighterStateType.KnockdownGround) return to == FighterStateType.WakeUp;
        if (from == FighterStateType.WakeUp) return to == FighterStateType.Idle;

        if (from == FighterStateType.LightPunch || from == FighterStateType.MediumPunch ||
            from == FighterStateType.HeavyPunch || from == FighterStateType.LightKick ||
            from == FighterStateType.MediumKick || from == FighterStateType.HeavyKick ||
            from == FighterStateType.SpecialMove)
        {
            return to == FighterStateType.Idle || to == FighterStateType.HitStunStanding ||
                   to == FighterStateType.KnockdownFalling;
        }

        if (from == FighterStateType.JumpNeutral || from == FighterStateType.JumpForward ||
            from == FighterStateType.JumpBackward || from == FighterStateType.Falling)
        {
            return to == FighterStateType.Idle || to == FighterStateType.Falling ||
                   to == FighterStateType.AirLightPunch || to == FighterStateType.AirHeavyKick ||
                   to == FighterStateType.KnockdownFalling || to == FighterStateType.HitStunStanding;
        }

        return true;
    }

    public bool IsStunned =>
        CurrentStateType == FighterStateType.HitStunStanding ||
        CurrentStateType == FighterStateType.HitStunCrouching ||
        CurrentStateType == FighterStateType.BlockingStanding ||
        CurrentStateType == FighterStateType.BlockingCrouching;

    public bool IsAttacking =>
        CurrentStateType == FighterStateType.LightPunch ||
        CurrentStateType == FighterStateType.MediumPunch ||
        CurrentStateType == FighterStateType.HeavyPunch ||
        CurrentStateType == FighterStateType.LightKick ||
        CurrentStateType == FighterStateType.MediumKick ||
        CurrentStateType == FighterStateType.HeavyKick ||
        CurrentStateType == FighterStateType.SpecialMove ||
        CurrentStateType == FighterStateType.AirLightPunch ||
        CurrentStateType == FighterStateType.AirHeavyKick;

    public bool IsAirborne =>
        CurrentStateType == FighterStateType.JumpNeutral ||
        CurrentStateType == FighterStateType.JumpForward ||
        CurrentStateType == FighterStateType.JumpBackward ||
        CurrentStateType == FighterStateType.Falling ||
        CurrentStateType == FighterStateType.KnockdownFalling;

    // [FIX 5A] Removed WalkBackward from IsBlocking
    public bool IsBlocking =>
        CurrentStateType == FighterStateType.BlockingStanding ||
        CurrentStateType == FighterStateType.BlockingCrouching;
}