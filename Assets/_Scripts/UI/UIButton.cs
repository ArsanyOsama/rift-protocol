// UIButton.cs — Consistent button behavior across all menus
// Assets/_Scripts/UI/UIButton.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _label;

    [SerializeField] private Color _normalBg = new Color(0.10f, 0.11f, 0.18f);
    [SerializeField] private Color _hoverBg = new Color(0.13f, 0.14f, 0.22f);
    [SerializeField] private Color _pressedBg = new Color(0.18f, 0.20f, 0.36f);
    [SerializeField] private Color _accentColor = new Color(0.29f, 0.62f, 1.0f);

    public void OnPointerEnter(PointerEventData e)
    {
        _background.color = _hoverBg;
        // Border accent highlight — handled via Outline component
    }

    public void OnPointerExit(PointerEventData e) => _background.color = _normalBg;

    public void OnPointerDown(PointerEventData e) => _background.color = _pressedBg;

    public void OnPointerUp(PointerEventData e) => _background.color = _hoverBg;
}