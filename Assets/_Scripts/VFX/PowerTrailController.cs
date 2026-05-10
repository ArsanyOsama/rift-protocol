// Assets/_Scripts/VFX/PowerTrailController.cs
using UnityEngine;

public class PowerTrailController : MonoBehaviour
{
    public TrailRenderer trail;
    public ParticleSystem auraParticles;
    public int comboThreshold = 5;

    void Start()
    {
        var cs = GetComponent<CharacterState>();
        if (cs) cs.OnComboUpdated += OnComboChanged;
        SetActive(false);
    }

    void OnComboChanged(int count) => SetActive(count >= comboThreshold);

    void SetActive(bool on)
    {
        trail.emitting = on;
        if (on && !auraParticles.isPlaying) auraParticles.Play();
        else if (!on) auraParticles.Stop();
    }
}