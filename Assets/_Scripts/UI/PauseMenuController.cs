// Assets/_Scripts/UI/PauseMenuController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public GameObject pausePanel;
    private bool _paused;

    void Update()
    {
        bool pauseKey = Keyboard.current?.escapeKey.wasPressedThisFrame == true
                     || Gamepad.current?.startButton.wasPressedThisFrame == true;
        if (pauseKey) TogglePause();
    }

    void TogglePause()
    {
        _paused = !_paused;
        Time.timeScale = _paused ? 0f : 1f;
        AudioManager.Instance?.SetMasterVolume(_paused ? 0.3f : 1f);
        pausePanel.SetActive(_paused);
    }

    public void Resume()
    {
        _paused = false; Time.timeScale = 1f;
        AudioManager.Instance?.SetMasterVolume(1f);
        pausePanel.SetActive(false);
    }

    public void QuitToMenu()
    { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); }
}