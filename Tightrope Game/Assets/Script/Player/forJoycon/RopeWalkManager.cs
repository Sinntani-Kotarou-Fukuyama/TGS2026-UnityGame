using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RopeWalkManager : MonoBehaviour
{
    enum Route
    {
        None,
        Idle,
        Left,
        Right
    }

    [SerializeField] TrolleyWall trolleyWall;

    [Header("ルートTransform（開始・終了の2個1組）")]
    [Tooltip("例: CommonRope1Start, CommonRope1End")]
    [SerializeField] private Transform[] commonRoutePoints;

    [Tooltip("例: LeftRope2Start, LeftRope2End, LeftRope3Start, LeftRope3End")]
    [SerializeField] private Transform[] leftRoutePoints;

    [Tooltip("例: RightRope2Start, RightRope2End, RightRope3Start, RightRope3End")]
    [SerializeField] private Transform[] rightRoutePoints;

    private Transform CurrentStart, CurrentEnd;
    private Route currentRoute;
    private int CntRoute;

    private void Start()
    {
        SetRoute(commonRoutePoints[0], commonRoutePoints[1]);

        currentRoute = Route.None;
        CntRoute = 1;
    }

    private void Update()
    {

        float distance = Vector3.Distance(trolleyWall.transform.position, CurrentEnd.position);
        // 
        if (distance <= 0.1f)
        {
            switch (currentRoute)
            {
                case Route.None:

                    trolleyWall.IsStop(true);
                    //gamobjectSetactive どちらに進むかのUIを表示させ、UIの法にCrrentRouteを設定させる
                    //joycon用のゲージは非表示にする
                    currentRoute = Route.Idle; // LeftかRightが決まるまでの待機場所
                    break;
                case Route.Idle:
                    currentRoute = Route.Left;
                    trolleyWall.IsStop(false);
                    break;
                case Route.Left:

                    if(CntRoute > leftRoutePoints.Length)
                    {
                        SceneManager.LoadScene("ClearScene");
                    }

                    SetRoute(leftRoutePoints[CntRoute - 1], leftRoutePoints[CntRoute]);
                    CntRoute += 2;


                    break;
                case Route.Right:

                    if(CntRoute > rightRoutePoints.Length)
                    {
                        SceneManager.LoadScene("ClearScene");
                    }
                    SetRoute(rightRoutePoints[CntRoute - 1], rightRoutePoints[CntRoute]);
                    CntRoute += 2;
                    break;
            }

        }
    }

    public void SetRoute(Transform start, Transform end)
    {
        CurrentStart = start;
        CurrentEnd = end;

        trolleyWall.transform.SetPositionAndRotation(CurrentStart.position, GetLookAtRotation(CurrentStart, CurrentEnd));
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

    public void MovePlayer()
    {
        trolleyWall.IsStop(false);
    }
    public void StopPlayer()
    {
        trolleyWall.IsStop(true);
    }
    public bool IsPlayerStop()
    {
        return trolleyWall.IsStop();
    }

}
