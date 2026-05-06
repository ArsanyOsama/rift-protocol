// CharacterSelectManager.cs
// Assets/_Scripts/GameFlow/CharacterSelectManager.cs

using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Available characters — order must match GameManager.characterPrefabs")]
    public string[] characterNames = { "KAEL", "SIRA", "HEMDAN", "RAMEZ" };

    // Static so they survive the scene load
    public static int P1Selection = 0;
    public static int P2Selection = 1;

    [Header("UI Display")]
    public TMP_Text p1NameDisplay;
    public TMP_Text p2NameDisplay;

    [Header("Single player mode toggle")]
    public bool isSinglePlayer = false;

    // ── Called by UI buttons ──────────────────────────────────────────────────
    public void P1SelectCharacter(int index)
    {
        P1Selection = Mathf.Clamp(index, 0, characterNames.Length - 1);
        if (p1NameDisplay != null)
            p1NameDisplay.text = characterNames[P1Selection];
    }

    public void P2SelectCharacter(int index)
    {
        P2Selection = Mathf.Clamp(index, 0, characterNames.Length - 1);
        if (p2NameDisplay != null)
            p2NameDisplay.text = characterNames[P2Selection];
    }

    public void StartFight()
    {
        // Store single player preference on GameManager after load
        PlayerPrefs.SetInt("SinglePlayer", isSinglePlayer ? 1 : 0);
        SceneManager.LoadScene("MainGame");
    }

    public void ToggleSinglePlayer(bool value)
    {
        isSinglePlayer = value;
    }
}