using UnityEngine;

public class CollisionReporter : MonoBehaviour
{
    public SlitherManager slitherManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (slitherManager != null)
            slitherManager.OnHeadTriggerEnter(other);
    }
}
