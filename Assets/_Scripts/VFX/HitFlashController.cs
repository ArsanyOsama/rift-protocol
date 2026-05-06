// HitFlashController.cs
// Assets/_Scripts/VFX/HitFlashController.cs
// Flashes the character white on hit, then restores original color correctly.

using System.Collections;
using UnityEngine;

public class HitFlashController : MonoBehaviour
{
    [SerializeField] private Renderer characterRenderer;
    [Tooltip("Duration of white flash in frames (at 60fps)")]
    [SerializeField] private int flashFrames = 3;

    private MaterialPropertyBlock _propBlock;
    private Color _originalColor = Color.white; // safe default
    private bool _colorCached = false;

    void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    void Start()
    {
        // Cache the original color ONCE so we can restore it after flash
        if (characterRenderer != null && !_colorCached)
        {
            characterRenderer.GetPropertyBlock(_propBlock);
            // Try to read existing color — if none set, default white is correct
            _originalColor = _propBlock.GetColor("_BaseColor");
            _colorCached = true;
        }
    }

    /// <summary>Call this from FighterControllerSimple when a hit lands.</summary>
    public void StartFlash()
    {
        StopAllCoroutines(); // prevent overlapping flashes
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Flash white
        characterRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_BaseColor", Color.white);
        characterRenderer.SetPropertyBlock(_propBlock);

        // Wait N frames
        for (int i = 0; i < flashFrames; i++)
            yield return null;

        // Restore original color — NOT black
        characterRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_BaseColor", _originalColor);
        characterRenderer.SetPropertyBlock(_propBlock);
    }
}