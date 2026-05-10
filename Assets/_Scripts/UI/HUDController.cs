using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance;

    [Header("Health Bars")]
    public Image p1HealthFill, p1HealthDelay;
    public Image p2HealthFill, p2HealthDelay;

    [Header("Timer")]
    public TMP_Text timerText;
    private float _timerValue;
    private bool _timerRunning;

    [Header("Round Indicators")]
    public Image[] p1RoundDots;    // 2 or 3 depending on best-of
    public Image[] p2RoundDots;

    [Header("Combo")]
    public TMP_Text p1ComboNumber, p1ComboText;
    public TMP_Text p2ComboNumber, p2ComboText;
    public CanvasGroup p1ComboGroup, p2ComboGroup;

    [Header("Power Gauge")]
    public Image p1PowerFill, p2PowerFill;

    [Header("Fight Text")]
    public TMP_Text centerText;
    public CanvasGroup centerTextGroup;

    [Header("Colors")]
    public Color kaelHealthColor = new Color(0.13f, 0.67f, 1.0f);
    public Color siraHealthColor = new Color(1.0f, 0.53f, 0.0f);
    public Color lowHealthColor = new Color(1.0f, 0.2f, 0.1f);

    void Awake() => Instance = this;

    void Start()
    {
        _timerValue = GameFlowManager.RoundTimerSeconds;
        p1ComboGroup.alpha = 0f;
        p2ComboGroup.alpha = 0f;
        centerTextGroup.alpha = 0f;
    }

    void Update()
    {
        if (_timerRunning && _timerValue > 0f)
        {
            _timerValue -= Time.deltaTime;
            timerText.text = Mathf.CeilToInt(_timerValue).ToString("00");
            if (_timerValue <= 10f) timerText.color = Color.red;
        }
    }

    // Called by CharacterState.OnHPChanged
    public void UpdateHealth(int playerIndex, int current, int max)
    {
        float ratio = (float)current / max;
        Image fill = playerIndex == 0 ? p1HealthFill : p2HealthFill;
        Image delay = playerIndex == 0 ? p1HealthDelay : p2HealthDelay;

        fill.fillAmount = ratio;
        fill.color = ratio < 0.25f ? lowHealthColor :
                     (playerIndex == 0 ? kaelHealthColor : siraHealthColor);

        // Delay bar catches up slowly
        StopCoroutine(playerIndex == 0 ? "DrainDelayP1" : "DrainDelayP2");
        StartCoroutine(playerIndex == 0 ? DrainDelayBar(delay, ratio, "P1") : DrainDelayBar(delay, ratio, "P2"));
    }

    IEnumerator DrainDelayBar(Image bar, float targetFill, string id)
    {
        yield return new WaitForSeconds(0.4f);
        float start = bar.fillAmount;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            bar.fillAmount = Mathf.Lerp(start, targetFill, t / 0.5f);
            yield return null;
        }
        bar.fillAmount = targetFill;
    }

    // Combo display
    public void ShowCombo(int playerIndex, int count)
    {
        var group = playerIndex == 0 ? p1ComboGroup : p2ComboGroup;
        var number = playerIndex == 0 ? p1ComboNumber : p2ComboNumber;

        if (count < 2) { group.alpha = 0f; return; }

        number.text = count.ToString();
        group.alpha = 1f;
        StopCoroutine("PunchComboText" + playerIndex);
        StartCoroutine(PunchComboText(number.transform));
    }

    IEnumerator PunchComboText(Transform t)
    {
        t.localScale = Vector3.one * 1.5f;
        float elapsed = 0f;
        while (elapsed < 0.15f) { elapsed += Time.deltaTime; t.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, elapsed / 0.15f); yield return null; }
        t.localScale = Vector3.one;
    }

    // Power gauge (fills as combo counter goes up, triggers special trail at max)
    public void UpdatePower(int playerIndex, float normalizedValue)
    {
        var fill = playerIndex == 0 ? p1PowerFill : p2PowerFill;
        fill.fillAmount = Mathf.Clamp01(normalizedValue);
    }

    // Round management
    public void UpdateRoundDots(int playerIndex, int roundsWon)
    {
        var dots = playerIndex == 0 ? p1RoundDots : p2RoundDots;
        for (int i = 0; i < dots.Length; i++)
            dots[i].color = i < roundsWon ? Color.white : new Color(0.3f, 0.3f, 0.3f);
    }

    // Big fight text
    public void ShowText(string text, float duration = 1.5f, float scale = 1f)
    {
        StopCoroutine("AnimateFightText");
        StartCoroutine(AnimateFightText(text, duration, scale));
    }

    IEnumerator AnimateFightText(string text, float duration, float scale)
    {
        centerText.text = text;
        centerText.transform.localScale = Vector3.one * scale * 1.4f;
        centerTextGroup.alpha = 1f;

        float t = 0f;
        while (t < 0.15f) { t += Time.deltaTime; centerText.transform.localScale = Vector3.Lerp(Vector3.one * scale * 1.4f, Vector3.one * scale, t / 0.15f); yield return null; }
        yield return new WaitForSeconds(duration);

        t = 0f;
        while (t < 0.3f) { t += Time.deltaTime; centerTextGroup.alpha = 1f - (t / 0.3f); yield return null; }
        centerTextGroup.alpha = 0f;
    }

    public void StartTimer() => _timerRunning = true;
    public void StopTimer() => _timerRunning = false;
    public float GetTimerValue() => _timerValue;
}