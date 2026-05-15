using UnityEngine;

public class BreakBuilding : MonoBehaviour
{
    public BreakablePart[] parts;
    public float debrisLifetime = 3f; // ← 破片が残る時間（秒）

    public void Break(Vector3 hitPos)
    {
        // 破片を飛ばす
        foreach (var p in parts)
        {
            p.Break(hitPos, 8f);

            // ★ 破片だけ遅れて消す
            Destroy(p.gameObject, debrisLifetime);
        }

        // ★ 建物本体はすぐ消す（当たり判定やAIのターゲットから除外）
        Destroy(gameObject);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Break(transform.position);
        }
    }
}
