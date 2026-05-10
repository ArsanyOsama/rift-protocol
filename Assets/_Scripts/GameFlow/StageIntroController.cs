// Assets/_Scripts/Stages/StageIntroController.cs
using UnityEngine;
using Cinemachine;
using System.Collections;

public class StageIntroController : MonoBehaviour
{
    public IEnumerator PlayStageCraneShot(CinemachineVirtualCamera craneCam)
    {
        var bootstrap = StageBootstrap.Instance;
        if (bootstrap?.craneStart == null) yield break;

        craneCam.Priority = 25;
        craneCam.transform.SetPositionAndRotation(
            bootstrap.craneStart.position, bootstrap.craneStart.rotation);

        float duration = 2.2f, t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float pct = Mathf.SmoothStep(0f, 1f, t / duration);
            craneCam.transform.position = Vector3.Lerp(
                bootstrap.craneStart.position, bootstrap.craneEnd.position, pct);
            craneCam.transform.rotation = Quaternion.Slerp(
                bootstrap.craneStart.rotation, bootstrap.craneEnd.rotation, pct);
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);
        craneCam.Priority = 0;
    }
}