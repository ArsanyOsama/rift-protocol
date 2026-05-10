// Assets/_Scripts/GameFlow/IntroSequenceController.cs
using UnityEngine;
using Cinemachine;
using System.Collections;

public class IntroSequenceController : MonoBehaviour
{
    public CinemachineVirtualCamera vcamFight, vcamP1, vcamP2, vcamStageCrane;
    public Animator p1Animator, p2Animator;   // Set by GameManager
    public int roundNumber = 1;

    public IEnumerator PlayIntroSequence()
    {
        // 1. LOCK CONTROLLERS
        LockAllInputs(true);

        // 2. Stage crane intro (if applicable)
        var stageCtrl = FindObjectOfType<StageIntroController>();
        if (stageCtrl != null && vcamStageCrane != null)
            yield return stageCtrl.PlayStageCraneShot(vcamStageCrane);

        // 3. P1 Intro
        vcamP1.Priority = 15; vcamFight.Priority = 10;
        p1Animator?.SetTrigger("Intro");
        AudioManager.Instance?.PlayIntroVoice(0);
        yield return new WaitForSeconds(1.5f);

        // 4. P2 Intro
        vcamP1.Priority = 9; vcamP2.Priority = 15;
        p2Animator?.SetTrigger("Intro");
        AudioManager.Instance?.PlayIntroVoice(1);
        yield return new WaitForSeconds(1.5f);

        // 5. Back to Fight Cam
        vcamP2.Priority = 9; vcamFight.Priority = 10;
        yield return new WaitForSeconds(0.5f);

        // 6. COUNTDOWN
        HUDController.Instance?.ShowText("3", 0.7f, 1.2f);
        yield return new WaitForSeconds(1.0f);
        HUDController.Instance?.ShowText("2", 0.7f, 1.2f);
        yield return new WaitForSeconds(1.0f);
        HUDController.Instance?.ShowText("1", 0.7f, 1.2f);
        yield return new WaitForSeconds(1.0f);

        // 7. FIGHT
        HUDController.Instance?.ShowText("FIGHT!", 0.6f, 1.5f);
        AudioManager.Instance?.PlayRoundStart();
        yield return new WaitForSeconds(0.2f);

        // 8. UNLOCK CONTROLLERS
        LockAllInputs(false);
        HUDController.Instance?.StartTimer();
    }

    void LockAllInputs(bool locked)
    {
        foreach (var f in FindObjectsOfType<FighterControllerSimple>())
        {
            f.inputLocked = locked;
        }
    }
}