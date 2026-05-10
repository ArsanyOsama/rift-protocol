using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSelectManager : MonoBehaviour
{
    public static MapSelectManager Instance;

    [Header("Map Prefabs — assign all 3 in Inspector")]
    public GameObject[] mapPrefabs;
    public Sprite[] mapPreviews;

    public static int P1MapVote = -1;
    public static int P2MapVote = -1;
    public static int SelectedMap = 2;  // default: Dead Ruins
    public static bool IsSinglePlayer = true;

    // [FIX 5B] Removed _totalMaps = 3; We use mapPrefabs.Length dynamically!

    void Awake() { Instance = this; }

    public void P1VoteMap(int index) => P1MapVote = index;
    public void P2VoteMap(int index) => P2MapVote = index;

    public void ConfirmAndLoad()
    {
        if (IsSinglePlayer)
        {
            SelectedMap = P1MapVote >= 0 ? P1MapVote : Random.Range(0, mapPrefabs.Length);
        }
        else
        {
            if (P1MapVote == P2MapVote && P1MapVote >= 0)
                SelectedMap = P1MapVote;           // both agreed
            else if (P1MapVote >= 0 && P2MapVote >= 0)
                SelectedMap = Random.value > 0.5f ? P1MapVote : P2MapVote; // random from 2
            else
                SelectedMap = Random.Range(0, mapPrefabs.Length); // no votes = full random
        }
        SceneManager.LoadScene("LoadingScreen");
    }

    // Called from MainGame scene to instantiate the selected stage
    public static GameObject SpawnSelectedMap(Transform parent = null)
    {
        var prefab = Instance?.mapPrefabs[SelectedMap];
        if (prefab == null) return null;
        return Instantiate(prefab, parent);
    }
}