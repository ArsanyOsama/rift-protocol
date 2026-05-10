using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Collections;

public class VideoIntroController : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public CanvasGroup blackOverlay;
    public VideoClip introClip;
    public VideoClip teaserClip;

    IEnumerator Start()
    {
        // Initial state: Start totally black
        blackOverlay.alpha = 1f;

        // --- PHASE 1: INTRO ---
        videoPlayer.clip = introClip;
        videoPlayer.Prepare();
        yield return new WaitUntil(() => videoPlayer.isPrepared);

        // Fade in from black
        yield return FadeOverlay(1f, 0f, 0.5f);
        videoPlayer.Play();

        // THE FIX: Wait until it ACTUALLY registers as playing first
        yield return new WaitUntil(() => videoPlayer.isPlaying);
        // THEN wait until it stops playing
        yield return new WaitUntil(() => !videoPlayer.isPlaying);

        // --- PHASE 2: TRANSITION ---
        yield return FadeOverlay(0f, 1f, 0.3f);
        yield return new WaitForSeconds(0.1f);

        // --- PHASE 3: TEASER ---
        videoPlayer.clip = teaserClip;
        videoPlayer.Prepare();
        yield return new WaitUntil(() => videoPlayer.isPrepared);

        yield return FadeOverlay(1f, 0f, 0.3f);
        videoPlayer.Play();

        // THE FIX: Bulletproof wait for the second video
        yield return new WaitUntil(() => videoPlayer.isPlaying);
        yield return new WaitUntil(() => !videoPlayer.isPlaying);

        // --- PHASE 4: EXIT ---
        yield return FadeOverlay(0f, 1f, 0.5f);
        SceneManager.LoadScene("MainMenu");
    }

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            blackOverlay.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        blackOverlay.alpha = to;
    }
}