using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// MoveExecutor is the bridge between input and actual move execution.
// FighterAnimationController subscribes to its events to drive visuals.
public class MoveExecutor : MonoBehaviour
{
    // Events — FighterAnimationController subscribes to all of these
    public event Action<MoveData> OnAttackStarted;   // fired when any attack begins
    public event Action<MoveData> OnSpecialActivated; // fired on special activation frame
    public event Action<int> OnComboUpdate;      // fired when combo count changes
    public event Action<DashDirection> OnDashStarted;     // fired on dash
    public event Action OnJumpStarted;      // fired on jump
    public event Action OnGrabAttempted;    // fired when grab input detected
    public event Action OnTaunt;            // fired on taunt input

    // Read by FighterAnimationController for combo display
    public int ComboCount { get; private set; }

    // Projectile spawning (future — stub for now)
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    private FighterControllerSimple _fighter;

    void Awake()
    {
        _fighter = GetComponent<FighterControllerSimple>();
    }

    // Called externally (from FighterControllerSimple or input layer) to fire events
    public void NotifyAttackStarted(MoveData move)
    {
        OnAttackStarted?.Invoke(move);
    }

    public void NotifySpecialActivated(MoveData move)
    {
        OnSpecialActivated?.Invoke(move);
    }

    public void NotifyCombo(int count)
    {
        ComboCount = count;
        OnComboUpdate?.Invoke(count);
    }

    public void NotifyDash(DashDirection dir) => OnDashStarted?.Invoke(dir);
    public void NotifyJump() => OnJumpStarted?.Invoke();
    public void NotifyGrabAttempted() => OnGrabAttempted?.Invoke();
    public void NotifyTaunt() => OnTaunt?.Invoke();

    // Projectile spawn — called by animation event
    public void SpawnProjectile()
    {
        if (projectilePrefab == null || projectileSpawnPoint == null) return;
        Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
    }
}