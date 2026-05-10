// Assets/_Scripts/UI/ControlBindingButton.cs
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class ControlBindingButton : MonoBehaviour
{
    public string actionName;       // e.g. "Attack_Light"
    public int playerIndex;      // 0 or 1
    public TMP_Text bindingLabel;

    private InputActionRebindingExtensions.RebindingOperation _rebindOp;

    public void StartRebind()
    {
        PlayerInput input = null;
        foreach (var p in FindObjectsOfType<PlayerInput>())
            if (p.GetComponent<FighterControllerSimple>()?.playerIndex == playerIndex)
            { input = p; break; }
        if (input == null) return;

        var action = input.actions.FindAction(actionName);
        action?.Disable();
        bindingLabel.text = "Press any key...";

        _rebindOp = action?.PerformInteractiveRebinding()
            .OnComplete(_ => { action.Enable(); RefreshLabel(action); _rebindOp.Dispose(); })
            .Start();
    }

    void RefreshLabel(InputAction action)
    {
        if (action?.bindings.Count > 0)
            bindingLabel.text = InputControlPath.ToHumanReadableString(
                action.bindings[0].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
    }
}