using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    public CanvasGroup fadeOverlay;
    public AudioClip menuMusic;

    void Start()
    {
        AudioManager.Instance?.PlayMusic(menuMusic);
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 0.5f) { t += Time.deltaTime; fadeOverlay.alpha = 1f - (t / 0.5f); yield return null; }
        fadeOverlay.alpha = 0f;
    }

    public void OnSinglePlayer()
    {
        GameFlowManager.IsMultiplayer = false;
        GameFlowManager.ResetMatch();
        LoadWithFade("CharacterSelect");
    }

    public void OnMultiplayer()
    {
        GameFlowManager.IsMultiplayer = true;
        GameFlowManager.ResetMatch();
        LoadWithFade("CharacterSelect");
    }

    void LoadWithFade(string scene) => StartCoroutine(FadeAndLoad(scene));

    IEnumerator FadeAndLoad(string scene)
    {
        float t = 0f;
        while (t < 0.4f) { t += Time.deltaTime; fadeOverlay.alpha = t / 0.4f; yield return null; }
        SceneManager.LoadScene(scene);
    }
}