using UnityEngine;

public class BreakablePart : MonoBehaviour
{
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Break(Vector3 explosionPos, float force)
    {
        rb.isKinematic = false;
        rb.AddExplosionForce(force, explosionPos, 10f);
    }
}
