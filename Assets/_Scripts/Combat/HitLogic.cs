using UnityEngine; // this code is for testing the hit spark effect and hit flash effect when a collision happens between two objects with rigidbodies. The object with the lower speed will trigger the effects, simulating that it got hit by the faster object so it's kinda out of the our base workflow but it's useful for testing the visual feedback of hits in our game. We can use this script on any object that has a Rigidbody and we want to see the hit effects when it collides with something faster than itself.

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