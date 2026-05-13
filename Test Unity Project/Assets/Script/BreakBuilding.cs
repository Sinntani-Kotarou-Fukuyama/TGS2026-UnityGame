using UnityEngine;

public class BreakBuilding : MonoBehaviour
{
    public BreakablePart[] parts;

    public void Break(Vector3 hitPos)
    {
        foreach (var p in parts)
        {
            p.Break(hitPos, 8f); // ”š•—‚Ì‹­‚³
        }
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Break(transform.position);
        }
    }

}
