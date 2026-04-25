using UnityEngine;

public class HitLogic : MonoBehaviour
{
    public GameObject hitSparkPrefab;
    public HitFlashController flashController;

     void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];

        // إظهار الشرارة
        Instantiate(hitSparkPrefab, contact.point, Quaternion.identity);

        // تشغيل الوميض
        if (flashController != null) flashController.StartFlash();
    }
}