// StateMachine.cs
// Assets/Scripts/Core/StateMachine.cs
//
// WHAT THIS FILE DOES:
//   The StateMachine enforces the rules of WHAT a fighter is allowed to do
//   at any given moment. It answers the question:
//     "The player pressed JUMP — are they ALLOWED to jump right now?"
//
//   Without a state machine, you'd have to write hundreds of if/else checks
//   scattered across your code. With one, all the rules live in ONE place.
//
// HOW TO USE IN UNITY:
//   1. Add Component → StateMachine to your Fighter GameObject
//   2. StateMachine will auto-create all state objects in Awake()
//   3. FighterControllerSimple calls machine.HandleInput() and reads machine.CurrentStateType

using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  BASE CLASS — every state inherits from this
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The template every state must follow.
/// Think of each state as a "mode" the fighter is in.
///
/// OnEnter  → runs ONCE when we switch INTO this state
/// OnUpdate → runs EVERY frame while in this state
/// OnExit   → runs ONCE when we switch OUT OF this state
///
/// This pattern (Enter/Update/Exit) is called the State pattern.
/// It's used in almost every game ever made.
/// </summary>
public abstract class FighterStateBase
{
    // Every state needs access to the fighter's components
    // We store references here so subclasses can use them
    protected StateMachine Machine { get; private set; }
    protected PhysicsBody Physics { get; private set; }
    protected FighterControllerSimple Fighter { get; private set; }

    /// <summary>Which enum value does this state correspond to.</summary>
    public abstract FighterStateType StateType { get; }

    /// <summary>
    /// Called by StateMachine when it creates this state object.
    /// Stores the references so OnEnter/OnUpdate can use them.
    /// </summary>
    public void Init(StateMachine machine, PhysicsBody physics, FighterControllerSimple fighter)
    {
        Machine = machine;
        Physics = physics;
        Fighter = fighter;
    }

    /// <summary>Runs once when entering this state. Set up animation, flags, etc.</summary>
    public virtual void OnEnter(FighterStateType previousState) { }

    /// <summary>Runs every FixedUpdate frame while in this state.</summary>
    public virtual void OnUpdate(float deltaTime) { }

    /// <summary>Runs once when leaving this state. Clean up any state-specific data.</summary>
    public virtual void OnExit(FighterStateType nextState) { }

    /// <summary>
    /// Called when a HitEvent targets this fighter while in this state.
    /// States handle being hit differently:
    ///   - Idle/Walk → enter HitStun
    ///   - Blocking  → enter BlockStun
    ///   - Dead      → do nothing
    /// </summary>
    public virtual void OnHitReceived(HitEvent hitEvent) { }
}

// ─────────────────────────────────────────────────────────────────────────────
//  CONCRETE STATES
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// IDLE — the default state. Fighter stands still, ready for anything.
/// This is the hub all other states return to.
/// </summary>
public class IdleState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Idle;

    public override void OnEnter(FighterStateType previousState)
    {
        // Stop horizontal movement when returning to idle
        Physics.StopHorizontal();
    }

    public override void OnUpdate(float deltaTime)
    {
        // Idle continuously checks if the fighter has started moving.
        // If PhysicsBody reports horizontal velocity, transition to Walk.
        // (Velocity is set by FighterControllerSimple based on input BEFORE this runs)
        if (Mathf.Abs(Physics.Velocity.x) > 0.05f)
        {
            float dir = Physics.Velocity.x > 0 ? 1f : -1f;
            bool movingForward = (dir > 0) == (Fighter.Facing == FacingDirection.Right);
            Machine.TransitionTo(movingForward
                ? FighterStateType.WalkForward
                : FighterStateType.WalkBackward);
        }

        if (!Physics.IsGrounded)
            Machine.TransitionTo(FighterStateType.Falling);
    }

    public override void OnHitReceived(HitEvent hitEvent)
    {
        Machine.TransitionTo(hitEvent.result == HitResult.Blocked
            ? FighterStateType.BlockingStanding
            : FighterStateType.HitStunStanding);
    }
}

/// <summary>
/// WALK FORWARD — moving toward the opponent.
/// </summary>
public class WalkForwardState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.WalkForward;

    public override void OnUpdate(float deltaTime)
    {
        // If no horizontal input, return to idle
        if (Mathf.Abs(Physics.Velocity.x) < 0.05f)
            Machine.TransitionTo(FighterStateType.Idle);

        if (!Physics.IsGrounded)
            Machine.TransitionTo(FighterStateType.Falling);
    }

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(e.result == HitResult.Blocked
            ? FighterStateType.BlockingStanding
            : FighterStateType.HitStunStanding);
    }
}

/// <summary>
/// WALK BACKWARD — moving away from opponent (also the block walk).
/// </summary>
public class WalkBackwardState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.WalkBackward;

    public override void OnUpdate(float deltaTime)
    {
        if (Mathf.Abs(Physics.Velocity.x) < 0.05f)
            Machine.TransitionTo(FighterStateType.Idle);

        if (!Physics.IsGrounded)
            Machine.TransitionTo(FighterStateType.Falling);
    }

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(e.result == HitResult.Blocked
            ? FighterStateType.BlockingStanding
            : FighterStateType.HitStunStanding);
    }
}

/// <summary>
/// CROUCHING — fighter is low. Can block low, do crouch attacks.
/// Exits when crouch input is released.
/// </summary>
public class CrouchingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Crouching;

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(e.result == HitResult.Blocked
            ? FighterStateType.BlockingCrouching
            : FighterStateType.HitStunCrouching);
    }
}

/// <summary>
/// JUMP NEUTRAL — rising straight up.
/// Transitions to Falling once vertical velocity goes negative.
/// </summary>
public class JumpNeutralState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.JumpNeutral;

    public override void OnUpdate(float deltaTime)
    {
        if (Physics.Velocity.y < 0f)
            Machine.TransitionTo(FighterStateType.Falling);

        if (Physics.IsGrounded)
            Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e)
    {
        // Getting hit in the air launches into a different knockdown arc
        Machine.TransitionTo(FighterStateType.KnockdownFalling);
    }
}

/// <summary>
/// FALLING — airborne and moving downward.
/// This covers both the descent from a jump AND being knocked into the air.
/// </summary>
public class FallingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Falling;

    public override void OnUpdate(float deltaTime)
    {
        // Land when PhysicsBody reports we've touched the ground
        if (Physics.IsGrounded)
            Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e)
    {
        Machine.TransitionTo(FighterStateType.KnockdownFalling);
    }
}

/// <summary>
/// ATTACKING — the fighter is mid-attack.
/// The attack has a duration (in frames). Once it expires, return to Idle.
///
/// HOW ATTACK TIMING WORKS:
///   Fighting games measure attack duration in FRAMES, not seconds.
///   At 60fps: 1 frame = 1/60th of a second ≈ 16.7ms
///
///   A typical light punch is:
///     • Startup:  4 frames  (wind-up before hitbox appears)
///     • Active:   3 frames  (hitbox is live — can deal damage)
///     • Recovery: 8 frames  (follow-through before you can act again)
///     Total:     15 frames  = 0.25 seconds
///
///   The BoxData array from FighterControllerSimple changes based on which phase
///   the attack is in. This state just counts frames and drives transitions.
/// </summary>
public class AttackingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.LightPunch;
    // NOTE: In a real project, each attack (LightPunch, HeavyKick, etc.)
    // gets its OWN state class. They all follow this same pattern.
    // For clarity we show one here as a template.

    private int _totalDuration;   // frames this attack lasts
    private int _frameCounter;    // frames elapsed since entering

    // Called by FighterControllerSimple BEFORE TransitionTo so the state knows
    // how long to run.
    public void SetDuration(int frames) => _totalDuration = frames;

    public override void OnEnter(FighterStateType previousState)
    {
        _frameCounter = 0;
        Physics.StopHorizontal();   // most attacks lock horizontal movement
    }

    public override void OnUpdate(float deltaTime)
    {
        _frameCounter++;

        if (_frameCounter >= _totalDuration)
            Machine.TransitionTo(FighterStateType.Idle);   // attack finished — recovery done
    }

    public override void OnHitReceived(HitEvent e)
    {
        // Getting hit during your own attack: you're counter-hit
        // Counter-hits typically deal more hitstun — handle in FighterControllerSimple
        Machine.TransitionTo(FighterStateType.HitStunStanding);
    }
}

/// <summary>
/// HITSTUN STANDING — the fighter was hit and is frozen, unable to act.
/// Counts down a number of frames then returns to Idle.
///
/// During hitstun the fighter CANNOT:
///   • Attack
///   • Jump
///   • Block
///   • Move voluntarily
///
/// The fighter CAN:
///   • Receive another hit (combo'd!)
///   • Be pushed by the attacker's momentum
/// </summary>
public class HitStunStandingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.HitStunStanding;

    private int _stunFramesRemaining;

    /// <summary>Called by FighterControllerSimple immediately after receiving a hit.</summary>
    public void SetStunDuration(int frames) => _stunFramesRemaining = frames;

    public override void OnEnter(FighterStateType previousState)
    {
        Physics.StopHorizontal();
    }

    public override void OnUpdate(float deltaTime)
    {
        _stunFramesRemaining--;

        if (_stunFramesRemaining <= 0)
            Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e)
    {
        // Being hit again RESETS the stun duration — this is how combos work.
        // Each new hit adds more hitstun, keeping the opponent locked.
        SetStunDuration(e.sourceBox.hitstunFrames);
    }
}

/// <summary>
/// HITSTUN CROUCHING — same as standing hitstun but from a hit while crouching.
/// Uses a different animation (the crouch-reel animation).
/// </summary>
public class HitStunCrouchingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.HitStunCrouching;

    private int _stunFramesRemaining;

    public void SetStunDuration(int frames) => _stunFramesRemaining = frames;

    public override void OnUpdate(float deltaTime)
    {
        _stunFramesRemaining--;
        if (_stunFramesRemaining <= 0)
            Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e)
    {
        SetStunDuration(e.sourceBox.hitstunFrames);
    }
}

/// <summary>
/// BLOCKING STANDING — the fighter is in guard, successfully blocking a hit.
/// Blockstun works exactly like hitstun — frozen for N frames — but the
/// fighter took no damage.
/// </summary>
public class BlockingStandingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.BlockingStanding;

    private int _blockstunRemaining;

    public void SetBlockstunDuration(int frames) => _blockstunRemaining = frames;

    public override void OnEnter(FighterStateType previousState)
    {
        Physics.StopHorizontal();
    }

    public override void OnUpdate(float deltaTime)
    {
        _blockstunRemaining--;
        if (_blockstunRemaining <= 0)
            Machine.TransitionTo(FighterStateType.Idle);
    }

    public override void OnHitReceived(HitEvent e)
    {
        // Blocked again — refresh blockstun (block strings in action)
        if (e.result == HitResult.Blocked)
            SetBlockstunDuration(e.sourceBox.blockstunFrames);
        else
            Machine.TransitionTo(FighterStateType.HitStunStanding);
    }
}

/// <summary>
/// BLOCKING CROUCHING — same but crouching. Covers low attacks.
/// </summary>
public class BlockingCrouchingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.BlockingCrouching;

    private int _blockstunRemaining;

    public void SetBlockstunDuration(int frames) => _blockstunRemaining = frames;

    public override void OnUpdate(float deltaTime)
    {
        _blockstunRemaining--;
        if (_blockstunRemaining <= 0)
            Machine.TransitionTo(FighterStateType.Crouching);
    }

    public override void OnHitReceived(HitEvent e)
    {
        if (e.result == HitResult.Blocked)
            SetBlockstunDuration(e.sourceBox.blockstunFrames);
        else
            Machine.TransitionTo(FighterStateType.HitStunCrouching);
    }
}

/// <summary>
/// KNOCKDOWN FALLING — hit hard enough to go airborne.
/// Fighter tumbles through the air, no control, until they hit the ground.
/// </summary>
public class KnockdownFallingState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.KnockdownFalling;

    public override void OnUpdate(float deltaTime)
    {
        if (Physics.IsGrounded)
            Machine.TransitionTo(FighterStateType.KnockdownGround);
    }

    // Cannot be hit again during knockdown fall (invincible in most games)
}

/// <summary>
/// KNOCKDOWN GROUND — lying on the floor after a knockdown.
/// Fighter is temporarily invincible. Then they wake up.
///
/// WAKEUP TIMING is important game design:
///   • Too short = no mixup opportunity for the attacker
///   • Too long = frustrating for the defender
///   • Standard: 30–60 frames of ground time
/// </summary>
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
        if (_frameCounter >= _wakeupFrames)
            Machine.TransitionTo(FighterStateType.WakeUp);
    }
}

/// <summary>
/// WAKEUP — brief invincibility window as the fighter stands up.
/// After this resolves, return to Idle.
/// </summary>
public class WakeUpState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.WakeUp;

    private readonly int _wakeupDuration = 10; // frames of wakeup animation
    private int _frameCounter;

    public override void OnEnter(FighterStateType previousState) => _frameCounter = 0;

    public override void OnUpdate(float deltaTime)
    {
        _frameCounter++;
        if (_frameCounter >= _wakeupDuration)
            Machine.TransitionTo(FighterStateType.Idle);
    }
    // Invincible during wakeup — OnHitReceived intentionally not overridden
}

/// <summary>
/// DEAD — HP reached zero. Terminal state. No transitions out.
/// GameManager listens for this to trigger the round-end sequence.
/// </summary>
public class DeadState : FighterStateBase
{
    public override FighterStateType StateType => FighterStateType.Dead;

    public override void OnEnter(FighterStateType previousState)
    {
        Physics.StopAll();
        // Fire event so GameManager knows to end the round
        StateMachine.RaiseFighterDied(Fighter.playerIndex);
    }
    // No OnUpdate — nothing happens after death
    // No OnHitReceived — can't be hit when dead
}

// ─────────────────────────────────────────────────────────────────────────────
//  THE STATE MACHINE — the controller that runs all of the above
// ─────────────────────────────────────────────────────────────────────────────

public class StateMachine : MonoBehaviour
{
    // ── Static event so GameManager can listen without a direct reference ────
    /// <summary>Fired when any fighter enters DeadState. int = player index.</summary>
    public static event Action<int> OnFighterDied;
    public static void RaiseFighterDied(int playerIndex) => OnFighterDied?.Invoke(playerIndex);

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Starting state")]
    [SerializeField] private FighterStateType _initialState = FighterStateType.Idle;

    // ── Public read ───────────────────────────────────────────────────────────
    /// <summary>The enum value of whatever state we're in right now.</summary>
    public FighterStateType CurrentStateType { get; private set; }

    /// <summary>How many frames we've been in the current state.</summary>
    public int FramesInCurrentState { get; private set; }

    /// <summary>What state we were in before the current one.</summary>
    public FighterStateType PreviousStateType { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────────
    private FighterStateBase _currentState;
    private Dictionary<FighterStateType, FighterStateBase> _states;

    private PhysicsBody _physics;
    private FighterControllerSimple _fighter;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _physics = GetComponent<PhysicsBody>();
        _fighter = GetComponent<FighterControllerSimple>();

        if (_physics == null)
            Debug.LogError("[StateMachine] PhysicsBody not found on this GameObject!");
        if (_fighter == null)
            Debug.LogError("[StateMachine] FighterControllerSimple not found on this GameObject!");

        BuildStateRegistry();
    }

    private void Start()
    {
        // Enter the initial state (Idle by default)
        ForceTransition(_initialState);
    }

    private void FixedUpdate()
    {
        // Tick the current state every physics frame
        _currentState?.OnUpdate(Time.fixedDeltaTime);
        FramesInCurrentState++;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STATE REGISTRY — builds all state objects at startup
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates one instance of every state class and stores them in a Dictionary.
    /// Dictionary lookup is O(1) — instant no matter how many states there are.
    ///
    /// WHY A DICTIONARY?
    ///   We look up states by their enum value dozens of times per second.
    ///   A switch statement works too but adding new states means editing
    ///   two places. Dictionary means you only add the state class.
    /// </summary>
    private void BuildStateRegistry()
    {
        _states = new Dictionary<FighterStateType, FighterStateBase>();

        // Create and register every state
        RegisterState(new IdleState());
        RegisterState(new WalkForwardState());
        RegisterState(new WalkBackwardState());
        RegisterState(new CrouchingState());
        RegisterState(new JumpNeutralState());
        RegisterState(new FallingState());
        RegisterState(new AttackingState());
        RegisterState(new HitStunStandingState());
        RegisterState(new HitStunCrouchingState());
        RegisterState(new BlockingStandingState());
        RegisterState(new BlockingCrouchingState());
        RegisterState(new KnockdownFallingState());
        RegisterState(new KnockdownGroundState());
        RegisterState(new WakeUpState());
        RegisterState(new DeadState());
    }

    private void RegisterState(FighterStateBase state)
    {
        state.Init(this, _physics, _fighter);
        _states[state.StateType] = state;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TRANSITION SYSTEM
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Request a transition to a new state.
    ///
    /// This is the GATEKEEPER. Before allowing any transition it checks:
    ///   1. Is the target state different from the current state?
    ///   2. Is this transition LEGAL from the current state?
    ///      (You can't jump while in HitStun, for example.)
    ///
    /// If the transition is legal it:
    ///   1. Calls OnExit() on the current state
    ///   2. Switches the current state pointer
    ///   3. Calls OnEnter() on the new state
    /// </summary>
    public bool TransitionTo(FighterStateType targetType)
    {
        // Already in that state — nothing to do
        if (targetType == CurrentStateType) return false;

        // Check if the transition is allowed
        if (!IsTransitionAllowed(CurrentStateType, targetType))
        {
            Debug.Log($"[StateMachine P{_fighter.playerIndex}] " +
                      $"Blocked illegal transition: {CurrentStateType} → {targetType}");
            return false;
        }

        // Find the target state object in our dictionary
        if (!_states.TryGetValue(targetType, out FighterStateBase nextState))
        {
            Debug.LogError($"[StateMachine] State {targetType} not registered!");
            return false;
        }

        // Execute the transition
        _currentState?.OnExit(targetType);

        PreviousStateType = CurrentStateType;
        CurrentStateType = targetType;
        _currentState = nextState;
        FramesInCurrentState = 0;

        _currentState.OnEnter(PreviousStateType);
        return true;
    }

    /// <summary>
    /// Force a transition regardless of legality rules.
    /// Used for: round start reset, GameManager forcing death, debug tools.
    /// Normal gameplay should ALWAYS use TransitionTo().
    /// </summary>
    public void ForceTransition(FighterStateType targetType)
    {
        if (!_states.TryGetValue(targetType, out FighterStateBase nextState))
        {
            Debug.LogError($"[StateMachine] State {targetType} not registered!");
            return;
        }

        _currentState?.OnExit(targetType);

        PreviousStateType = CurrentStateType;
        CurrentStateType = targetType;
        _currentState = nextState;
        FramesInCurrentState = 0;

        _currentState.OnEnter(PreviousStateType);
    }

    /// <summary>
    /// Pass a hit event to the current state.
    /// Each state decides for itself what "being hit" means in that context.
    /// FighterControllerSimple calls this when CollisionManager fires OnHitConfirmed.
    /// </summary>
    public void NotifyHitReceived(HitEvent hitEvent)
    {
        _currentState?.OnHitReceived(hitEvent);
    }

    /// <summary>
    /// Convenience accessor for getting a specific typed state.
    /// Used when FighterControllerSimple needs to set hitstun duration on
    /// the HitStunState before transitioning into it.
    ///
    /// Example:
    ///   machine.GetState<HitStunStandingState>().SetStunDuration(14);
    ///   machine.TransitionTo(FighterStateType.HitStunStanding);
    /// </summary>
    public T GetState<T>() where T : FighterStateBase
    {
        foreach (var state in _states.Values)
            if (state is T typed) return typed;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TRANSITION RULES — the core game design lives here
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if moving from 'from' to 'to' is a legal transition.
    ///
    /// THIS IS WHERE YOUR GAME DESIGN LIVES.
    /// Every rule about what a fighter can and can't do is expressed here.
    ///
    /// The rules below are a solid foundation for a Mortal Kombat-style game.
    /// Your team will tune these as you playtest.
    /// </summary>
    private bool IsTransitionAllowed(FighterStateType from, FighterStateType to)
    {
        // ── Dead fighters can't do anything ──────────────────────────────────
        if (from == FighterStateType.Dead) return false;

        // ── Anything can transition to Dead (HP hit zero) ─────────────────────
        if (to == FighterStateType.Dead) return true;

        // ── HitStun: fighter is frozen — NO voluntary actions ─────────────────
        if (from == FighterStateType.HitStunStanding ||
            from == FighterStateType.HitStunCrouching)
        {
            // Only allowed exits: stun expiring → Idle, or being hit again
            return to == FighterStateType.Idle ||
                   to == FighterStateType.HitStunStanding ||
                   to == FighterStateType.HitStunCrouching ||
                   to == FighterStateType.KnockdownFalling;
        }

        // ── BlockStun: frozen in guard — NO actions ───────────────────────────
        if (from == FighterStateType.BlockingStanding ||
            from == FighterStateType.BlockingCrouching)
        {
            return to == FighterStateType.Idle ||
                   to == FighterStateType.Crouching ||
                   to == FighterStateType.BlockingStanding ||
                   to == FighterStateType.BlockingCrouching ||
                   to == FighterStateType.HitStunStanding ||
                   to == FighterStateType.HitStunCrouching;
        }

        // ── Knockdown: no control until wakeup ────────────────────────────────
        if (from == FighterStateType.KnockdownFalling)
            return to == FighterStateType.KnockdownGround;

        if (from == FighterStateType.KnockdownGround)
            return to == FighterStateType.WakeUp;

        if (from == FighterStateType.WakeUp)
            return to == FighterStateType.Idle;

        // ── Attacking: can't act until recovery is done ───────────────────────
        // (The AttackingState.OnUpdate drives the exit when frame count expires)
        if (from == FighterStateType.LightPunch ||
            from == FighterStateType.MediumPunch ||
            from == FighterStateType.HeavyPunch ||
            from == FighterStateType.LightKick ||
            from == FighterStateType.MediumKick ||
            from == FighterStateType.HeavyKick ||
            from == FighterStateType.SpecialMove)
        {
            return to == FighterStateType.Idle ||
                   to == FighterStateType.HitStunStanding ||
                   to == FighterStateType.KnockdownFalling;
            // NOTE: Add jump-cancel or special-cancel rules here later
            // e.g.:  || to == FighterStateType.JumpNeutral  (jump cancel)
        }

        // ── Airborne: limited options — can't walk or crouch ──────────────────
        if (from == FighterStateType.JumpNeutral ||
            from == FighterStateType.JumpForward ||
            from == FighterStateType.JumpBackward ||
            from == FighterStateType.Falling)
        {
            return to == FighterStateType.Idle ||   // landing
                   to == FighterStateType.Falling ||   // apex → descent
                   to == FighterStateType.AirLightPunch ||   // air attacks
                   to == FighterStateType.AirHeavyKick ||
                   to == FighterStateType.KnockdownFalling ||   // hit mid-air
                   to == FighterStateType.HitStunStanding;        // air hitstun
        }

        // ── Ground neutral (Idle, Walk, Crouch): almost anything is allowed ───
        // These are the states where the player has full control.
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  QUERY HELPERS — used by FighterControllerSimple and UI
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True if the fighter is completely frozen (hitstun or blockstun).</summary>
    public bool IsStunned =>
        CurrentStateType == FighterStateType.HitStunStanding ||
        CurrentStateType == FighterStateType.HitStunCrouching ||
        CurrentStateType == FighterStateType.BlockingStanding ||
        CurrentStateType == FighterStateType.BlockingCrouching;

    /// <summary>True while any attack animation is active.</summary>
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

    /// <summary>True when the fighter is airborne for any reason.</summary>
    public bool IsAirborne =>
        CurrentStateType == FighterStateType.JumpNeutral ||
        CurrentStateType == FighterStateType.JumpForward ||
        CurrentStateType == FighterStateType.JumpBackward ||
        CurrentStateType == FighterStateType.Falling ||
        CurrentStateType == FighterStateType.KnockdownFalling;

    /// <summary>
    /// True if this fighter is considered blocking.
    /// Used by CollisionManager.ResolveHitResult to check guard.
    /// </summary>
    public bool IsBlocking =>
        CurrentStateType == FighterStateType.BlockingStanding ||
        CurrentStateType == FighterStateType.BlockingCrouching ||
        CurrentStateType == FighterStateType.WalkBackward;     // holding back = passive guard
}