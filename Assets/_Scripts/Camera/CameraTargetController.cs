// Assets/_Scripts/Camera/CameraTargetController.cs
using UnityEngine;

public class CameraTargetController : MonoBehaviour
{
    public static CameraTargetController Instance;

    [Header("Set by GameManager after fighter spawn")]
    public Transform p1;
    public Transform p2;

    [Header("Clamp to stage bounds (match StageBootstrap)")]
    public float leftLimit = -8f;
    public float rightLimit = 8f;

    void Awake()
    {
        Instance = this;
    }

    void LateUpdate()  // LateUpdate so it runs AFTER physics + character movement
    {
        if (p1 == null || p2 == null) return;

        // Find the midpoint between the two fighters
        float midX = (p1.position.x + p2.position.x) * 0.5f;

        // Follow the higher fighter so the camera pans up during jumps
        float midY = Mathf.Max(p1.position.y, p2.position.y);

        // Clamp to stage — camera never shows outside walls
        midX = Mathf.Clamp(midX, leftLimit, rightLimit);

        // Apply the position (keeping the camera's original Z depth)
        transform.position = new Vector3(midX, midY + 0.3f, transform.position.z);
    }

    // Called by GameManager after spawn to inject fighter references
    public void SetFighters(Transform f1, Transform f2)
    {
        p1 = f1;
        p2 = f2;
    }

    // Called by StageBootstrap to update clamp bounds dynamically
    public void SetStageBounds(float left, float right)
    {
        // Offset inward so camera doesn't show stage edge at extreme positions
        leftLimit = left + 2f;
        rightLimit = right - 2f;
    }
}