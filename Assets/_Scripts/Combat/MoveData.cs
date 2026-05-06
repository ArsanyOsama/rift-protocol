// MoveData.cs — ScriptableObject holding all data for one move
// Assets/_Scripts/Combat/MoveData.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMove", menuName = "RiftProtocol/MoveData")]
public class MoveData : ScriptableObject
{
    [Header("Identity")]
    public string moveName;
    public FighterStateType stateType;

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
}