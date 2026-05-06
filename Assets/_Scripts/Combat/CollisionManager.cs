// CollisionManager.cs
// Assets/Scripts/Core/CollisionManager.cs
//
// WHAT THIS FILE DOES:
//   Every physics frame (FixedUpdate), this system:
//     1. Asks both fighters for their active boxes this frame
//     2. Converts those boxes from local → world space
//     3. Tests every Hitbox from Fighter A against every Hurtbox on Fighter B (and vice versa)
//     4. Resolves the result (Hit, Blocked, Traded, Whiff)
//     5. Fires a C# event so any listener (FighterController, UI, SFX, etc.) can react
//
// HOW TO USE IN UNITY:
//   - Create an empty GameObject in your scene called "CollisionManager"
//   - Attach this script to it
//   - Drag both FighterController GameObjects into the two fighter slots in the Inspector
//   - In FighterController, implement IBoxProvider (shown at the bottom of this file)

using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  INTERFACE — what CollisionManager needs from each fighter
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Any object that has collision boxes must implement this.
/// CollisionManager will call these methods every FixedUpdate.
///
/// Think of it like a contract: "if you want to be part of the
/// collision system, you must be able to answer these questions."
/// </summary>
public interface IBoxProvider
{
    /// <summary>Player index. 0 = P1, 1 = P2.</summary>
    int PlayerIndex { get; }

    /// <summary>Fighter's current world-space position (feet on ground).</summary>
    Vector2 Position { get; }

    /// <summary>Which way the fighter is facing right now.</summary>
    FacingDirection Facing { get; }

    /// <summary>
    /// Returns ALL boxes that are ACTIVE this frame.
    /// A box is active if the fighter's current animation frame
    /// says it should exist.  Inactive boxes (e.g. the punch
    /// hitbox on frame 1 before the fist extends) are NOT returned.
    /// </summary>
    IReadOnlyList<BoxData> GetActiveBoxes();

    /// <summary>
    /// True if the fighter is currently in a blocking state
    /// (BlockingStanding or BlockingCrouching).
    /// Used by CollisionManager to decide Hit vs Blocked.
    /// </summary>
    bool IsBlocking { get; }

    /// <summary>True if the fighter is crouching (for low-hit blocking rules).</summary>
    bool IsCrouching { get; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  MAIN CLASS
// ─────────────────────────────────────────────────────────────────────────────

public class CollisionManager : MonoBehaviour
{
    // ── Inspector fields ──────────────────────────────────────────────────────

    [Header("Fighters")]
    [Tooltip("Drag the P1 FighterController GameObject here")]
    [SerializeField] private MonoBehaviour _fighterASource;   // must implement IBoxProvider

    [Tooltip("Drag the P2 FighterController GameObject here")]
    [SerializeField] private MonoBehaviour _fighterBSource;

    [Header("Debug visuals")]
    [Tooltip("Show hitboxes and hurtboxes as coloured rectangles in the Scene view")]
    [SerializeField] private bool _drawGizmos = true;

    [Tooltip("Red = Hitbox, Green = Hurtbox, Blue = Pushbox, Yellow = ProximityGuard")]
    [SerializeField] private float _gizmoAlpha = 0.35f;

    // ── C# Events — subscribe to these from FighterController, UI, SFX ───────

    /// <summary>
    /// Fired whenever a Hitbox connects with a Hurtbox (or is blocked).
    ///
    /// HOW TO SUBSCRIBE (in FighterController.Start):
    ///   CollisionManager.Instance.OnHitConfirmed += HandleHit;
    ///
    /// HOW TO UNSUBSCRIBE (in FighterController.OnDestroy):
    ///   CollisionManager.Instance.OnHitConfirmed -= HandleHit;
    /// </summary>
    public event Action<HitEvent> OnHitConfirmed;

    /// <summary>
    /// Fired when two Pushboxes overlap — both fighters must be
    /// nudged apart.  ArenaManager and FighterController listen to this.
    /// </summary>
    public event Action<float> OnPushboxOverlap;   // float = overlap depth

    // ── Singleton ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Static reference so any class can write:
    ///   CollisionManager.Instance.OnHitConfirmed += ...
    /// without needing a serialized reference.
    /// </summary>
    public static CollisionManager Instance { get; private set; }

    // ── Private state ─────────────────────────────────────────────────────────

    private IBoxProvider _fighterA;
    private IBoxProvider _fighterB;

    // We cache world-space rects every frame to avoid recalculating them
    // inside the inner loop.
    private readonly List<(BoxData box, Rect worldRect)> _rectsA = new();
    private readonly List<(BoxData box, Rect worldRect)> _rectsB = new();

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Singleton setup — there should only ever be one CollisionManager
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CollisionManager] Duplicate detected — destroying extra.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Cast the MonoBehaviours to IBoxProvider so we can call their methods
        // The 'as' keyword returns null if the cast fails (safer than a hard cast)
        _fighterA = _fighterASource as IBoxProvider;
        _fighterB = _fighterBSource as IBoxProvider;

        // Warn the developer in the Unity Console if something is wired wrong
        if (_fighterA == null)
            Debug.LogError("[CollisionManager] Fighter A does not implement IBoxProvider!");
        if (_fighterB == null)
            Debug.LogError("[CollisionManager] Fighter B does not implement IBoxProvider!");
    }

    /// <summary>
    /// FixedUpdate runs at a FIXED timestep (default 50 times/sec in Unity).
    /// We use FixedUpdate — not Update — because collision detection must be
    /// frame-rate independent.  If you run at 144 fps the boxes should still
    /// only be checked at the physics rate.
    /// </summary>
    private void FixedUpdate()
    {
        if (_fighterA == null || _fighterB == null) return;

        // Step 1 & 2 — gather boxes and convert them to world space
        BuildWorldRects(_fighterA, _rectsA);
        BuildWorldRects(_fighterB, _rectsB);

        // Step 3, 4 & 5 — check A's hitboxes vs B's hurtboxes, then reverse
        CheckHitboxVsHurtbox(_fighterA, _rectsA, _fighterB, _rectsB);
        CheckHitboxVsHurtbox(_fighterB, _rectsB, _fighterA, _rectsA);

        // Pushbox check — separate concern, fires its own event
        CheckPushboxOverlap(_rectsA, _rectsB);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STEP 1 & 2 — BUILD WORLD-SPACE RECTS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears the cache list then rebuilds it with fresh world-space rects.
    ///
    /// WHY WE CONVERT TO WORLD SPACE:
    ///   BoxData stores offsets in local space (relative to the fighter).
    ///   To compare boxes from two different fighters we need a common
    ///   coordinate system — world space.  GetWorldRect() handles the flip
    ///   for the facing direction automatically.
    /// </summary>
    private void BuildWorldRects(IBoxProvider fighter,
                                  List<(BoxData, Rect)> output)
    {
        output.Clear();

        IReadOnlyList<BoxData> activeBoxes = fighter.GetActiveBoxes();

        foreach (BoxData box in activeBoxes)
        {
            // GetWorldRect is defined in BoxData (our struct from Part 1)
            Rect worldRect = box.GetWorldRect(fighter.Position, fighter.Facing);
            output.Add((box, worldRect));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STEP 3, 4 & 5 — HITBOX vs HURTBOX CHECK
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The core detection loop.
    ///
    /// attacker = the fighter whose HITBOXES we are testing
    /// defender = the fighter whose HURTBOXES we are testing against
    ///
    /// This is called TWICE per frame: A vs B, then B vs A.
    /// That handles simultaneous hits (trades) naturally.
    /// </summary>
    private void CheckHitboxVsHurtbox(
        IBoxProvider attacker, List<(BoxData box, Rect worldRect)> attackerRects,
        IBoxProvider defender, List<(BoxData box, Rect worldRect)> defenderRects)
    {
        foreach (var (atkBox, atkRect) in attackerRects)
        {
            // Only process HITBOXES from the attacker
            if (atkBox.type != BoxType.Hitbox) continue;

            // Skip if this hitbox already connected this attack cycle
            // (prevents dealing damage on every frame the boxes overlap —
            //  we only want ONE hit per swing)
            if (atkBox.hasConnected) continue;

            foreach (var (defBox, defRect) in defenderRects)
            {
                // Only test against HURTBOXES on the defender
                if (defBox.type != BoxType.Hurtbox) continue;

                // STEP 4 — AABB overlap test
                // Unity's Rect.Overlaps() returns true if the two rectangles
                // share any area. This is called AABB (Axis-Aligned Bounding Box).
                // It's fast because we only compare edges — no rotations.
                if (!atkRect.Overlaps(defRect)) continue;

                // ── We have a hit! Now figure out what KIND ──────────────────

                HitResult result = ResolveHitResult(atkBox, attacker, defender);

                // Calculate the world-space midpoint of the overlap area
                // (used for spawning hit sparks, sound effects, etc.)
                Rect overlap = GetOverlapRect(atkRect, defRect);
                Vector2 contact = overlap.center;

                // Calculate actual damage (could be modified by defender's
                // defence stat, stale-move negation, etc. — kept simple here)
                int damageDealt = CalculateDamage(atkBox, result);

                // Build the event payload
                HitEvent hitEvent = new HitEvent
                {
                    result = result,
                    attackerIndex = attacker.PlayerIndex,
                    defenderIndex = defender.PlayerIndex,
                    sourceBox = atkBox,
                    contactPoint = contact,
                    damageDealt = damageDealt
                };

                // STEP 5 — Fire the event
                // Any class subscribed to OnHitConfirmed will run its handler now.
                // FighterController uses this to apply hitstun, health loss, etc.
                OnHitConfirmed?.Invoke(hitEvent);

                // Mark the hitbox as "used" so it can't score again this swing
                // NOTE: Because BoxData is a struct (value type), we have to go
                // back to the source list to set the flag — structs are copied, not referenced.
                MarkBoxConnected(attackerRects, atkBox);

                // Break out of the inner loop — one hit per hitbox per swing
                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPER — RESOLVE HIT RESULT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decides whether the hit lands clean, gets blocked, or is invalid.
    ///
    /// BLOCKING RULES (like Street Fighter / Mortal Kombat):
    ///   • Overhead  → must be blocked STANDING   (crouch block doesn't work)
    ///   • Low       → must be blocked CROUCHING  (stand block doesn't work)
    ///   • Mid/High  → blocked either way
    ///   • Grab      → UNBLOCKABLE
    /// </summary>
    private HitResult ResolveHitResult(BoxData atkBox,
                                        IBoxProvider attacker,
                                        IBoxProvider defender)
    {
        // Grabs cannot be blocked
        if (atkBox.hitType == HitType.Grab)
            return HitResult.Hit;

        // Defender is not in a blocking state at all
        if (!defender.IsBlocking)
            return HitResult.Hit;

        // Defender IS blocking — check if their guard covers this hit type
        bool guardCoversThisHit = atkBox.hitType switch
        {
            HitType.Overhead => !defender.IsCrouching,  // must stand-block
            HitType.Low => defender.IsCrouching,  // must crouch-block
            HitType.Mid => true,                    // either guard works
            HitType.High => true,                    // either guard works
            HitType.Projectile => true,                    // blocked either way
            _ => true
        };

        return guardCoversThisHit ? HitResult.Blocked : HitResult.Hit;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPER — DAMAGE CALCULATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Translates raw box damage into actual damage dealt.
    ///
    /// Blocked hits deal 0 damage (guard chip damage is a separate system
    /// you can add later — some games give tiny chip damage on block).
    ///
    /// This is where you'd plug in combo scaling, defence modifiers, etc.
    /// </summary>
    private int CalculateDamage(BoxData atkBox, HitResult result)
    {
        if (result == HitResult.Blocked)
            return 0;   // No damage on block (chip damage = future feature)

        // For now, raw damage.  Later: multiply by combo_scaling_factor.
        return atkBox.damage;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPER — MARK BOX AS CONNECTED
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// IMPORTANT UNITY QUIRK — STRUCTS ARE COPIED, NOT REFERENCED.
    ///
    /// When you write:
    ///   foreach (var (box, rect) in list)
    ///       box.hasConnected = true;   ← THIS DOES NOTHING to the list!
    ///
    /// Because BoxData is a struct, 'box' is a COPY.
    /// We must find the entry in the list by index and replace it.
    /// </summary>
    private void MarkBoxConnected(List<(BoxData box, Rect worldRect)> rects,
                                   BoxData target)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            // We identify the right slot using type + offset as a key
            // (good enough for a fighting game with a small number of boxes)
            if (rects[i].box.type == target.type &&
                rects[i].box.offset == target.offset)
            {
                BoxData updated = rects[i].box;
                updated.hasConnected = true;
                rects[i] = (updated, rects[i].worldRect);
                return;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUSHBOX CHECK
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the two fighters' Pushboxes overlap.
    /// If they do, fires OnPushboxOverlap with the depth of the overlap.
    ///
    /// The PhysicsBody component (next system to build) listens to this
    /// and separates the fighters by moving them apart equally.
    ///
    /// WHY A SEPARATE PUSHBOX?
    ///   Without pushboxes, fighters can walk through each other.
    ///   But we can't use Unity's physics colliders because fighting game
    ///   movement must be deterministic and tightly controlled.
    ///   So we handle it manually here.
    /// </summary>
    private void CheckPushboxOverlap(
        List<(BoxData box, Rect worldRect)> rectsA,
        List<(BoxData box, Rect worldRect)> rectsB)
    {
        Rect pushA = default;
        Rect pushB = default;
        bool foundA = false, foundB = false;

        // Find the first (and usually only) Pushbox for each fighter
        foreach (var (box, rect) in rectsA)
        {
            if (box.type == BoxType.Pushbox) { pushA = rect; foundA = true; break; }
        }
        foreach (var (box, rect) in rectsB)
        {
            if (box.type == BoxType.Pushbox) { pushB = rect; foundB = true; break; }
        }

        if (!foundA || !foundB) return;
        if (!pushA.Overlaps(pushB)) return;

        // Calculate how deeply the two pushboxes are overlapping
        // (the caller uses this depth to know how far to push each fighter)
        float overlapDepth = Mathf.Min(pushA.xMax, pushB.xMax)
                           - Mathf.Max(pushA.xMin, pushB.xMin);

        OnPushboxOverlap?.Invoke(overlapDepth);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UTILITY — RECT INTERSECTION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the rectangle that is the intersection of two overlapping rects.
    /// Used to find the contact point for hit effects.
    ///
    /// Unity doesn't have a built-in "get overlap rect" method,
    /// so we calculate it manually.
    /// </summary>
    private Rect GetOverlapRect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DEBUG GIZMOS — draws boxes in the Unity Scene view
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// OnDrawGizmos is a Unity magic method — it runs in the Editor only,
    /// never in a shipped build.
    ///
    /// This is how you see your hitboxes visually while playtesting
    /// in the Scene window.  Press Play in Unity, switch to the Scene tab,
    /// and you'll see coloured rectangles around the fighters.
    ///
    ///   RED    = Hitbox  (the attacking area)
    ///   GREEN  = Hurtbox (the area that receives damage)
    ///   BLUE   = Pushbox (the solid body)
    ///   YELLOW = ProximityGuard
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !Application.isPlaying) return;

        DrawProviderGizmos(_rectsA);
        DrawProviderGizmos(_rectsB);
    }

    private void DrawProviderGizmos(List<(BoxData box, Rect worldRect)> rects)
    {
        foreach (var (box, rect) in rects)
        {
            Gizmos.color = box.type switch
            {
                BoxType.Hitbox => new Color(1f, 0f, 0f, _gizmoAlpha),      // red
                BoxType.Hurtbox => new Color(0f, 1f, 0f, _gizmoAlpha),      // green
                BoxType.Pushbox => new Color(0f, 0.4f, 1f, _gizmoAlpha),   // blue
                BoxType.ProximityGuard => new Color(1f, 0.9f, 0f, _gizmoAlpha),   // yellow
                _ => Color.white
            };

            // Unity's Gizmos.DrawCube needs a CENTER position and a SIZE vector
            // Rect gives us xMin/yMin and width/height, so we convert:
            Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
            Vector3 size = new Vector3(rect.width, rect.height, 0.1f);

            Gizmos.DrawCube(center, size);

            // Draw a solid border so we can see edges clearly
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}