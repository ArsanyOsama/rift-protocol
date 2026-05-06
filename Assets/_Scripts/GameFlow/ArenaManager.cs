using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArenaManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The Main Camera in your scene.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Fighter 1 PhysicsBody (P1).")]
    [SerializeField] private PhysicsBody _fighterA;

    [Tooltip("Fighter 2 PhysicsBody (P2).")]
    [SerializeField] private PhysicsBody _fighterB;

    [Header("Stage boundaries")]
    [Tooltip("X position of the left wall in world space. " +
             "Match this to your background art edge.")]
    [SerializeField] private float _stageLeft = -8f;

    [Tooltip("X position of the right wall in world space.")]
    [SerializeField] private float _stageRight = 8f;

    [Tooltip("Y position of the ground. Usually 0.")]
    [SerializeField] private float _groundY = 0f;

    [Header("Camera settings")]
    [Tooltip("How quickly the camera moves toward the midpoint. " +
             "Higher = snappier. Lower = floatier. Range: 3–10.")]
    [SerializeField][Range(1f, 20f)] private float _cameraLerpSpeed = 6f;

    [Tooltip("The camera's Y position. Set this so fighters are " +
             "visible from head to toe in your background.")]
    [SerializeField] private float _cameraY = 3f;

    [Tooltip("The camera's Z position. Must be negative so it " +
             "looks into the scene. Typical: -10.")]
    [SerializeField] private float _cameraZ = -10f;

    [Tooltip("Orthographic size when fighters are at minimum distance apart. " +
             "Smaller = more zoomed in.")]
    [SerializeField] private float _minCameraSize = 4f;

    [Tooltip("Orthographic size when fighters are at maximum distance apart. " +
             "Larger = more zoomed out.")]
    [SerializeField] private float _maxCameraSize = 7f;

    [Tooltip("Fighter distance at which camera is fully zoomed in.")]
    [SerializeField] private float _minFighterDistance = 2f;

    [Tooltip("Fighter distance at which camera is fully zoomed out.")]
    [SerializeField] private float _maxFighterDistance = 12f;

    [Tooltip("How smoothly the camera size changes. Higher = snappier.")]
    [SerializeField][Range(1f, 20f)] private float _zoomLerpSpeed = 4f;

    [Header("Corner push")]
    [Tooltip("Width of the corner zone on each side of the stage. " +
             "When a fighter is inside this zone, the opponent gets pushed back.")]
    [SerializeField] private float _cornerZoneWidth = 1.5f;

    [Tooltip("How hard the corner pushes back the non-cornered fighter. " +
             "Matches the movement speed of the cornered fighter exactly so " +
             "the attacker can't push through. Range: 1–10.")]
    [SerializeField][Range(0f, 10f)] private float _cornerPushStrength = 5f;

    [Header("Minimum separation")]
    [Tooltip("Fighters can never be closer than this in world units. " +
             "Safety net on top of the pushbox system.")]
    [SerializeField] private float _minSeparation = 0.8f;

    // ─────────────────────────────────────────────────────────────────────────
    //  SINGLETON
    // ─────────────────────────────────────────────────────────────────────────

    public static ArenaManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC READ-ONLY STATE
    //  Other systems (UI, SFX) can read these without needing their own
    //  calculations.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Current world-space midpoint between the two fighters.</summary>
    public Vector2 FighterMidpoint { get; private set; }

    /// <summary>Current distance between the two fighters in world units.</summary>
    public float FighterDistance { get; private set; }

    /// <summary>True if Fighter A is pinned against the left or right wall.</summary>
    public bool IsFighterAInCorner { get; private set; }

    /// <summary>True if Fighter B is pinned against the left or right wall.</summary>
    public bool IsFighterBInCorner { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (_camera == null) Debug.LogError("[ArenaManager] Camera not assigned!");
    }

    private void FixedUpdate()
    {
        if (_fighterA == null || _fighterB == null) return;

        // Order matters: calculate spatial data first, then use it
        UpdateSpatialData();
        EnforceMinimumSeparation();
        HandleCornerPush();
        UpdateCamera();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STEP 1 — UPDATE SPATIAL DATA
    //  Calculates midpoint and distance — used by everything else this frame.
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateSpatialData()
    {
        Vector2 posA = _fighterA.Position;
        Vector2 posB = _fighterB.Position;

        FighterMidpoint = (posA + posB) * 0.5f;
        FighterDistance = Mathf.Abs(posA.x - posB.x);

        // Corner detection: is a fighter within the corner zone?
        float leftCornerEdge = _stageLeft + _cornerZoneWidth;
        float rightCornerEdge = _stageRight - _cornerZoneWidth;

        IsFighterAInCorner = posA.x <= leftCornerEdge || posA.x >= rightCornerEdge;
        IsFighterBInCorner = posB.x <= leftCornerEdge || posB.x >= rightCornerEdge;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STEP 2 — ENFORCE MINIMUM SEPARATION
    //
    //  If the two fighters' centers are closer than _minSeparation,
    //  push them apart symmetrically.
    //
    //  This is a SAFETY NET. The primary separation comes from the pushbox
    //  system in CollisionManager. This catches edge cases where that fails
    //  (e.g. a grab move that teleports a fighter, or a frame-rate spike).
    // ─────────────────────────────────────────────────────────────────────────

    private void EnforceMinimumSeparation()
    {
        // 1. Vertical Check: Are they jumping over each other?
        // If the height difference is greater than 1.5 units, allow the cross-up!
        float verticalDist = Mathf.Abs(_fighterB.Position.y - _fighterA.Position.y);
        if (verticalDist > 1.5f) return;

        // 2. Horizontal Check
        // Use Mathf.Abs to get the true distance, regardless of who is on the left
        float dist = Mathf.Abs(_fighterB.Position.x - _fighterA.Position.x);

        // If they are closer than the minimum allowed distance on the ground...
        if (dist < _minSeparation)
        {
            // Calculate half the overlap
            float overlap = _minSeparation - dist;
            float correction = overlap * 0.5f;

            // Figure out who is on the left right now
            float dirA = _fighterA.Position.x <= _fighterB.Position.x ? -1f : 1f;
            float dirB = -dirA; // B goes the opposite direction of A

            // Push them apart
            _fighterA.SetPositionX(_fighterA.Position.x + (dirA * correction));
            _fighterB.SetPositionX(_fighterB.Position.x + (dirB * correction));

            // Recalculate spatial data immediately since positions changed
            UpdateSpatialData();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STEP 3 — CORNER PUSH
    //
    //  WHAT IS CORNER PUSH?
    //    In fighting games, when fighter A backs fighter B into the corner,
    //    B literally cannot retreat any further. The wall stops them.
    //    But A can keep pressing forward.
    //
    //    To prevent A from "walking into" B and overlapping them,
    //    we detect this situation and push A BACKWARD by the same
    //    amount B would have moved — as if the corner is "reflecting"
    //    the pressure back at the attacker.
    //
    //    This creates a fair dynamic: the corner is advantageous for
    //    the attacker (they can keep attacking), but they can't keep
    //    gaining ground by walking forward.
    //
    //  HOW IT WORKS:
    //    Each frame we check: is Fighter B in the corner AND is Fighter A
    //    trying to move toward that same corner?
    //    If yes → apply an equal opposite push to Fighter A.
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleCornerPush()
    {
        ApplyCornerPushForPair(_fighterA, _fighterB, IsFighterBInCorner);
        ApplyCornerPushForPair(_fighterB, _fighterA, IsFighterAInCorner);
    }

    /// <summary>
    /// If 'cornered' is in the corner and 'attacker' is moving toward it,
    /// push 'attacker' back by the corner push strength.
    /// </summary>
    private void ApplyCornerPushForPair(PhysicsBody attacker,
                                         PhysicsBody cornered,
                                         bool corneredIsInCorner)
    {
        if (!corneredIsInCorner) return;

        // Is attacker moving TOWARD the cornered fighter?
        float directionToCorner = Mathf.Sign(cornered.Position.x - attacker.Position.x);
        float attackerHorizontalVelocity = attacker.Velocity.x;

        bool movingTowardCorner = Mathf.Sign(attackerHorizontalVelocity) == directionToCorner
                                  && Mathf.Abs(attackerHorizontalVelocity) > 0.1f;

        if (!movingTowardCorner) return;

        // Push attacker in the OPPOSITE direction at corner push strength
        // We directly offset their position this frame — we don't add velocity
        // because velocity would carry into next frame and feel floaty.
        float pushDir = -directionToCorner;
        float pushDelta = pushDir * _cornerPushStrength * Time.fixedDeltaTime;

        Vector2 newPos = new Vector2(
            Mathf.Clamp(attacker.Position.x + pushDelta, _stageLeft, _stageRight),
            attacker.Position.y
        );

        attacker.Warp(newPos);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STEP 4 — CAMERA UPDATE
    //
    //  The camera does three things every frame:
    //
    //  A) FOLLOW the midpoint between fighters horizontally.
    //     It uses Lerp (linear interpolation) to smooth the movement.
    //     Lerp(current, target, t) moves 't' fraction of the way to target
    //     each frame — this creates the characteristic "easing" feel.
    //
    //  B) ZOOM based on distance between fighters.
    //     When fighters are close, zoom in (small orthographic size).
    //     When fighters are far, zoom out (large orthographic size).
    //     This keeps both fighters on screen at all times.
    //     Uses InverseLerp to map distance → 0..1 range, then Lerp to
    //     map that → camera size range.
    //
    //  C) CLAMP the camera so it never shows outside the stage.
    //     If the stage is narrower than what the camera can see, center it.
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateCamera()
    {
        if (_camera == null) return;

        // ── A) Calculate target X (horizontal tracking) ───────────────────────

        float targetX = FighterMidpoint.x;

        // Clamp so the camera never shows past the stage walls.
        // The camera shows (cameraSize * aspectRatio) world units wide.
        // Half that is the margin we need to keep inside the stage.
        float halfCameraWidth = _camera.orthographicSize * _camera.aspect;
        float clampedX = Mathf.Clamp(
            targetX,
            _stageLeft + halfCameraWidth,
            _stageRight - halfCameraWidth
        );

        // ── B) Calculate target orthographic size (zoom) ──────────────────────

        // InverseLerp converts fighter distance into a 0..1 "zoom factor"
        // 0 = fighters at min distance (zoom in), 1 = at max distance (zoom out)
        float zoomT = Mathf.InverseLerp(_minFighterDistance, _maxFighterDistance,
                                               FighterDistance);
        float targetSize = Mathf.Lerp(_minCameraSize, _maxCameraSize, zoomT);

        // ── C) Apply with smoothing ───────────────────────────────────────────

        float dt = Time.fixedDeltaTime;

        // Smooth horizontal position
        float smoothX = Mathf.Lerp(
            _camera.transform.position.x,
            clampedX,
            _cameraLerpSpeed * dt
        );

        // Smooth zoom
        float smoothSize = Mathf.Lerp(
            _camera.orthographicSize,
            targetSize,
            _zoomLerpSpeed * dt
        );

        // Write to the camera — keep Y and Z fixed
        _camera.transform.position = new Vector3(smoothX, _cameraY, _cameraZ);
        _camera.orthographicSize = smoothSize;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API — called by GameManager at round start/end
    // ─────────────────────────────────────────────────────────────────────────
    public void SetFighters(PhysicsBody p1, PhysicsBody p2)
    {
        _fighterA = p1;
        _fighterB = p2;
    }
    /// <summary>
    /// Resets both fighters to their starting positions for a new round.
    /// Called by GameManager when a round begins.
    ///
    /// Fighters start at symmetric positions either side of center.
    /// The offset ensures they aren't overlapping but are close enough
    /// to feel threatening from frame one.
    /// </summary>
    public void ResetFightersToStart()
    {
        float startOffset = 2.5f;   // world units from center

        _fighterA?.Warp(new Vector2(-startOffset, _groundY));
        _fighterB?.Warp(new Vector2(startOffset, _groundY));
    }

    /// <summary>
    /// Snaps the camera immediately to the correct position.
    /// Used at round start so the camera doesn't Lerp in from somewhere else.
    /// </summary>
    public void SnapCameraToCenter()
    {
        if (_camera == null) return;

        _camera.transform.position = new Vector3(0f, _cameraY, _cameraZ);
        _camera.orthographicSize = _minCameraSize;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  QUERY HELPERS — read by UI and other systems
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the X position clamped inside stage boundaries.
    /// Used by any system that needs to place something "on stage."
    /// </summary>
    public float ClampToStage(float x) =>
        Mathf.Clamp(x, _stageLeft, _stageRight);

    /// <summary>
    /// Returns which side of center a world X position is on.
    /// Positive = right side, negative = left side.
    /// Used by UI to decide which health bar to flash.
    /// </summary>
    public float SideSign(float worldX) =>
        worldX >= 0f ? 1f : -1f;

    // ─────────────────────────────────────────────────────────────────────────
    //  DEBUG GIZMOS
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        float wallHeight = 5f;

        // Stage floor
        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.5f);
        Gizmos.DrawLine(
            new Vector3(_stageLeft, _groundY, 0f),
            new Vector3(_stageRight, _groundY, 0f));

        // Stage walls
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawLine(new Vector3(_stageLeft, _groundY, 0f),
                        new Vector3(_stageLeft, _groundY + wallHeight, 0f));
        Gizmos.DrawLine(new Vector3(_stageRight, _groundY, 0f),
                        new Vector3(_stageRight, _groundY + wallHeight, 0f));

        // Corner push zones
        Gizmos.color = new Color(0.1f, 0.8f, 0.8f, 0.2f);
        float zoneH = wallHeight;
        // Left corner zone
        Gizmos.DrawCube(
            new Vector3(_stageLeft + _cornerZoneWidth * 0.5f, _groundY + zoneH * 0.5f, 0f),
            new Vector3(_cornerZoneWidth, zoneH, 0.1f));
        // Right corner zone
        Gizmos.DrawCube(
            new Vector3(_stageRight - _cornerZoneWidth * 0.5f, _groundY + zoneH * 0.5f, 0f),
            new Vector3(_cornerZoneWidth, zoneH, 0.1f));

        // Midpoint indicator (only in play mode)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(new Vector3(FighterMidpoint.x, FighterMidpoint.y, 0f), 0.1f);
        }
    }
}