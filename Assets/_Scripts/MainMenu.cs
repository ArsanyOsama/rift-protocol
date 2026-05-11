using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void StartGame()
    {
        SceneTransition.Instance.FadeToScene("CharacterSelection"); // Selection 1 scene
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}