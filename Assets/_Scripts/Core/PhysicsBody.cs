// Assets/_Scripts/Fighter/PhysicsBody.cs
// HOW TO USE IN UNITY:
//   1. Select your Fighter GameObject in the Hierarchy
//   2. In the Inspector → Add Component → Physics Body
//   3. Leave Stage Left/Right Wall and Ground Y at defaults —
//      StageBootstrap.ApplyToFighters() will override them at runtime.
//   4. Do NOT also add a Rigidbody — this replaces it.

using UnityEngine;

[RequireComponent(typeof(Transform))]
public class PhysicsBody : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR SETTINGS
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

    [Header("Stage Boundaries")]
    [Tooltip("Left wall X position in world space. " +
             "Overridden at runtime by StageBootstrap.ApplyToFighters().")]
    [SerializeField] private float _stageLeftWall = -8f;

    [Tooltip("Right wall X position in world space. " +
             "Overridden at runtime by StageBootstrap.ApplyToFighters().")]
    [SerializeField] private float _stageRightWall = 8f;

    [Tooltip("Y position of the ground (floor). " +
             "CRITICAL — must match the actual stage floor Y. " +
             "Overridden at runtime by StageBootstrap.ApplyToFighters(). " +
             "If this is wrong: fighter can't jump + camera jitters on Y.")]
    [SerializeField] private float _groundY = 0f;

    [Header("Coyote Time")]
    [Tooltip("Seconds after walking off a ledge where the fighter " +
             "can still jump. Set to 0 to disable.")]
    [SerializeField][Range(0f, 0.2f)] private float _coyoteTime = 0.08f;

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC PROPERTIES
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
    /// </summary>
    public bool CanCoyoteJump => _coyoteTimer > 0f;

    /// <summary>How many jumps the fighter has used since last landing.</summary>
    public int JumpsUsed { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────

    private float _moveInput;
    private bool _jumpRequested;
    private float _coyoteTimer;
    private bool _wasGrounded;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Position = transform.position;
    }

    private void Start()
    {
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnPushboxOverlap += HandlePushboxOverlap;
    }

    private void OnDestroy()
    {
        if (CollisionManager.Instance != null)
            CollisionManager.Instance.OnPushboxOverlap -= HandlePushboxOverlap;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        ApplyGravity(dt);
        ApplyHorizontalMovement(dt);
        ProcessJump();
        IntegratePosition(dt);
        GroundCheck();
        ClampToStageBounds();
        UpdateCoyoteTimer(dt);
        SyncTransform();

        _moveInput = 0f;
        _jumpRequested = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API — called by FighterController each frame
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by StageBootstrap.ApplyToFighters() after each scene load.
    /// Pushes the stage's real floor Y and wall bounds into this body.
    /// Without this call, _groundY stays at 0 regardless of stage geometry —
    /// causing IsGrounded to never fire on stages where the floor isn't at Y=0.
    /// </summary>
    public void SetStageSettings(float groundY, float leftWall, float rightWall)
    {
        _groundY = groundY;
        _stageLeftWall = leftWall;
        _stageRightWall = rightWall;

        // If the fighter was placed above the new groundY, snap them to it immediately
        // so they don't fall through or float on the first frame.
        if (Position.y < _groundY)
        {
            Position = new Vector2(Position.x, _groundY);
            IsGrounded = true;
            Velocity = new Vector2(Velocity.x, 0f);
            SyncTransform();
        }
    }

    public void SetPositionX(float newX)
    {
        Position = new Vector2(newX, Position.y);
        transform.position = new Vector3(Position.x, Position.y, transform.position.z);
    }

    public void SetMoveInput(float input)
    {
        _moveInput = Mathf.Clamp(input, -1f, 1f);
    }

    /// <summary>
    /// Request a jump this frame. The actual jump happens in FixedUpdate.
    /// Buffering prevents the jump from being missed on a FixedUpdate boundary.
    /// </summary>
    public void RequestJump()
    {
        _jumpRequested = true;
    }

    /// <summary>
    /// Instantly sets velocity.
    /// Used for: knockback, launch moves, dashes.
    /// </summary>
    public void SetVelocity(Vector2 newVelocity)
    {
        Velocity = newVelocity;
    }

    public void ApplyKnockback(float force)
    {
        // Push direction is opposite to facing direction
        float pushDirection = transform.eulerAngles.y < 90f ? -1f : 1f;
        SetVelocity(new Vector2(force * pushDirection * 3f, Velocity.y));
    }

    /// <summary>
    /// Adds velocity on top of existing. Used for jump-cancel moves.
    /// </summary>
    public void AddVelocity(Vector2 delta)
    {
        Velocity += delta;
    }

    /// <summary>
    /// Instantly stops horizontal movement.
    /// Called when the fighter enters hitstun.
    /// </summary>
    public void StopHorizontal()
    {
        Velocity = new Vector2(0f, Velocity.y);
    }

    /// <summary>
    /// Instantly stops all movement. Called on death or round reset.
    /// </summary>
    public void StopAll()
    {
        Velocity = Vector2.zero;
    }

    /// <summary>
    /// Teleports the fighter to a position.
    /// Used by GameManager to reset fighters at the beginning of each round.
    /// </summary>
    public void Warp(Vector2 targetPosition)
    {
        Position = targetPosition;
        Velocity = Vector2.zero;
        _moveInput = 0f;
        _jumpRequested = false;
        IsGrounded = true;
        SyncTransform();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PHYSICS STEPS — called in order from FixedUpdate
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// STEP 1 — Gravity
    /// While airborne, accelerate downward each frame.
    /// </summary>
    private void ApplyGravity(float dt)
    {
        if (IsGrounded) return;

        float newVY = Velocity.y - (_gravity * dt);

        // Hard cap — prevents infinite velocity from accumulating
        newVY = Mathf.Clamp(newVY, -_maxFallSpeed, 50f);

        Velocity = new Vector2(Velocity.x, newVY);
    }

    /// <summary>
    /// STEP 2 — Horizontal movement
    /// Lerp toward target speed using friction. groundFriction = 1 = instant stop.
    /// </summary>
    private void ApplyHorizontalMovement(float dt)
    {
        float targetVX = _moveInput * _moveSpeed;

        float newVX;
        if (IsGrounded)
        {
            newVX = Mathf.Lerp(Velocity.x, targetVX, _groundFriction);
        }
        else
        {
            // Reduced air control — standard fighting game feel
            float airControlFactor = 0.6f;
            newVX = Mathf.Lerp(Velocity.x, targetVX, _groundFriction * airControlFactor);
        }

        Velocity = new Vector2(newVX, Velocity.y);
    }

    /// <summary>
    /// STEP 3 — Process jump request
    /// Fires if: grounded, within coyote window, or double-jump remaining.
    /// </summary>
    private void ProcessJump()
    {
        if (!_jumpRequested) return;

        bool canJump = (IsGrounded || CanCoyoteJump) && JumpsUsed < _maxJumps
                    || (!IsGrounded && JumpsUsed < _maxJumps);

        if (!canJump) return;

        Velocity = new Vector2(Velocity.x, _jumpForce);
        JumpsUsed++;
        IsGrounded = false;
        _coyoteTimer = 0f;
    }

    /// <summary>
    /// STEP 4 — Integrate position
    /// new position = old position + (velocity × time). Euler integration.
    /// </summary>
    private void IntegratePosition(float dt)
    {
        Position += Velocity * dt;
    }

    /// <summary>
    /// STEP 5 — Ground check
    ///
    /// After moving, check if we've reached or passed the floor.
    /// If so, snap to _groundY and zero downward velocity (landing).
    ///
    /// NOTE: _groundY is set per-stage by StageBootstrap.ApplyToFighters().
    /// If _groundY doesn't match the actual floor, IsGrounded is always false
    /// → can't jump, gravity oscillates vs floor collider → camera jitters.
    /// </summary>
    private void GroundCheck()
    {
        _wasGrounded = IsGrounded;

        if (Position.y <= _groundY)
        {
            // Snap to ground level
            Position = new Vector2(Position.x, _groundY);

            // Kill downward velocity on landing
            if (Velocity.y < 0f)
                Velocity = new Vector2(Velocity.x, 0f);

            if (!_wasGrounded)
                OnLanded();

            IsGrounded = true;
        }
        else
        {
            // Became airborne — start coyote timer
            if (_wasGrounded && !IsGrounded)
                _coyoteTimer = _coyoteTime;

            IsGrounded = false;
        }
    }

    /// <summary>
    /// Called the exact frame the fighter touches the ground.
    /// </summary>
    private void OnLanded()
    {
        JumpsUsed = 0;
    }

    /// <summary>
    /// STEP 6 — Clamp to stage boundaries
    /// Kills horizontal velocity when hitting a wall.
    /// </summary>
    private void ClampToStageBounds()
    {
        float clampedX = Mathf.Clamp(Position.x, _stageLeftWall, _stageRightWall);

        if (!Mathf.Approximately(clampedX, Position.x))
            Velocity = new Vector2(0f, Velocity.y);

        Position = new Vector2(clampedX, Position.y);
    }

    private void UpdateCoyoteTimer(float dt)
    {
        if (_coyoteTimer > 0f)
            _coyoteTimer -= dt;
    }

    /// <summary>
    /// FINAL STEP — Push calculated position back to the Unity Transform.
    /// We write to Transform ONCE per frame (expensive — triggers matrix recalc).
    /// </summary>
    private void SyncTransform()
    {
        transform.position = new Vector3(Position.x, Position.y, transform.position.z);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUSHBOX SEPARATION — called by CollisionManager event
    // ─────────────────────────────────────────────────────────────────────────

    private void HandlePushboxOverlap(float overlapDepth)
    {
        PhysicsBody opponent = FindOpponent();
        if (opponent == null) return;

        float pushDirection = Position.x < opponent.Position.x ? -1f : 1f;
        float halfDepth = overlapDepth * 0.5f;

        Position = new Vector2(Position.x + pushDirection * halfDepth, Position.y);

        ClampToStageBounds();
        SyncTransform();
    }

    private PhysicsBody FindOpponent()
    {
        PhysicsBody[] allBodies = FindObjectsOfType<PhysicsBody>();
        foreach (PhysicsBody body in allBodies)
        {
            if (body != this) return body;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DEBUG GIZMOS — visible in Scene view while playing
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        // Green line = ground
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
        Gizmos.DrawLine(
            new Vector3(_stageLeftWall, _groundY, 0f),
            new Vector3(_stageRightWall, _groundY, 0f));

        // Orange lines = stage walls
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