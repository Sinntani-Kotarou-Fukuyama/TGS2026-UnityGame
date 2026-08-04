using UnityEngine;

public class RopeWalkManager : MonoBehaviour
{
    [SerializeField] GameObject trolleyWall;

    [Header("ルートTransform（開始・終了の2個1組）")]
    [Tooltip("例: CommonRope1Start, CommonRope1End")]
    [SerializeField] private Transform[] commonRoutePoints;

    [Tooltip("例: LeftRope2Start, LeftRope2End, LeftRope3Start, LeftRope3End")]
    [SerializeField] private Transform[] leftRoutePoints;

    [Tooltip("例: RightRope2Start, RightRope2End, RightRope3Start, RightRope3End")]
    [SerializeField] private Transform[] rightRoutePoints;

    private Transform CurrentStart, CurrentEnd;


    private void Start()
    {
        CurrentStart = commonRoutePoints[0];
        CurrentEnd = commonRoutePoints[1];
        trolleyWall.transform.SetPositionAndRotation(CurrentStart.position, GetLookAtRotation(CurrentStart, CurrentEnd));

    }

    private void Update()
    {

        float distance = Vector3.Distance(trolleyWall.transform.position, CurrentEnd.position);
        // 
        if (distance <= 1.0f)
        {
           // Quaternion quaternion = GetLookAtRotation(start2.position, end2.position); // まっすぐ向くためのQuaternionを作る
            //trolleyWall.transform.SetPositionAndRotation(start2.position, quaternion); // 移動と回転をする

        }
    }

    //第一、第二引数から向きを計算し、その方向に真っ直ぐ向くための回転データ（Quaternion）を作り出す
    public Quaternion GetLookAtRotation(Transform currentPosition, Transform targetPosition)
    {
        Vector3 direction = targetPosition.position - currentPosition.position;

        // 完全に同じ座標でなければ、向き（Quaternion）を計算して返す
        if (direction != Vector3.zero)
        {
            return Quaternion.LookRotation(direction);
        }

        // 向きが計算できない場合は、現在のオブジェクトの回転をそのまま返す
        return transform.rotation;
    }
}
