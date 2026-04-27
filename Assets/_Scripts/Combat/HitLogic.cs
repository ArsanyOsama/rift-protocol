using UnityEngine;

public class HitLogic : MonoBehaviour
{
    public GameObject hitSparkPrefab;
    public HitFlashController flashController;

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody myRb = GetComponent<Rigidbody>();
        Rigidbody otherRb = collision.gameObject.GetComponent<Rigidbody>();

        if (otherRb != null)
        {
            // بنحسب سرعة كل واحدة فيهم وقت اللحظة دي
            float mySpeed = myRb.velocity.magnitude;
            float otherSpeed = otherRb.velocity.magnitude;

            // لو سرعة اللي خبطني (otherSpeed) أكبر من سرعتي (mySpeed)
            // ده معناه إنه هو اللي هاجمني، فـ أنا اللي هطلع الإيفيكت
            if (otherSpeed > mySpeed)
            {
                if (hitSparkPrefab != null)
                {
                    ContactPoint contact = collision.contacts[0];
                    Instantiate(hitSparkPrefab, contact.point, Quaternion.identity);
                }

                if (flashController != null)
                {
                    flashController.StartFlash();
                }

                Debug.Log(gameObject.name + " was hit by " + collision.gameObject.name);
            }
        }
    }
}
