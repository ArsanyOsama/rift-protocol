using UnityEngine;

[CreateAssetMenu(fileName = "NewMove", menuName = "RiftProtocol/MoveData")]
public class MoveData : ScriptableObject
{
    [Header("Identity")]
    public string moveName;
    public FighterStateType stateType;

    // ADD — was referenced in FighterAnimationController but missing
    public AttackType attackType = AttackType.Punch;

    [Header("Frame Data")]
    public int startupFrames = 4;
    public int activeFrames = 3;
    public int recoveryFrames = 8;
    public int hitstunFrames = 12;
    public int blockstunFrames = 8;
    public int hitFreezeFrames = 2;

    [Header("Damage + Physics")]
    public int damage = 8;
    public float knockback = 1.5f;
    public bool causesKnockdown = false;
    public bool isLauncher = false;

    [Header("Box Data")]
    public HitType hitLevel = HitType.Mid;

    [Header("Cancel Windows")]
    public bool cancelableIntoSpecial = false;
    public int cancelWindowStart = 10;

    // ADD — audio/feel fields referenced in FighterAnimationController
    [Header("Feel")]
    public AudioClip startupSFX;       // sound on attack startup
    public AudioClip specialSFX;       // sound on special activation
    public float screenShakeForce = 0f;  // 0 = no shake, >0 = impulse force
}