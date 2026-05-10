// Assets/_Scripts/Core/StageManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance;

    public string[] stageSceneNames = { "Stage_RiftTemple", "Stage_FractureCage", "Stage_ApexHelipad" };
    public string[] stageDisplayNames = { "THE RIFT TEMPLE", "THE FRACTURE CAGE", "APEX TOWER HELIPAD" };
    public Sprite[] stageThumbnails;       // 3 sprites for MapSelect UI
    public bool[] stageHasCraneIntro = { true, false, false };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadStageAdditive(int index)
    {
        if (index < 0 || index >= stageSceneNames.Length) return;
        StartCoroutine(LoadAndNotify(index));
    }

    IEnumerator LoadAndNotify(int index)
    {
        var op = SceneManager.LoadSceneAsync(stageSceneNames[index], LoadSceneMode.Additive);
        yield return op;

        // Find spawn points NOW (they only exist after stage scene loads)
        var p1 = GameObject.FindWithTag("SpawnP1");
        var p2 = GameObject.FindWithTag("SpawnP2");
        Vector3 pos1 = p1 != null ? p1.transform.position : new Vector3(-3f, 0f, 0f);
        Vector3 pos2 = p2 != null ? p2.transform.position : new Vector3(3f, 0f, 0f);

        GameManager.Instance?.OnStageLoaded(pos1, pos2, index);
    }

    public bool CurrentStageHasCraneIntro(int index)
        => index >= 0 && index < stageHasCraneIntro.Length && stageHasCraneIntro[index];
}