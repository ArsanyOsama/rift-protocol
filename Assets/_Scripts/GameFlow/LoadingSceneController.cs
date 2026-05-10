using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingSceneController : MonoBehaviour
{
    public UnityEngine.UI.Slider loadingBar;

    IEnumerator Start()
    {
        var op = SceneManager.LoadSceneAsync("FightScene");
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            loadingBar.value = op.progress;
            yield return null;
        }

        // Artificial hold so player can see the isometric preview
        float t = 0f;
        while (t < 1.2f) { t += Time.deltaTime; loadingBar.value = 0.9f + (t / 1.2f) * 0.1f; yield return null; }

        op.allowSceneActivation = true;
    }
}