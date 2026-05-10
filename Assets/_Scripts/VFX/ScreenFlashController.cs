// Assets/_Scripts/VFX/ScreenFlashController.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFlashController : MonoBehaviour
{
    public static ScreenFlashController Instance;
    [SerializeField] private Image _flashPanel;   // on Canvas_Overlay, starts alpha=0

    void Awake() => Instance = this;

    public void Flash(Color color, float duration = 0.08f)
    { StopAllCoroutines(); StartCoroutine(DoFlash(color, duration)); }

    public void KOFlash() => Flash(Color.white, 0.25f);

    IEnumerator DoFlash(Color c, float dur)
    {
        c.a = 1f; _flashPanel.color = c; _flashPanel.raycastTarget = false;
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; c.a = 1f - (t / dur); _flashPanel.color = c; yield return null; }
        c.a = 0f; _flashPanel.color = c;
    }
}