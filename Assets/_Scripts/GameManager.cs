using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public int selectedStageIndex;
    public string player1Character;
    public string player2Character;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}