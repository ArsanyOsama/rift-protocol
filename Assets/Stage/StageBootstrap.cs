// Assets/_Scripts/Stages/StageBootstrap.cs
// Attach to a root GO in every stage scene.
// GameManager reads from it after fighters are spawned,
// then calls ApplyToFighters() to push stage values into PhysicsBody.

using UnityEngine;

public class StageBootstrap : MonoBehaviour
{
    public static StageBootstrap Instance { get; private set; }

    [Header("Fighter Spawn Points")]
    [Tooltip("Place these empty GOs directly ON the stage floor surface.")]
    public Transform spawnP1;    // e.g. (-3.5, 0, 0) for Temple | (-2.8, 0.05, 0) for Cage
    public Transform spawnP2;    // mirror of P1

    [Header("Cutscene Camera Anchors")]
    public Transform introCamP1;
    public Transform introCamP2;
    public Transform koCamPos;
    public Transform craneStart;   // null if stage has no crane intro
    public Transform craneEnd;

    [Header("Stage Bounds")]
    [Tooltip("Must match the physical walls in the scene.")]
    public float stageLeftWall = -7.5f;  // Cage: -6.5f
    public float stageRightWall = 7.5f;  // Cage:  6.5f

    [Tooltip("Y position of the floor. " +
             "Set this to match the Y of your floor GameObject. " +
             "Check it by selecting the floor in the scene and reading Transform.Position.Y. " +
             "Rift Temple = 0 | Cage = check your floor GO.")]
    public float groundY = 0f;

    void Awake() => Instance = this;

    // ─────────────────────────────────────────────────────────────────────────
    // Called by GameManager AFTER fighters are spawned into the scene.
    // Pushes groundY + wall bounds from THIS stage into every PhysicsBody.
    // This is what was missing — without this call, PhysicsBody keeps its
    // default groundY = 0 regardless of what the stage floor actually is.
    // ─────────────────────────────────────────────────────────────────────────
    public void ApplyToFighters()
    {
        PhysicsBody[] bodies = FindObjectsOfType<PhysicsBody>();

        if (bodies.Length == 0)
        {
            Debug.LogWarning("[StageBootstrap] ApplyToFighters called but no PhysicsBody found in scene. " +
                             "Make sure fighters are spawned before calling this.");
            return;
        }

        foreach (PhysicsBody body in bodies)
        {
            body.SetStageSettings(groundY, stageLeftWall, stageRightWall);

            Debug.Log($"[StageBootstrap] Applied to {body.gameObject.name}: " +
                      $"groundY={groundY} | walls={stageLeftWall} to {stageRightWall}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Also updates CameraTargetController bounds so the camera
    // never shows past the stage edges.
    // ─────────────────────────────────────────────────────────────────────────
    public void ApplyCameraBounds()
    {
        CameraTargetController.Instance?.SetStageBounds(stageLeftWall, stageRightWall);
    }
}