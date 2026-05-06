using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitEffectsManager : MonoBehaviour
{
    public static HitEffectsManager Instance;

    [Header("Particles")]
    public ParticleSystem hitSparkLight;
    public ParticleSystem hitSparkHeavy;
    public ParticleSystem blockSpark;

    void Awake() { Instance = this; }

    public void SpawnHitSpark(Vector2 contactPoint, bool isBlocked)
    {
        var ps = isBlocked ? blockSpark : hitSparkLight;
        if (ps == null) return;
        ps.transform.position = contactPoint;
        ps.Play();
    }

    public void SpawnHeavySpark(Vector2 contactPoint)
    {
        if (hitSparkHeavy == null) return;
        hitSparkHeavy.transform.position = contactPoint;
        hitSparkHeavy.Play();
    }
}