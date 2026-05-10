using System;
using System.Collections.Generic;
using UnityEngine;

public interface IBoxProvider
{
    int PlayerIndex { get; }
    Vector2 Position { get; }
    FacingDirection Facing { get; }
    IReadOnlyList<BoxData> GetActiveBoxes();
    bool IsBlocking { get; }
    bool IsCrouching { get; }
}

public class CollisionManager : MonoBehaviour
{
    [Header("Fighters")]
    [SerializeField] private MonoBehaviour _fighterASource;
    [SerializeField] private MonoBehaviour _fighterBSource;

    [Header("Debug visuals")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private float _gizmoAlpha = 0.35f;

    // ── Original Events ───────────────────────────────────────────────────────
    public event Action<HitEvent> OnHitConfirmed;
    public event Action<float> OnPushboxOverlap;

    // ── ADDED: Events FighterAnimationController subscribes to ────────────────
    public event System.Action<HitEvent> OnReceivedHit;
    public event System.Action OnKnockdown;
    public event System.Action OnGrabReceived;

    public static CollisionManager Instance { get; private set; }

    private IBoxProvider _fighterA;
    private IBoxProvider _fighterB;

    private readonly List<(BoxData box, Rect worldRect)> _rectsA = new();
    private readonly List<(BoxData box, Rect worldRect)> _rectsB = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CollisionManager] Duplicate detected — destroying extra.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _fighterA = _fighterASource as IBoxProvider;
        _fighterB = _fighterBSource as IBoxProvider;
    }

    public void SetFighters(IBoxProvider p1, IBoxProvider p2)
    {
        _fighterA = p1;
        _fighterB = p2;
    }

    private void FixedUpdate()
    {
        if (_fighterA == null || _fighterB == null) return;

        BuildWorldRects(_fighterA, _rectsA);
        BuildWorldRects(_fighterB, _rectsB);

        CheckHitboxVsHurtbox(_fighterA, _rectsA, _fighterB, _rectsB);
        CheckHitboxVsHurtbox(_fighterB, _rectsB, _fighterA, _rectsA);

        CheckPushboxOverlap(_rectsA, _rectsB);
    }

    private void BuildWorldRects(IBoxProvider fighter, List<(BoxData, Rect)> output)
    {
        output.Clear();
        IReadOnlyList<BoxData> activeBoxes = fighter.GetActiveBoxes();

        foreach (BoxData box in activeBoxes)
        {
            Rect worldRect = box.GetWorldRect(fighter.Position, fighter.Facing);
            output.Add((box, worldRect));
        }
    }

    private void CheckHitboxVsHurtbox(
        IBoxProvider attacker, List<(BoxData box, Rect worldRect)> attackerRects,
        IBoxProvider defender, List<(BoxData box, Rect worldRect)> defenderRects)
    {
        foreach (var (atkBox, atkRect) in attackerRects)
        {
            if (atkBox.type != BoxType.Hitbox) continue;
            if (atkBox.hasConnected) continue;

            foreach (var (defBox, defRect) in defenderRects)
            {
                if (defBox.type != BoxType.Hurtbox) continue;
                if (!atkRect.Overlaps(defRect)) continue;

                HitResult result = ResolveHitResult(atkBox, attacker, defender);
                Rect overlap = GetOverlapRect(atkRect, defRect);
                Vector2 contact = overlap.center;
                int damageDealt = CalculateDamage(atkBox, result);

                HitEvent hitEvent = new HitEvent
                {
                    result = result,
                    attackerIndex = attacker.PlayerIndex,
                    defenderIndex = defender.PlayerIndex,
                    sourceBox = atkBox,
                    contactPoint = contact,
                    damageDealt = damageDealt
                };

                // Fire original event
                OnHitConfirmed?.Invoke(hitEvent);

                // ── ADDED: Fire the new specific events ────────────────────────
                OnReceivedHit?.Invoke(hitEvent);
                if (atkBox.damage >= 22) OnKnockdown?.Invoke(); // Example threshold for KD
                if (atkBox.hitType == HitType.Grab) OnGrabReceived?.Invoke();

                MarkBoxConnected(attackerRects, atkBox);
                break;
            }
        }
    }

    private HitResult ResolveHitResult(BoxData atkBox, IBoxProvider attacker, IBoxProvider defender)
    {
        if (atkBox.hitType == HitType.Grab) return HitResult.Hit;
        if (!defender.IsBlocking) return HitResult.Hit;

        bool guardCoversThisHit = atkBox.hitType switch
        {
            HitType.Overhead => !defender.IsCrouching,
            HitType.Low => defender.IsCrouching,
            HitType.Mid => true,
            HitType.High => true,
            HitType.Projectile => true,
            _ => true
        };

        return guardCoversThisHit ? HitResult.Blocked : HitResult.Hit;
    }

    private int CalculateDamage(BoxData atkBox, HitResult result)
    {
        if (result == HitResult.Blocked) return 0;
        return atkBox.damage;
    }

    private void MarkBoxConnected(List<(BoxData box, Rect worldRect)> rects, BoxData target)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i].box.type == target.type && rects[i].box.offset == target.offset)
            {
                BoxData updated = rects[i].box;
                updated.hasConnected = true;
                rects[i] = (updated, rects[i].worldRect);
                return;
            }
        }
    }

    private void CheckPushboxOverlap(List<(BoxData box, Rect worldRect)> rectsA, List<(BoxData box, Rect worldRect)> rectsB)
    {
        Rect pushA = default;
        Rect pushB = default;
        bool foundA = false, foundB = false;

        foreach (var (box, rect) in rectsA) { if (box.type == BoxType.Pushbox) { pushA = rect; foundA = true; break; } }
        foreach (var (box, rect) in rectsB) { if (box.type == BoxType.Pushbox) { pushB = rect; foundB = true; break; } }

        if (!foundA || !foundB) return;
        if (!pushA.Overlaps(pushB)) return;

        float overlapDepth = Mathf.Min(pushA.xMax, pushB.xMax) - Mathf.Max(pushA.xMin, pushB.xMin);
        OnPushboxOverlap?.Invoke(overlapDepth);
    }

    private Rect GetOverlapRect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ── ADDED: Hitbox API ─────────────────────────────────────────────────────
    public void EnableHitbox(string hitboxName, int playerIndex)
    {
        Debug.Log($"[CollisionManager] EnableHitbox '{hitboxName}' P{playerIndex + 1}");
    }

    public void DisableHitbox(string hitboxName, int playerIndex)
    {
        Debug.Log($"[CollisionManager] DisableHitbox '{hitboxName}' P{playerIndex + 1}");
    }

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
                BoxType.Hitbox => new Color(1f, 0f, 0f, _gizmoAlpha),
                BoxType.Hurtbox => new Color(0f, 1f, 0f, _gizmoAlpha),
                BoxType.Pushbox => new Color(0f, 0.4f, 1f, _gizmoAlpha),
                BoxType.ProximityGuard => new Color(1f, 0.9f, 0f, _gizmoAlpha),
                _ => Color.white
            };

            Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
            Vector3 size = new Vector3(rect.width, rect.height, 0.1f);

            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}