// Assets/_Scripts/Stages/LightPulse.cs — attach to any pulsing light
using UnityEngine;

public class LightPulse : MonoBehaviour
{
    private Light _light;
    public float baseIntensity = 6f;
    public float amplitude = 2.5f;
    public float frequency = 0.8f;
    public Color baseColor = new Color(0.53f, 0.27f, 1f);
    public Color peakColor = new Color(0.53f, 0.07f, 0.9f);

    void Awake() => _light = GetComponent<Light>();

    void Update()
    {
        float t = (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) + 1f) * 0.5f;
        _light.intensity = Mathf.Lerp(baseIntensity - amplitude, baseIntensity + amplitude, t);
        _light.color = Color.Lerp(baseColor, peakColor, t);
    }
}