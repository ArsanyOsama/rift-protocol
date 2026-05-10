using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Character Data")]
    public CharacterData[] characters; // ScriptableObjects with name, art, prefab

    [Header("P1 UI")]
    public Image p1CharDisplay;
    public TMP_Text p1CharName;

    [Header("P2 UI")]
    public Image p2CharDisplay;
    public TMP_Text p2CharName;
    public GameObject p2Column; // hide in single player

    [Header("Confirm")]
    public Button confirmButton;

    private int _p1Selection = -1;
    private int _p2Selection = -1;

    void Start()
    {
        p2Column.SetActive(GameFlowManager.IsMultiplayer);
        confirmButton.interactable = false;
    }

    public void SelectCharacter(int charIndex, int playerIndex)
    {
        // Mutual lock-out — if other player already chose this, ignore
        if (playerIndex == 0)
        {
            if (GameFlowManager.IsMultiplayer && _p2Selection == charIndex) return;
            _p1Selection = charIndex;
            p1CharDisplay.sprite = characters[charIndex].portrait;
            p1CharName.text = characters[charIndex].displayName;
            GameFlowManager.P1CharacterIndex = charIndex;
        }
        else
        {
            if (_p1Selection == charIndex) return;
            _p2Selection = charIndex;
            p2CharDisplay.sprite = characters[charIndex].portrait;
            p2CharName.text = characters[charIndex].displayName;
            GameFlowManager.P2CharacterIndex = charIndex;
        }

        CheckConfirmReady();
    }

    void CheckConfirmReady()
    {
        bool ready = _p1Selection >= 0 &&
                     (!GameFlowManager.IsMultiplayer || _p2Selection >= 0);
        confirmButton.interactable = ready;
    }

    public void OnConfirm()
    {
        // If single player, auto-set P2 from AI selection
        SceneManager.LoadScene("MapSelect");
    }
}

// ScriptableObject for character metadata
[CreateAssetMenu(fileName = "CharacterData", menuName = "RiftProtocol/CharacterData")]
public class CharacterData : ScriptableObject
{
    public string displayName;
    public string loreBlurb;
    public Sprite portrait;
    public Sprite fullBodyArt;
    public GameObject fighterPrefab;
}