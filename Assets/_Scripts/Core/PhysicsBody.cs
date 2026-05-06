// HOW TO USE IN UNITY:
//   1. Select your Fighter GameObject in the Hierarchy
//   2. In the Inspector → Add Component → Physics Body
//   3. Set the Stage Left/Right Wall values to match your arena width
//   4. Do NOT also add a Rigidbody2D — this replaces it

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Transform))]   // tells Unity this needs a Transform
public class PhysicsBody : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR SETTINGS
    //  (these show up as sliders/fields in the Unity Inspector panel)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Gravity")]
    [Tooltip("How fast the fighter accelerates downward while airborne. " +
             "Higher = heavier feel. Typical range: 20-40.")]
    [SerializeField] private float _gravity = 28f;

    [Tooltip("Maximum speed the fighter can fall. " +
             "Prevents tunneling through the floor at high framerates.")]
    [SerializeField] private float _maxFallSpeed = 20f;

    [Header("Movement")]
    [Tooltip("Horizontal movement speed in world units per second.")]
    [SerializeField] private float _moveSpeed = 5f;

    [Tooltip("How fast the fighter stops when no input is held. " +
             "1.0 = instant stop (fighting game standard). " +
             "Lower values add sliding/momentum.")]
    [SerializeField][Range(0f, 1f)] private float _groundFriction = 1f;

    [Header("Jumping")]
    [Tooltip("Upward velocity applied the instant the fighter jumps. " +
             "Tweak this to control jump height. Typical range: 12-18.")]
    [SerializeField] private float _jumpForce = 15f;

    [Tooltip("Maximum number of jumps before landing. " +
             "1 = normal jump. 2 = double jump (some characters).")]
    [SerializeField][Range(1, 2)] private int _maxJumps = 1;

    [Header("Stage boundaries")]
    [Tooltip("Left wall X position in world space. " +
             "The fighter's center cannot go left of this.")]
    [SerializeField] private float _stageLeftWall = -8f;

    [Tooltip("Right wall X position in world space.")]
    [SerializeField] private float _stageRightWall = 8f;

    [Tooltip("Y position of the ground (floor). " +
             "Fighters land when their position.y reaches this.")]
    [SerializeField] private float _groundY = 0f;

    [Header("Coyote time")]
    [Tooltip("Seconds after walking off a ledge where the fighter " +
             "can still jump. Feels more responsive than strict ground check. " +
             "Set to 0 to disable.")]
    [SerializeField][Range(0f, 0.2f)] private float _coyoteTime = 0.08f;

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC PROPERTIES
    //  Other scripts (FighterController, StateMachine) read these.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Current world-space position of the fighter's feet.</summary>
    public Vector2 Position { get; private set; }

    /// <summary>Current velocity in world units per second.</summary>
    public Vector2 Velocity { get; private set; }

    /// <summary>
    /// True when the fighter is standing on the ground.
    /// Used by FighterController to decide which animations/moves are available.
    /// </summary>
    public bool IsGrounded { get; private set; }

    /// <summary>
    /// True during the coyote time window (briefly after walking off a ledge).
    /// The fighter can still jump during this time even though IsGrounded is false.
    /// </summary>
    public bool CanCoyoteJump => _coyoteTimer > 0f;

    /// <summary>How many jumps the fighter has used since last landing.</summary>
    public int JumpsUsed { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────

    // The horizontal input this frame, set by FighterController before
    // FixedUpdate runs. Range: -1 (full left) to +1 (full right).
    private float _moveInput;

    // Jump was requested this frame (button pressed).
    // We buffer this from Update so it isn't missed on a FixedUpdate boundary.
    private bool _jumpRequested;

    // Countdown timer for coyote time (seconds remaining).
    private float _coyoteTimer;

    // Whether the fighter was grounded last frame.
    // Used to detect the exact moment of landing and takeoff.
    private bool _wasGrounded;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Initialize position from wherever the GameObject is placed in the scene.
        // Your level designer sets the spawn position in Unity; we read it here.
        Position = transform.position;
    }

    private void Start()
    {
        // Subscribe to the pushbox overlap event so we can separate fighters
        // when CollisionManager tells us they're overlapping.
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnPushboxOverlap += HandlePushboxOverlap;
    }

    private void OnDestroy()
    {
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnPushboxOverlap -= HandlePushboxOverlap;
    }

    /// <summary>
    /// FixedUpdate runs at the physics timestep (50 times/sec by default).
    /// ALL physics math lives here — never in Update — so the simulation is
    /// frame-rate independent.
    /// </summary>
    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;   // shorthand — seconds per physics step

        // Run each phase in order. The order matters:
        // gravity must be applied before ground check,
        // ground check must happen before wall clamp,
        // wall clamp must happen before transform sync.

        ApplyGravity(dt);
        ApplyHorizontalMovement(dt);
        ProcessJump();
        IntegratePosition(dt);
        GroundCheck();
        ClampToStageBounds();
        UpdateCoyoteTimer(dt);
        SyncTransform();

        // Clear per-frame inputs so they don't carry over
        _moveInput = 0f;
        _jumpRequested = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API — called by FighterController each frame
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set horizontal movement intention this frame.
    /// Call this from FighterController.Update() before FixedUpdate runs.
    ///
    /// input: -1 = full left, 0 = no movement, +1 = full right.
    ///        Diagonal values (e.g. 0.5) are valid for analog sticks.
    /// </summary>
    public void SetPositionX(float newX)
    {
        // 1. Update our internal math position
        Position = new Vector2(newX, Position.y);

        // 2. Immediately push that change to the visual Unity Transform
        transform.position = new Vector3(Position.x, Position.y, transform.position.z);
    }


    public void SetMoveInput(float input)
    {
        _moveInput = Mathf.Clamp(input, -1f, 1f);
    }

    /// <summary>
    /// Request a jump this frame.  The actual jump happens in FixedUpdate.
    /// Buffering the request here prevents the jump from being missed if
    /// Update and FixedUpdate run on different frames.
    /// </summary>
    public void RequestJump()
    {
        _jumpRequested = true;
    }

    /// <summary>
    /// Instantly sets velocity.  Used for:
    ///   • Knockback when a hit lands
    ///   • Launch moves (uppercuts, air launches)
    ///   • Dashes
    /// </summary>
    public void SetVelocity(Vector2 newVelocity)
    {
        Velocity = newVelocity;
    }

    public void ApplyKnockback(float force)
    {
        // Determines push direction based on facing. 
        // If facing right (Y rotation is 0), push left (-1). If facing left, push right (1).
        float pushDirection = transform.eulerAngles.y < 90f ? -1f : 1f;

        // Applies the force backward, maintaining current vertical velocity
        SetVelocity(new Vector2(force * pushDirection * 3f, Velocity.y));
    }

    /// <summary>
    /// Adds velocity on top of what's already there.
    /// Used for moves that keep existing momentum (e.g. a jump-cancel).
    /// </summary>
    public void AddVelocity(Vector2 delta)
    {
        Velocity += delta;
    }

    /// <summary>
    /// Instantly stops all horizontal movement.
    /// Called when the fighter enters hitstun — they can't move voluntarily.
    /// </summary>
    public void StopHorizontal()
    {
        Velocity = new Vector2(0f, Velocity.y);
    }

    /// <summary>
    /// Instantly stops all movement.  Called on death or round reset.
    /// </summary>
    public void StopAll()
    {
        Velocity = Vector2.zero;
    }

    /// <summary>
    /// Teleports the fighter to a position.
    /// Used by GameManager to reset fighters to their start positions
    /// at the beginning of each round.
    /// </summary>
    public void Warp(Vector2 targetPosition)
    {
        Position = targetPosition;
        Velocity = Vector2.zero;
        IsGrounded = targetPosition.y <= _groundY + 0.01f;
        SyncTransform();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PHYSICS STEPS (called in order from FixedUpdate)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// STEP 1 — Gravity
    ///
    /// While the fighter is airborne, accelerate them downward.
    /// This mimics real gravity: velocity changes continuously,
    /// and position changes as a result of velocity (not directly).
    ///
    /// WHY NOT Unity's gravity?
    ///   Unity's Rigidbody2D gravity is applied globally and interacts
    ///   with colliders. We need full control: different gravity for
    ///   ascending vs descending, fast-fall, etc.
    /// </summary>
    private void ApplyGravity(float dt)
    {
        if (IsGrounded) return;   // no gravity when standing on solid ground

        // Increase downward speed each frame
        float newVY = Velocity.y - (_gravity * dt);

        // Cap at maxFallSpeed so the fighter can't fall infinitely fast.
        // (Infinite fall speed causes the fighter to skip through the floor.)
        newVY = Mathf.Max(newVY, -_maxFallSpeed);

        Velocity = new Vector2(Velocity.x, newVY);
    }

    /// <summary>
    /// STEP 2 — Horizontal movement
    ///
    /// _moveInput is set by FighterController.  We multiply it by moveSpeed
    /// to get the desired horizontal velocity this frame.
    ///
    /// Ground friction makes the fighter stop instantly when input is released
    /// (groundFriction = 1.0 is the classic fighting game feel).
    /// A lower friction value gives them a bit of slide.
    /// </summary>
    private void ApplyHorizontalMovement(float dt)
    {
        float targetVX = _moveInput * _moveSpeed;

        float newVX;
        if (IsGrounded)
        {
            // On the ground: lerp toward target velocity using friction.
            // Lerp(a, b, t): t=1 snaps instantly, t=0 never moves.
            newVX = Mathf.Lerp(Velocity.x, targetVX, _groundFriction);
        }
        else
        {
            // In the air: reduced control (air mobility).
            // Fighters can't change direction as sharply mid-air —
            // that's the standard fighting game feel.
            float airControlFactor = 0.6f;
            newVX = Mathf.Lerp(Velocity.x, targetVX, _groundFriction * airControlFactor);
        }

        Velocity = new Vector2(newVX, Velocity.y);
    }

    /// <summary>
    /// STEP 3 — Process jump request
    ///
    /// If a jump was requested (button pressed this frame) AND the fighter
    /// is allowed to jump, apply the jump force instantly.
    ///
    /// ALLOWED means:
    ///   • Standing on the ground, OR
    ///   • Within the coyote time window, OR
    ///   • Has a double-jump remaining (if maxJumps == 2)
    /// </summary>
    private void ProcessJump()
    {
        if (!_jumpRequested) return;

        bool canJump = (IsGrounded || CanCoyoteJump) && JumpsUsed < _maxJumps
                    || (!IsGrounded && JumpsUsed < _maxJumps);

        if (!canJump) return;

        // Apply upward velocity — this is what makes the fighter go up.
        // We SET velocity.y (not add) so jumping from a downward arc still
        // launches them to the full jump height.
        Velocity = new Vector2(Velocity.x, _jumpForce);

        JumpsUsed++;
        IsGrounded = false;
        _coyoteTimer = 0f;   // cancel coyote time — we already used the jump
    }

    /// <summary>
    /// STEP 4 — Integrate position
    ///
    /// "Integration" means: new position = old position + (velocity × time).
    /// This is called Euler integration — the simplest and fastest method,
    /// perfectly adequate for a 2D fighting game.
    /// </summary>
    private void IntegratePosition(float dt)
    {
        Position += Velocity * dt;
    }

    /// <summary>
    /// STEP 5 — Ground check
    ///
    /// After moving, check if we've gone below (or hit) the floor.
    /// If so, snap to the floor and zero out downward velocity (landing).
    ///
    /// WHY SNAP instead of stopping early?
    ///   At high velocities a fighter could travel past the floor in a
    ///   single frame.  Snapping back to groundY is simpler and more reliable
    ///   than sub-frame collision detection for a flat floor.
    /// </summary>
    private void GroundCheck()
    {
        _wasGrounded = IsGrounded;

        if (Position.y <= _groundY)
        {
            // Snap to ground level
            Position = new Vector2(Position.x, _groundY);

            // Zero out downward velocity (but keep horizontal for landing skid)
            if (Velocity.y < 0f)
                Velocity = new Vector2(Velocity.x, 0f);

            // If we just landed this frame, reset jump count
            if (!_wasGrounded)
                OnLanded();

            IsGrounded = true;
        }
        else
        {
            // Became airborne this frame — start the coyote timer
            if (_wasGrounded && !IsGrounded)
                _coyoteTimer = _coyoteTime;

            IsGrounded = false;
        }
    }

    /// <summary>
    /// Called the exact frame the fighter touches the ground.
    /// FighterController listens for IsGrounded to flip from false → true
    /// to trigger the landing animation.
    /// </summary>
    private void OnLanded()
    {
        JumpsUsed = 0;   // refill jumps on landing
    }

    /// <summary>
    /// STEP 6 — Clamp to stage boundaries
    ///
    /// The stage has invisible walls on the left and right.
    /// Mathf.Clamp ensures the fighter's X never exceeds those limits.
    ///
    /// We also zero out horizontal velocity when hitting a wall so the
    /// fighter doesn't "stick" to it with residual velocity.
    /// </summary>
    private void ClampToStageBounds()
    {
        float clampedX = Mathf.Clamp(Position.x, _stageLeftWall, _stageRightWall);

        // If clamping changed X, the fighter just hit a wall — kill horizontal velocity
        if (!Mathf.Approximately(clampedX, Position.x))
            Velocity = new Vector2(0f, Velocity.y);

        Position = new Vector2(clampedX, Position.y);
    }

    /// <summary>
    /// Count down coyote time each frame.
    /// Once it hits zero the fighter can no longer jump (if they're airborne).
    /// </summary>
    private void UpdateCoyoteTimer(float dt)
    {
        if (_coyoteTimer > 0f)
            _coyoteTimer -= dt;
    }

    /// <summary>
    /// FINAL STEP — Push our calculated position back to the Unity Transform.
    ///
    /// WHY NOT just move the Transform directly throughout?
    ///   We calculate everything in our own Vector2 first, then write to
    ///   the Transform ONCE at the end.  This is faster (Transform writes
    ///   are expensive — they trigger matrix recalculations) and avoids
    ///   reading stale Transform data mid-frame.
    /// </summary>
    private void SyncTransform()
    {
        // Keep the Z position (depth) unchanged — we only work in 2D
        transform.position = new Vector3(Position.x, Position.y, transform.position.z);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUSHBOX SEPARATION — called by CollisionManager event
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When CollisionManager detects that the two fighters' pushboxes are
    /// overlapping, it fires OnPushboxOverlap with the overlap depth.
    ///
    /// Both fighters receive this event simultaneously.  Each one moves
    /// away by HALF the overlap depth — so together they separate by the
    /// full overlap amount.
    ///
    /// DIRECTION: each fighter moves AWAY from the other.
    ///   If this fighter is to the LEFT of the opponent, they move left.
    ///   If to the RIGHT, they move right.
    ///
    /// HOW DO WE KNOW WHICH DIRECTION?
    ///   We compare our X position to the opponent's X position.
    ///   We find the opponent via the CollisionManager (it knows both fighters).
    /// </summary>
    private void HandlePushboxOverlap(float overlapDepth)
    {
        // Find the opponent's PhysicsBody so we can compare positions
        PhysicsBody opponent = FindOpponent();
        if (opponent == null) return;

        // Determine which direction WE should be pushed
        // If we're to the left of the opponent, push left (negative X)
        // If we're to the right, push right (positive X)
        float pushDirection = Position.x < opponent.Position.x ? -1f : 1f;

        // Move half the overlap depth in our direction
        // (opponent does the same in their direction = full separation)
        float halfDepth = overlapDepth * 0.5f;
        Position = new Vector2(Position.x + pushDirection * halfDepth, Position.y);

        // Re-clamp after push so neither fighter gets pushed through a wall
        ClampToStageBounds();

        // Immediately sync so CollisionManager sees the corrected position next frame
        SyncTransform();
    }

    /// <summary>
    /// Finds the other fighter's PhysicsBody by looking at CollisionManager.
    /// This avoids needing a direct reference to the other fighter.
    /// </summary>
    private PhysicsBody FindOpponent()
    {
        // We look for all PhysicsBody components in the scene and return
        // the one that isn't us.  In a 2-fighter game this is always correct.
        // If you add more fighters later, replace this with a proper lookup.
        PhysicsBody[] allBodies = FindObjectsOfType<PhysicsBody>();
        foreach (PhysicsBody body in allBodies)
        {
            if (body != this) return body;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DEBUG — visible in Scene view while playing
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        // Draw the ground line
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
        Gizmos.DrawLine(
            new Vector3(_stageLeftWall, _groundY, 0f),
            new Vector3(_stageRightWall, _groundY, 0f));

        // Draw stage walls
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        float wallHeight = 6f;
        Gizmos.DrawLine(
            new Vector3(_stageLeftWall, _groundY, 0f),
            new Vector3(_stageLeftWall, _groundY + wallHeight, 0f));
        Gizmos.DrawLine(
            new Vector3(_stageRightWall, _groundY, 0f),
            new Vector3(_stageRightWall, _groundY + wallHeight, 0f));
    }
}