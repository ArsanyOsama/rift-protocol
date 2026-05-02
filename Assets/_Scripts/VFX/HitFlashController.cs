using System.Collections;
using UnityEngine;

public class HitFlashController : MonoBehaviour
{
    [SerializeField] private Renderer characterRenderer;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    public void StartFlash()
    {
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        characterRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", Color.white);
        characterRenderer.SetPropertyBlock(propBlock);

        for (int i = 0; i < 3; i++) yield return null;

        characterRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", Color.black);
        characterRenderer.SetPropertyBlock(propBlock);
    }
}