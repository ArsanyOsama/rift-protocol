// Assets/_Scripts/VFX/HitEffectsManager.cs
using UnityEngine;
using Cinemachine;

public class HitEffectsManager : MonoBehaviour
{
    public static HitEffectsManager Instance;

    [Header("Base VFX")]
    public ParticleSystem hitSparkLight, hitSparkMedium, hitSparkHeavy;
    public ParticleSystem blockSpark, counterHitBurst, groundShockwave;

    [Header("Kael VFX")]
    public ParticleSystem kaelFrostFist, kaelIceCrack, kaelFreezeBurst;

    [Header("Sira VFX")]
    public ParticleSystem siraBladeArc, siraHeatShimmer;

    [Header("Hemdan VFX")]
    public ParticleSystem hemdanConduitCharge, hemdanPlasmaCoreFlare;
    public ParticleSystem hemdanDataScan, hemdanRiftCrack;
    public GameObject hemdanSpecialProjectilePrefab;

    [Header("Ramez VFX")]
    public ParticleSystem ramezFloorDecals, ramezAxiomBurst, ramezSphereSlamWave;
    public GameObject ramezTrapZonePrefab;

    [Header("Camera")]
    public CinemachineImpulseSource impulseSource;

    void Awake()
    {
        Instance = this;
        if (CollisionManager.Instance)
            CollisionManager.Instance.OnHitConfirmed += OnHit;
    }

    void OnDestroy()
    {
        if (CollisionManager.Instance)
            CollisionManager.Instance.OnHitConfirmed -= OnHit;
    }

    void OnHit(HitEvent e)
    {
        Vector3 contact = e.contactPoint;
        bool blocked = e.result == HitResult.Blocked;

        if (blocked) { Spawn(blockSpark, contact); AudioManager.Instance?.OnHit(e); return; }

        if (e.damageDealt >= 18)
        {
            Spawn(hitSparkHeavy, contact);
            Spawn(groundShockwave, new Vector3(contact.x, 0f, 0f));
            impulseSource?.GenerateImpulse(new Vector3(0.15f, 0.08f, 0f));
        }
        else if (e.damageDealt >= 12)
        {
            Spawn(hitSparkMedium, contact);
        }
        else
        {
            Spawn(hitSparkLight, contact);
        }

        // [FIXED] Removed the Counter-hit check here to clear the error!

        // Character-specific VFX
        int atkChar = e.attackerIndex == 0 ? GameFlowManager.P1CharacterIndex : GameFlowManager.P2CharacterIndex;

        switch (atkChar)
        {
            case 0: // Kael
                Spawn(kaelFrostFist, contact);
                if (e.damageDealt >= 18) Spawn(kaelIceCrack, new Vector3(contact.x, 0f, 0f));
                break;
            case 1: // Sira
                Spawn(siraBladeArc, contact); break;
            case 2: // Hemdan
                Spawn(hemdanConduitCharge, contact);
                if (e.damageDealt >= 18)
                {
                    Spawn(hemdanRiftCrack, new Vector3(contact.x, 0f, 0f));
                    ScreenFlashController.Instance?.Flash(new Color(1f, 0.1f, 0.1f, 1f), 0.04f);
                }
                break;
            case 3: // Ramez
                if (e.damageDealt >= 12 && e.damageDealt < 18) Spawn(ramezAxiomBurst, contact);
                else if (e.damageDealt >= 18) Spawn(ramezSphereSlamWave, new Vector3(contact.x, 0f, 0f));
                break;
        }

        AudioManager.Instance?.OnHit(e);
    }

    public void SpawnSpecialActivationVFX(Vector3 position, int playerIndex)
    {
        int charIdx = playerIndex == 0
            ? GameFlowManager.P1CharacterIndex
            : GameFlowManager.P2CharacterIndex;

        Color c = charIdx switch
        {
            0 => new Color(0.13f, 0.67f, 1f),
            1 => new Color(1f, 0.53f, 0f),
            2 => new Color(1f, 0.13f, 0.2f),
            3 => new Color(0.27f, 0.6f, 1f),
            _ => Color.white
        };
        ScreenFlashController.Instance?.Flash(c, 0.1f);
        impulseSource?.GenerateImpulse(new Vector3(0f, -0.3f, 0f));

        switch (charIdx)
        {
            case 2: Spawn(hemdanPlasmaCoreFlare, position); break;
            case 3: Spawn(ramezAxiomBurst, position); break;
        }
    }

    void Spawn(ParticleSystem ps, Vector3 worldPos)
    {
        if (ps == null) return;
        ps.transform.position = worldPos;
        ps.Play();
    }
}