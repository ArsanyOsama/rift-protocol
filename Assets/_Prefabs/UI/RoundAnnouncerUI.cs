// RoundAnnouncerUI.cs
using System.Collections;
using TMPro;
using UnityEngine;

public class RoundAnnouncerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _roundText;
    [SerializeField] private TMP_Text _fightText;
    [SerializeField] private TMP_Text _koText;
    [SerializeField] private GameObject _overlayParent;

    // Call this from GameManager when round starts
    public IEnumerator ShowRoundStart(int roundNumber)
    {
        _roundText.text = $"ROUND {roundNumber}";
        _roundText.gameObject.SetActive(true);
        _fightText.gameObject.SetActive(false);
        _koText.gameObject.SetActive(false);
        _overlayParent.SetActive(true);

        // Slide in from above
        yield return AnimateSlideIn(_roundText.transform, 1.5f);

        _roundText.gameObject.SetActive(false);

        // FIGHT! punch in
        _fightText.gameObject.SetActive(true);
        yield return AnimatePunch(_fightText.transform, 0.3f);

        yield return new WaitForSeconds(0.5f);
        _overlayParent.SetActive(false);
    }

    public IEnumerator ShowKO()
    {
        _overlayParent.SetActive(true);
        _roundText.gameObject.SetActive(false);
        _fightText.gameObject.SetActive(false);
        _koText.gameObject.SetActive(true);

        yield return AnimatePunch(_koText.transform, 0.2f);
        yield return new WaitForSeconds(2.5f);
        _overlayParent.SetActive(false);
    }

    IEnumerator AnimateSlideIn(Transform t, float duration)
    {
        Vector3 startPos = t.localPosition + Vector3.up * 200f;
        Vector3 endPos = t.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tNorm = elapsed / duration;
            t.localPosition = Vector3.Lerp(startPos, endPos,
                              Mathf.SmoothStep(0f, 1f, tNorm));
            yield return null;
        }
        t.localPosition = endPos;
        yield return new WaitForSeconds(1.5f);
    }

    IEnumerator AnimatePunch(Transform t, float duration)
    {
        t.localScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tNorm = elapsed / duration;
            float scale = Mathf.LerpUnclamped(0f, 1f,
                          EaseOutBack(tNorm)); // overshoot then settle
            t.localScale = Vector3.one * scale;
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
    }
}