// KOSequenceController.cs
using UnityEngine;
using Cinemachine;
using System.Collections;

public class KOSequenceController : MonoBehaviour
{
    public CinemachineVirtualCamera vcamKO;

    public IEnumerator PlayKOSequence(int winnerIndex, Animator winnerAnim)
    {
        // Hit freeze — 0.05x timescale for 0.1s realtime
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(0.1f);

        // White screen flash
        ScreenFlashController.Instance?.KOFlash();
        Time.timeScale = 1.0f;
        yield return new WaitForSeconds(0.2f);

        // KO cam takes over + slow motion
        vcamKO.Priority = 20;
        Time.timeScale = 0.3f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        AudioManager.Instance?.PlayKOSequence(winnerIndex);

        yield return new WaitForSecondsRealtime(0.4f);
        HUDController.Instance?.ShowText("K.O.", 99f, 1.5f);
        yield return new WaitForSecondsRealtime(0.6f);

        // Restore
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        string name = GameFlowManager.GetCharName(winnerIndex == 0
            ? GameFlowManager.P1CharacterIndex
            : GameFlowManager.P2CharacterIndex);
        HUDController.Instance?.ShowText($"{name} WINS!", 99f, 1.2f);

        winnerAnim?.SetTrigger("Victory");
        yield return new WaitForSeconds(2.5f);

        vcamKO.Priority = 8;  // return priority to fight cam
    }
}