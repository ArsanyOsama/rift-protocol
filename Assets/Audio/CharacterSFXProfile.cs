// Assets/_Scripts/Audio/CharacterSFXProfile.cs
using UnityEngine;

[CreateAssetMenu(fileName = "SFX_Char", menuName = "RiftProtocol/CharacterSFXProfile")]
public class CharacterSFXProfile : ScriptableObject
{
    [Header("Voice Lines")]
    public AudioClip introLine;
    public AudioClip[] tauntLines;
    public AudioClip koVoiceLine;           // when this fighter wins
    public AudioClip koReactionLine;        // when this fighter loses
    public AudioClip[] attackGrunts;          // effort on heavy attacks
    public AudioClip[] hitReactionGrunts;     // taking a hit

    [Header("Combat SFX")]
    public AudioClip[] lightSwingClips;
    public AudioClip[] mediumSwingClips;
    public AudioClip[] heavySwingClips;
    public AudioClip specialActivationClip;
    public AudioClip specialImpactClip;

    [Header("Movement SFX")]
    public AudioClip footstepClip;
    public AudioClip jumpClip;
    public AudioClip landClip;
    public AudioClip dashClip;
}