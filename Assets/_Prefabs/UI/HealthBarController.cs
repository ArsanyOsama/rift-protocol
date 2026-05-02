// HealthBarController.cs
using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    [SerializeField] private Slider _fillSlider;
    [SerializeField] private Slider _ghostSlider;
    [SerializeField] private Image _fillImage;
    [SerializeField] private GameObject _fighter;

    private FighterControllerSimple _fighter_ctrl;
    private float _ghostDelay = 0.3f;
    private float _ghostTimer;

    private Color _green = new Color(0.18f, 0.80f, 0.44f);
    private Color _yellow = new Color(0.95f, 0.61f, 0.07f);
    private Color _red = new Color(0.91f, 0.30f, 0.24f);

    void Start()
    {
        _fighter_ctrl = _fighter.GetComponent<FighterControllerSimple>();
        _fighter_ctrl.OnHPChanged += UpdateBar;
    }

    void UpdateBar(int current, int max)
    {
        float pct = (float)current / max;
        _fillSlider.value = pct;

        _fillImage.color = pct > 0.5f ? _green :
                           pct > 0.3f ? _yellow : _red;

        _ghostTimer = _ghostDelay;
    }

    void Update()
    {
        if (_ghostTimer > 0)
        {
            _ghostTimer -= Time.deltaTime;
            if (_ghostTimer <= 0)
                _ghostSlider.value = _fillSlider.value;
        }
    }
}