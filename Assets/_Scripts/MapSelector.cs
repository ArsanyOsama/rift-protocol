using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class MapSelector : MonoBehaviour
{
    public Image stageImage;
    public TMP_Text stageNameText;

    public Sprite[] maps;
    public string[] mapNames;

    private int currentIndex = 0;

    void Start()
    {
        UpdateMap();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
            NextMap();

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            PreviousMap();

        if (Input.GetKeyDown(KeyCode.Return))
            SelectMap();
    }

    public void NextMap()
    {
        currentIndex = (currentIndex + 1) % maps.Length;
        UpdateMap();
    }

    public void PreviousMap()
    {
        currentIndex--;

        if (currentIndex < 0)
            currentIndex = maps.Length - 1;

        UpdateMap();
    }

    void UpdateMap()
    {
        stageImage.sprite = maps[currentIndex];
        stageNameText.text = mapNames[currentIndex];
    }

    public void SelectMap()
    {
        Debug.Log("Selected Map: " + mapNames[currentIndex]);

        GameManager.Instance.selectedStageIndex = currentIndex;

        //SceneTransition.Instance.FadeToScene(3); // Fight scene index
    }
}