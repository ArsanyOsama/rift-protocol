using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance;

    public Image fadeImage;
    public float fadeDuration = 0.5f;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        // Fade to black
        fadeImage.DOFade(1f, fadeDuration);
        yield return new WaitForSeconds(fadeDuration);

        SceneManager.LoadScene(sceneName);

        yield return null;

        // Fade back in
        fadeImage.DOFade(0f, fadeDuration);
    }
}