using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TrolleyWall : MonoBehaviour
{
    enum Type
    {
        Joycon,
        X,
        Y,
        Keyboard
    }


    [Header("Control Mode")]
    [SerializeField] private Type type;

    [Header("Movement")]
    [Tooltip("Playerをロープ上で自動前進させます。")]
    [SerializeField] private bool autoWalk = false;

    [Tooltip("ロープ上を1秒間に自動で進む距離です。")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;

    [Header("Balance Input")]
    [Tooltip("Joy-Con以外で使用する棒の最大入力角度（度）です。落下判定のMin / Max Angleとは別の値です。")]
    [SerializeField, Min(0f)] private float BarMaxAngle = 90f;

    [Tooltip("ONの時だけJoy-Conの取得を試します。取得できない場合はキーボードとマウスへ戻ります。")]
    [SerializeField] private bool useJoyConInput = false;

    [Tooltip("Joy-Conの相対角度へ掛ける感度倍率です。キーボードとマウスには影響しません。")]
    [SerializeField, Min(0f)] private float joyConSensitivity = 0.5f;

    [Tooltip("Joy-ConのIMUが有効になってからニュートラル安定判定を始めるまでの最低待機時間です。")]
    [SerializeField, Min(0f)] private float joyConNeutralMinimumWait = 1f;

    [Tooltip("Joy-ConのQuaternionが許容角度内に収まり続ける必要がある時間です。")]
    [SerializeField, Min(0f)] private float joyConNeutralStableDuration = 0.5f;

    [Tooltip("ニュートラル安定判定中に許容する基準姿勢からの角度差です。")]
    [SerializeField, Min(0f)] private float joyConNeutralAngleTolerance = 1.5f;

    [Tooltip("左右矢印キーで棒の入力角度を1秒間に変化させる量（度/秒）です。左右で同じ絶対値を使用します。")]
    [SerializeField, Min(0f)] private float keyboardBalanceStrength = 30f;

    [Tooltip("画面中央からのマウス横位置を棒の入力角度へ変換するときの倍率です。")]
    [SerializeField, Min(0f)] private float mouseBalanceSensitivity = 1f;

    [Tooltip("マウスが画面中央付近にある時、入力を0として扱う範囲です。")]
    [SerializeField, Range(0f, 1f)] private float mouseDeadZone = 0.05f;

    [Tooltip("マウス横移動を入力として認識する最小量です。")]
    [SerializeField, Min(0f)] private float mouseMoveThreshold = 1f;

    [Tooltip("入力がない時、棒の入力角度を0度へ戻す速さ（度/秒）です。")]
    [SerializeField, Min(0f)] private float returnToCenterSpeed = 20f;

    private Vector3 mousePos;  // マウスの座標を入れる


    private Rigidbody WallRb; // 壁のRigitBody
    private ConfigurableJoint trolleyjoin;　// trollyのconfigurableJoin
    [SerializeField] private GameObject trolley;
    [SerializeField] private GameObject Player;
    //[SerializeField] private GameObject Pole;



    [Header("参照オブジェクト")]
    [Tooltip("Joy-Conと同期して直接回転させる棒のTransform")]
    [SerializeField] private Transform balanceBarTransform;

    [Tooltip("棒の傾きによって、プレイヤーの姿勢が崩れる（加速する）強さ")]
    [SerializeField] private float barInfluenceStrength = 2.0f;
    private float latestBarRoll = 0f; // Updateで取得した最新の棒の傾きを保存する変数
    private Quaternion balanceBarBaseLocalRotation;
    private bool hasCapturedBalanceBarBaseLocalRotation;


    // JoyconLibのクラスを保持する変数
    private List<Joycon> joycons;
    private Joycon myJoycon;
    private bool joyConInitializationAttempted;
    private float joyConImuReadyTime;
    private Quaternion joyConStabilityReference;
    private float joyConStableElapsed;
    private bool hasJoyConStabilityReference;
    private bool joyConMinimumWaitStarted;
    private bool neutralCaptured;
    private Quaternion neutralJoyConRotation;
    private const float JoyConProjectedVectorMinSqrMagnitude = 0.000001f;

    [Header("Connected Object (接続先の土台)")]
    private Rigidbody connectedRigidbody; // 今上に乗っているロープのオブジェクトをセット


    //=================================================================================
    // 疑似的なHingeJoinの設定
    //=================================================================================
    [Header("Hinge Settings")]
    [SerializeField] private Vector3 localAnchor; // 動くオブジェクトから見た回転軸の位置
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // 回転軸（通常はY軸: 0, 1, 0）

    [Header("Center of Mass (重心)")]
    [SerializeField] private Vector3 localCenterOfMass = new Vector3(0, 0, 0); // プレイヤーのローカル重心位置

    [Header("Fall Limits")]
    [SerializeField] private bool useLimits = true;

    [Tooltip("Player角度がこの値を下回ると落下を開始します（度）。")]
    [SerializeField] private float minAngle = -90f;

    [Tooltip("Player角度がこの値を上回ると落下を開始します（度）。")]
    [SerializeField] private float maxAngle = 90f;

    [Header("Fall")]
    [Tooltip("ONでは限界超過をRopeWalkManagerへ1回だけ通知します。OFFでは従来の物理落下を使用します。")]
    [SerializeField] private bool useManagedFallFlow = true;

    [SerializeField] private float bounciness = 0.2f; // 壁に衝突した時の跳ね返り強度

    [Header("Balance Physics")]
    [Tooltip("角速度へ毎FixedUpdate適用する減衰の強さです。大きいほど傾きの変化が早く収まります。")]
    [SerializeField, Min(0f)] private float angularDrag = 1.0f; // 摩擦・空気抵抗（大きいほど早く止まる）

    [Tooltip("旧設定として保持しています。現行の独自重力計算では直接参照していません。")]
    [SerializeField] private bool applyGravity = true; // 傾いたときに重力で戻るか

    [Tooltip("傾きに応じて倒れる力を加える既存処理を使用します。")]
    [SerializeField] private bool applyFallForce = true;

    [Tooltip("現在角度から傾く方向へ加えるトルクの倍率です。大きいほど傾きの角加速度が増えます。")]
    [SerializeField, Min(0f)] private float fallSpeedScale = 50.0f; // この数値を大きくするほど、傾いた方向へ強く・速く倒れていく

    [Tooltip("棒の入力角度1度あたり、Playerの角速度へ加える補正の倍率です。")]
    [SerializeField, Min(0f)] private float postureControlStrength = 15.0f; // 【この値を大きくすると、棒の傾きに体が素早く追従する

    [Tooltip("Playerからロープパーツへ伝える反作用の倍率です。")]
    [SerializeField, Min(0f)] private float reactionForceScale = 1.0f;

    [Tooltip("前進中に発生する自然な揺れのトルク強度です。")]
    [SerializeField, Min(0f)] private float wobbleIntensity = 0.5f; // 【追加】前進時に発生する「ブレ」の強さ

    [Tooltip("自然な揺れのSin波へ掛ける時間係数（ラジアン/秒）です。大きいほど揺れが速くなります。")]
    [SerializeField, Min(0f)] private float naturalSwaySpeed = 1f;

    [Header("Debug")]
    [Tooltip("調査用ログを表示する場合だけONにします。通常はOFFにしてください。")]
    [SerializeField] private bool enableDebugLog = false;

    private Rigidbody PlayerRb; // プレイヤーのRigidBody
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalAnchorOffset;

    private float currentAngle = 0f;
    private float angularVelocity = 0f; // 角速度 (度/秒)
    private bool isInitialized = false;
    private bool isStop = false; //カメラが離れて見えづらい時などに一時的に操作、回転を止める用
    public bool balanceOnlyMode = false;
    private bool waitForBalanceInputRelease;
    private bool isPausedForExternalEvent;
    private bool isManagedFallActive;
    private bool hasNotifiedManagedFall;

    private Rigidbody trolleyRigidbody;
    private RigidbodyStateSnapshot managedFallWallRigidbodyState;
    private RigidbodyStateSnapshot managedFallPlayerRigidbodyState;
    private RigidbodyStateSnapshot managedFallTrolleyRigidbodyState;
    private Rigidbody managedFallConnectedRigidbody;
    private Rigidbody managedFallJointConnectedBody;
    private bool hasCapturedManagedFallPhysicsState;

    public event Action<float> ManagedFallStarted;
    public bool UsesManagedFallFlow => useManagedFallFlow;
    public bool IsAutoWalkEnabled => autoWalk;

    private struct RigidbodyStateSnapshot
    {
        public bool IsValid;
        public bool UseGravity;
        public bool IsKinematic;
        public RigidbodyConstraints Constraints;

        public static RigidbodyStateSnapshot Capture(Rigidbody rigidbody)
        {
            return rigidbody == null
                ? default
                : new RigidbodyStateSnapshot
                {
                    IsValid = true,
                    UseGravity = rigidbody.useGravity,
                    IsKinematic = rigidbody.isKinematic,
                    Constraints = rigidbody.constraints
                };
        }

        public void Restore(Rigidbody rigidbody)
        {
            if (!IsValid || rigidbody == null)
            {
                return;
            }

            rigidbody.useGravity = UseGravity;
            rigidbody.isKinematic = IsKinematic;
            rigidbody.constraints = Constraints;
            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    //[Header("揺れを伝えるオブジェクト")]
    //[SerializeField] private GameObject ShakingObject;



    // ロープの接続に関する判定をするフラグ
    private bool isTouchingRope = true;   // ロープの上に乗っているか？
    private bool EndMove = false;         // ロープから離れるときに一度だけ使うフラグ(プレイヤーを移動させるため1)
    private bool hasLoggedMissingRopeRigidbody;



    private void Start()
    {
        if (!TryInitializeRequiredReferences())
        {
            enabled = false;
            return;
        }

        if (useLimits && maxAngle <= minAngle)
        {
            Debug.LogWarning("TrolleyWall: Max AngleはMin Angleより大きい値にしてください。設定を直すまで落下角度判定を行いません。", this);
        }

        // 物理の自動衝突検知は残しつつ、自動の移動・回転をカット（スクリプトで制御するため）
        PlayerRb.isKinematic = false;
        PlayerRb.useGravity = false;
        PlayerRb.constraints = RigidbodyConstraints.FreezeAll;

        // Rigidbody自体に自作の重心位置を覚えさせる（物理的な回転の慣性に影響する）
        PlayerRb.centerOfMass = localCenterOfMass;

        // 回転軸
        rotationAxis = rotationAxis.normalized;

        // Use Joy Con InputがOFFなら、JoyconManagerには一切アクセスしない
        if (useJoyConInput)
        {
            TryInitializeJoyConInput();
        }
    }

    // 共通ロープ開始時に、Trolley一式とPlayerを同じ場所へ揃えて初期化します。
    public bool InitializeForCommonRope(Transform startPoint, Transform endPoint)
    {
        return InitializeForRouteSegment(startPoint, endPoint, startPoint != null ? startPoint.position : Vector3.zero);
    }

    // 分岐後の区間を準備します。近い開始点なら現在位置を維持し、離れている場合だけ一式を開始点へ揃えます。
    public bool PrepareForRouteSegment(Transform startPoint, Transform endPoint, float keepCurrentPositionDistance)
    {
        if (startPoint == null)
        {
            Debug.LogWarning("TrolleyWall: 選択ルートの開始点が未設定です。", this);
            return false;
        }

        float allowedDistance = Mathf.Max(0f, keepCurrentPositionDistance);
        Vector3 targetPosition = Vector3.Distance(transform.position, startPoint.position) <= allowedDistance
            ? transform.position
            : startPoint.position;
        return InitializeForRouteSegment(startPoint, endPoint, targetPosition);
    }

    private bool InitializeForRouteSegment(Transform startPoint, Transform endPoint, Vector3 targetPosition)
    {
        if (startPoint == null || endPoint == null)
        {
            Debug.LogWarning("TrolleyWall: ロープの開始点または終点が未設定です。", this);
            return false;
        }

        if (!TryInitializeRequiredReferences())
        {
            return false;
        }

        Vector3 direction = endPoint.position - startPoint.position;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            Debug.LogWarning("TrolleyWall: ロープの開始点と終点が同じ位置です。", this);
            return false;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        PlaceTrolleySystem(targetPosition, targetRotation);

        currentAngle = 0f;
        angularVelocity = 0f;
        latestBarRoll = 0f;
        waitForBalanceInputRelease = false;
        isManagedFallActive = false;
        hasNotifiedManagedFall = false;
        hasCapturedManagedFallPhysicsState = false;
        isInitialized = false;
        isTouchingRope = true;
        EndMove = false;
        hasLoggedMissingRopeRigidbody = false;
        connectedRigidbody = null;
        trolleyjoin.connectedBody = null;

        PlayerRb.isKinematic = false;
        PlayerRb.useGravity = false;
        PlayerRb.constraints = RigidbodyConstraints.FreezeAll;
        PlayerRb.centerOfMass = localCenterOfMass;
        rotationAxis = rotationAxis.normalized;

        if (balanceBarTransform != null)
        {
            ApplyBalanceBarRotation(0f);
        }

        if (trolleyRigidbody != null)
        {
            trolleyRigidbody.linearVelocity = Vector3.zero;
            trolleyRigidbody.angularVelocity = Vector3.zero;
        }

        StopRigidBodyMotion();
        isStop = true;
        enabled = true;
        return true;
    }

    // Segment開始時と落下復帰時で同じ一式配置を使用し、Playerだけが分離しないようにします。
    private void PlaceTrolleySystem(Vector3 targetPosition, Quaternion targetRotation)
    {
        Transform systemRoot = transform.parent;
        if (systemRoot != null)
        {
            Quaternion rootRotation = targetRotation * Quaternion.Inverse(transform.localRotation);
            systemRoot.rotation = rootRotation;
            Vector3 wallOffsetFromRoot = systemRoot.TransformVector(transform.localPosition);
            systemRoot.position = targetPosition - wallOffsetFromRoot;
            trolley.transform.SetParent(systemRoot, true);
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
        trolley.transform.SetPositionAndRotation(targetPosition, targetRotation);

        Quaternion uprightPlayerRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        Player.transform.SetPositionAndRotation(targetPosition - Vector3.up * 0.09f, uprightPlayerRotation);
    }

    // 保存したSegment進行率から、Root・Trolley・Playerを同じロープ位置へ一式で戻します。
    public bool RestoreManagedFallAtRouteProgress(Transform startPoint, Transform endPoint, float progress)
    {
        if (!useManagedFallFlow || !isManagedFallActive)
        {
            Debug.LogWarning("TrolleyWall: 管理落下中ではないため、落下位置への復帰を行いません。", this);
            return false;
        }

        if (startPoint == null || endPoint == null || !TryInitializeRequiredReferences())
        {
            Debug.LogWarning("TrolleyWall: 復帰先Segmentまたは必要参照が不足しているため、停止を維持します。", this);
            return false;
        }

        Vector3 direction = endPoint.position - startPoint.position;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            Debug.LogWarning("TrolleyWall: 復帰先Segmentの長さが0のため、停止を維持します。", this);
            return false;
        }

        Vector3 targetPosition = Vector3.Lerp(startPoint.position, endPoint.position, Mathf.Clamp01(progress));
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        StopRigidBodyMotion();
        PlaceTrolleySystem(targetPosition, targetRotation);

        currentAngle = 0f;
        angularVelocity = 0f;
        latestBarRoll = 0f;
        waitForBalanceInputRelease = true;
        isTouchingRope = true;
        EndMove = false;
        hasLoggedMissingRopeRigidbody = false;

        if (balanceBarTransform != null)
        {
            ApplyBalanceBarRotation(0f);
        }

        RestoreManagedFallPhysicsState();
        connectedRigidbody = managedFallConnectedRigidbody;
        trolleyjoin.connectedBody = managedFallJointConnectedBody != null
            ? managedFallJointConnectedBody
            : connectedRigidbody;

        if (connectedRigidbody != null)
        {
            initialLocalRotation = Quaternion.Inverse(connectedRigidbody.rotation) * Player.transform.rotation;
            Vector3 worldAnchor = Player.transform.TransformPoint(localAnchor);
            initialLocalAnchorOffset = connectedRigidbody.transform.InverseTransformPoint(worldAnchor);
            isInitialized = true;
        }
        else
        {
            // 接触中のRopePartsが取得できなかった場合は、保護終了後のOnTriggerStayで再接続します。
            isInitialized = false;
        }

        isStop = true;
        StopRigidBodyMotion();
        return true;
    }

    // 復帰保護が終わった時だけ管理落下ガードを解除します。RouteやSegmentは初期化しません。
    public void CompleteManagedFallRecovery()
    {
        if (!isManagedFallActive)
        {
            return;
        }

        currentAngle = 0f;
        angularVelocity = 0f;
        latestBarRoll = 0f;
        isTouchingRope = true;
        EndMove = false;
        hasLoggedMissingRopeRigidbody = false;
        waitForBalanceInputRelease = true;
        isManagedFallActive = false;
        hasNotifiedManagedFall = false;
        hasCapturedManagedFallPhysicsState = false;
        isStop = true;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            mousePos = mouse.position.ReadValue();
        }

        if (balanceBarTransform != null)
        {
            ApplyBalanceBarRotation(0f);
        }

        StopRigidBodyMotion();
        //復帰後に疑似HingeJointを再初期化する
        if (connectedRigidbody != null)
        {
            initialLocalRotation = Quaternion.Inverse(connectedRigidbody.rotation) * Player.transform.rotation;

            Vector3 worldAnchor = Player.transform.TransformPoint(localAnchor);
            initialLocalAnchorOffset = connectedRigidbody.transform.InverseTransformPoint(worldAnchor);

            isInitialized = true;
        }
        else
        {
            // RopeParts がまだ取れない場合は次の OnTriggerStay で再接続
            isInitialized = false;
        }

        if (balanceOnlyMode)
        {
            isStop = false;
            isManagedFallActive = false;
            waitForBalanceInputRelease = false;
            isInitialized = true;
        }
    }

    // 分岐待機・区間終端・Goal停止で共通して、現在の物理速度を確実に止めます。
    public void StopRouteMovement()
    {
        isStop = true;
        angularVelocity = 0f;
        StopRigidBodyMotion();
    }
    

    // 旧APIは既存参照との互換用に残します。
    public void StopAtCommonRopeEnd()
    {
        StopRouteMovement();
    }

    // 分岐決定に使った矢印キーが離れるまで、棒の入力だけを受け付けません。
    public void ResumeRouteMovement(bool waitForInputRelease)
    {
        latestBarRoll = 0f;
        waitForBalanceInputRelease = waitForInputRelease;
        isStop = isPausedForExternalEvent || isManagedFallActive;
    }

    // イベント側からTransformを書き換えている間は、Trolley側の全更新を止めます。
    public void PauseForExternalEvent()
    {
        isPausedForExternalEvent = true;
        StopRouteMovement();
    }

    public void StraightenForExternalEvent()
    {
        ResetBalanceStateAfterExternalEvent();
    }

    // イベント終了後は現在地点と進行方向を維持し、バランス状態だけを中央へ戻します。
    public void FinishExternalEventPause(bool resumeMovement)
    {
        if (!isPausedForExternalEvent)
        {
            if (!resumeMovement)
            {
                StopRouteMovement();
            }

            return;
        }

        ResetBalanceStateAfterExternalEvent();
        isPausedForExternalEvent = false;
        waitForBalanceInputRelease = true;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            mousePos = mouse.position.ReadValue();
        }

        if (resumeMovement && !isManagedFallActive)
        {
            isStop = false;
        }
        else
        {
            StopRouteMovement();
        }
    }

    private bool TryInitializeRequiredReferences()
    {
        WallRb = GetComponent<Rigidbody>();
        if (WallRb == null)
        {
            Debug.LogError("TrolleyWall: 同じGameObjectにRigidbodyが必要です。処理を停止します。", this);
            return false;
        }

        if (Player == null)
        {
            Debug.LogError("TrolleyWall: Player参照が未設定です。処理を停止します。", this);
            return false;
        }

        PlayerRb = Player.GetComponent<Rigidbody>();
        if (PlayerRb == null)
        {
            Debug.LogError("TrolleyWall: PlayerにRigidbodyがありません。処理を停止します。", this);
            return false;
        }

        if (trolley == null)
        {
            Debug.LogError("TrolleyWall: Trolley参照が未設定です。処理を停止します。", this);
            return false;
        }

        trolleyjoin = trolley.GetComponent<ConfigurableJoint>();
        if (trolleyjoin == null)
        {
            Debug.LogError("TrolleyWall: TrolleyにConfigurableJointがありません。処理を停止します。", this);
            return false;
        }

        trolleyRigidbody = trolley.GetComponent<Rigidbody>();

        if (balanceBarTransform == null)
        {
            Debug.LogError("TrolleyWall: Balance Bar Transformが未設定です。処理を停止します。", this);
            return false;
        }

        CaptureBalanceBarBaseLocalRotation();

        return true;
    }

    private bool TryInitializeJoyConInput()
    {
        joyConInitializationAttempted = true;

        JoyconManager joyConManager = JoyconManager.Instance;
        if (joyConManager == null)
        {
            Debug.LogWarning("TrolleyWall: JoyconManagerが見つからないため、キーボードとマウスへ切り替えます。", this);
            return false;
        }

        joycons = joyConManager.j;
        if (joycons == null || joycons.Count == 0)
        {
            Debug.LogWarning("TrolleyWall: 接続中のJoy-Conがないため、キーボードとマウスへ切り替えます。", this);
            return false;
        }

        // 最初に見つかったJoy-Con（左でも右でも可）を操作用として割り当て
        myJoycon = joycons[0];
        if (myJoycon != null)
        {
            neutralCaptured = false;
            joyConMinimumWaitStarted = false;
            hasJoyConStabilityReference = false;
            joyConStableElapsed = 0f;
            latestBarRoll = 0f;
        }

        return myJoycon != null;
    }

    //=================================================================================
    // Update()では主に操作(棒)の処理をする
    //=================================================================================
    private void Update()
    {
        // カメラがはなれている時などは止まる
        if (isStop || isPausedForExternalEvent || isManagedFallActive) { return; }
        // ロープから離れていれば処理しない
        if (isTouchingRope == false) { return; }

        if (waitForBalanceInputRelease)
        {
            Keyboard routeKeyboard = Keyboard.current;
            bool arrowKeyHeld = routeKeyboard != null &&
                (routeKeyboard.leftArrowKey.isPressed || routeKeyboard.rightArrowKey.isPressed ||
                 routeKeyboard.upArrowKey.isPressed || routeKeyboard.downArrowKey.isPressed);
            if (arrowKeyHeld)
            {
                return;
            }

            // キーを離した直後も1フレーム待ち、分岐入力やマウス差分を棒へ持ち越しません。
            waitForBalanceInputRelease = false;
            latestBarRoll = 0f;
            return;
        }

        if (useJoyConInput && !joyConInitializationAttempted)
        {
            TryInitializeJoyConInput();
        }

        bool isUsingJoyConThisFrame = useJoyConInput && myJoycon != null;
        if (isUsingJoyConThisFrame)
        {
            UpdateJoyConInput();
        }
        else
        {
            UpdateKeyboardAndMouseInput();
        }


        // 決定した角度を棒のグラフィックに適用（Joy-Conでもキーボードでも共通）
        if (balanceBarTransform != null)
        {
            float displayRoll = -latestBarRoll;
            ApplyBalanceBarRotation(displayRoll);
        }
    }

    // Sceneで調整済みの棒の向きを基準として保存し、通常バランス入力だけを追加します。
    private void CaptureBalanceBarBaseLocalRotation()
    {
        if (hasCapturedBalanceBarBaseLocalRotation || balanceBarTransform == null)
        {
            return;
        }

        balanceBarBaseLocalRotation = balanceBarTransform.localRotation;
        hasCapturedBalanceBarBaseLocalRotation = true;
    }

    private void ApplyBalanceBarRotation(float roll)
    {
        if (balanceBarTransform == null)
        {
            return;
        }

        CaptureBalanceBarBaseLocalRotation();
        Transform balanceBarParent = balanceBarTransform.parent;
        Vector3 rollAxisInParent = balanceBarParent != null
            ? balanceBarParent.InverseTransformDirection(WallRb.transform.forward)
            : WallRb.transform.forward;
        Quaternion balanceRollRotation = Quaternion.AngleAxis(roll, rollAxisInParent);
        balanceBarTransform.localRotation = balanceRollRotation * balanceBarBaseLocalRotation;
    }

    private void UpdateJoyConInput()
    {
        Quaternion joyconRotation = myJoycon.GetVector();

        if (!neutralCaptured)
        {
            latestBarRoll = 0f;
            if (myJoycon.state < Joycon.state_.IMU_DATA_OK)
            {
                joyConMinimumWaitStarted = false;
                hasJoyConStabilityReference = false;
                joyConStableElapsed = 0f;
            }
            else if (!joyConMinimumWaitStarted)
            {
                joyConMinimumWaitStarted = true;
                joyConImuReadyTime = Time.unscaledTime;
                hasJoyConStabilityReference = false;
                joyConStableElapsed = 0f;
            }
            else if (Time.unscaledTime - joyConImuReadyTime >= Mathf.Max(0f, joyConNeutralMinimumWait))
            {
                if (!hasJoyConStabilityReference)
                {
                    joyConStabilityReference = joyconRotation;
                    hasJoyConStabilityReference = true;
                    joyConStableElapsed = 0f;
                }
                else
                {
                    float stabilityAngle =
                        Quaternion.Angle(joyConStabilityReference, joyconRotation);
                    if (stabilityAngle <= Mathf.Max(0f, joyConNeutralAngleTolerance))
                    {
                        joyConStableElapsed += Time.unscaledDeltaTime;
                    }
                    else
                    {
                        joyConStabilityReference = joyconRotation;
                        joyConStableElapsed = 0f;
                    }
                }

                if (hasJoyConStabilityReference &&
                    joyConStableElapsed >= Mathf.Max(0f, joyConNeutralStableDuration))
                {
                    neutralJoyConRotation = joyconRotation;
                    neutralCaptured = true;
                }
            }
        }
        else
        {
            Quaternion relativeRotation =
                Quaternion.Inverse(neutralJoyConRotation) * joyconRotation;
            Vector3 rotatedForward = relativeRotation * Vector3.forward;
            Vector3 projectedForward = Vector3.ProjectOnPlane(rotatedForward, Vector3.up);
            if (projectedForward.sqrMagnitude > JoyConProjectedVectorMinSqrMagnitude)
            {
                float quaternionRollY = Vector3.SignedAngle(
                    Vector3.forward,
                    projectedForward.normalized,
                    Vector3.up);
                float joyConRoll = quaternionRollY;
                float adjustedRoll = joyConRoll * joyConSensitivity;
                float safeBarMaxAngle = Mathf.Max(0f, BarMaxAngle);
                latestBarRoll = Mathf.Clamp(adjustedRoll, -safeBarMaxAngle, safeBarMaxAngle);
            }
            else
            {
                latestBarRoll = 0f;
            }
        }
    }

    private void UpdateKeyboardAndMouseInput()
    {
        Keyboard keyboard = Keyboard.current;
        bool leftPressed = keyboard != null && keyboard.leftArrowKey.isPressed;
        bool rightPressed = keyboard != null && keyboard.rightArrowKey.isPressed;
        bool hasKeyboardInput = leftPressed || rightPressed;

        // 矢印キー入力中はマウスよりキーボードを優先する
        if (hasKeyboardInput)
        {
            float safeKeyboardBalanceStrength = Mathf.Max(0f, keyboardBalanceStrength);
            if (leftPressed && !rightPressed)
            {
                latestBarRoll -= safeKeyboardBalanceStrength * Time.deltaTime;
            }
            else if (rightPressed && !leftPressed)
            {
                latestBarRoll += safeKeyboardBalanceStrength * Time.deltaTime;
            }
        }
        else if (TryGetMouseTargetRoll(out float mouseTargetRoll))
        {
            latestBarRoll = mouseTargetRoll;
        }
        else
        {
            latestBarRoll = Mathf.MoveTowards(latestBarRoll, 0f, Mathf.Max(0f, returnToCenterSpeed) * Time.deltaTime);
        }

        float safeBarMaxAngle = Mathf.Max(0f, BarMaxAngle);
        latestBarRoll = Mathf.Clamp(latestBarRoll, -safeBarMaxAngle, safeBarMaxAngle);
    }

    private bool TryGetMouseTargetRoll(out float targetRoll)
    {
        targetRoll = 0f;

        Mouse mouse = Mouse.current;
        if (mouse == null || Screen.width <= 0)
        {
            return false;
        }

        float horizontalMove = mouse.delta.ReadValue().x;
        if (Mathf.Abs(horizontalMove) < Mathf.Max(0f, mouseMoveThreshold))
        {
            return false;
        }

        mousePos = mouse.position.ReadValue();
        float screenCenterX = Screen.width * 0.5f;
        float normalizedX = Mathf.Clamp((mousePos.x - screenCenterX) / screenCenterX, -1f, 1f);

        if (Mathf.Abs(normalizedX) <= Mathf.Clamp01(mouseDeadZone))
        {
            normalizedX = 0f;
        }

        targetRoll = normalizedX * Mathf.Max(0f, BarMaxAngle) * Mathf.Max(0f, mouseBalanceSensitivity);
        return true;
    }

    //=================================================================================
    // オブジェクトを動かす処理
    //=================================================================================
    public float heliAngle = 0f;
    public float heliAngularVelocity = 0f;
    private void FixedUpdate()
    {

        //通常モードの停止条件
        if (isStop || isPausedForExternalEvent || isManagedFallActive)
        {
            StopRigidBodyMotion();
            return;
        }





        // ロープから離れていれば処理しない
        if (isTouchingRope == false) return;

        DebugLog("止まっていない");
        float currentWobble = 0f;

        // ★ ヘリイベント中は前進だけ止める
        if (balanceOnlyMode)
        {
            WallRb.linearVelocity = Vector3.zero;
        }
        else
        {

        
        if (autoWalk)
        {
            WallRb.linearVelocity = WallRb.transform.forward * Mathf.Max(0f, moveSpeed);
            // サイン波（Mathf.Sin）を使い、時間の経過（Time.time）に合わせて、大きな波のようにじわ〜っと揺らす。
            // Natural Sway Speedを小さくするほど、よりスローでぬるっとした波になります。
            float slowWave = Mathf.Sin(Time.time * Mathf.Max(0f, naturalSwaySpeed));

            // 滑らかな波に wobbleIntensity（揺れの強さ）をかけ算して、回転力（トルク）に変換します。
            currentWobble = slowWave * Mathf.Max(0f, wobbleIntensity);
        }
        else
        {
            // Wキーによる前進処理と、前進時のブレ（キッカケ）の計算 ───

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.wKey.isPressed)
            {

                // 壁をうごかす
                WallRb.linearVelocity = WallRb.transform.forward * Mathf.Max(0f, moveSpeed);

                // サイン波（Mathf.Sin）を使い、時間の経過（Time.time）に合わせて、大きな波のようにじわ〜っと揺らす。
                // Natural Sway Speedを小さくするほど、よりスローでぬるっとした波になります。
                float slowWave = Mathf.Sin(Time.time * Mathf.Max(0f, naturalSwaySpeed));

                // 滑らかな波に wobbleIntensity（揺れの強さ）をかけ算して、回転力（トルク）に変換します。
                currentWobble = slowWave * Mathf.Max(0f, wobbleIntensity);

            }
            else
            {
                WallRb.linearVelocity = Vector3.zero;
            }
        }
    }



        if (connectedRigidbody == null) { return; }
        // 接続先のロープパーツのtransformを取得
        Transform connectedBody = connectedRigidbody.transform;


        //--------------------------------------------------------
        // ここら辺から疑似的なHingeJointの処理
        //--------------------------------------------------------

        // 1.プレイヤーなどがぶつかった「外力（トルク）」を抽出
        float totalTorque = CalculateExternalTorque();

        // 前進によるランダムな揺らぎ（ブレ）を回転力に足す
        totalTorque += currentWobble;

        // 支点（Anchor）から重心（Center of Mass）までの「距離」を計算
        float torqueArmLength = Vector3.Distance(localAnchor, localCenterOfMass);


        // 2. 土台の傾きに応じた重力の影響を計算
        if (applyFallForce)
        { 

            // 距離が0でなければ、重心の離れ具合に応じて倒れる力（トルク）を増幅させる
            if (torqueArmLength > 0.001f)
            {
                // 角度（sin） × 倒れる力 × 重心までの距離
                float fallTorque = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * Mathf.Max(0f, fallSpeedScale) * torqueArmLength;
                totalTorque += fallTorque;


            }
            else
            {
                // 重心が支点と全く同じ位置にある場合のフォールバック計算
                float fallTorque = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * Mathf.Max(0f, fallSpeedScale);
                totalTorque += fallTorque;
            }
        }

        // 棒の回転とその傾きによる回転を与える
        angularVelocity += -latestBarRoll * Mathf.Max(0f, postureControlStrength) * Time.fixedDeltaTime;

        // 3. 運動方程式による角速度の計算 (トルク / 質量 = 角加速度)
        float angularAcceleration = totalTorque / PlayerRb.mass;
        angularVelocity += angularAcceleration * Time.fixedDeltaTime;

        // 4. 摩擦（空気抵抗）の適用
        angularVelocity *= Mathf.Clamp01(1.0f - Mathf.Max(0f, angularDrag) * Time.fixedDeltaTime);



        // 5. 角度の更新と制限（Limits）の処理
        currentAngle += angularVelocity * Time.fixedDeltaTime;

        if (useLimits && maxAngle > minAngle)
        {
            if (currentAngle < minAngle || currentAngle > maxAngle)
            {
                if (useManagedFallFlow)
                {
                    BeginManagedFall();
                    return;
                }

                BeginLegacyPhysicalFall();
            }
        }

        // 6. 土台の位置・回転に同期させながらオブジェクトを配置
        ApplyConnectedHingeTransforms(connectedBody);

        // ================================================================
        // 7. 台（棒）に対して「回転力」と「重力・直線的な力」の両方を伝える
        // ================================================================

        // --- 7a. 回転力（トルク）の伝達 ---
        Vector3 currentWorldAxis = connectedBody.rotation * (initialLocalRotation * rotationAxis);
        float safeReactionForceScale = Mathf.Max(0f, reactionForceScale);
        float reactionTorqueMagnitude = -(angularAcceleration * PlayerRb.mass) * safeReactionForceScale;
        Vector3 reactionTorque = currentWorldAxis * reactionTorqueMagnitude;
        connectedRigidbody.AddTorque(reactionTorque, ForceMode.Force);

        // --- 7b. 【追加】直線的な力（フォース）の伝達 ---
        // 1. カプセル自体の純粋な重力を計算 (F = m * g)
        Vector3 capsuleGravityForce = Physics.gravity * PlayerRb.mass;

        // 2. カプセルが回転・移動したことで発生する慣性力（遠心力）を簡易シミュレート
        // 軸方向への引っ張り力を計算
        Vector3 worldAnchorPos = connectedBody.TransformPoint(initialLocalAnchorOffset);
        Vector3 worldCoMPos = Player.transform.TransformPoint(localCenterOfMass);
        Vector3 armDirection = (worldCoMPos - worldAnchorPos).normalized;

        // 遠心力 = 質量 × (角速度^2) × 半径
        float angularVelocityRad = angularVelocity * Mathf.Deg2Rad;
        float centrifugalForceMagnitude = PlayerRb.mass * (angularVelocityRad * angularVelocityRad) * Mathf.Max(torqueArmLength, 0.1f);
        Vector3 centrifugalForce = armDirection * centrifugalForceMagnitude;

        // 3. 重力と遠心力を合算し、倍率をかけて土台のRigidbodyに伝える
        Vector3 totalForceToPlatform = (capsuleGravityForce + centrifugalForce) * safeReactionForceScale;

        // 土台（棒）の位置に対して下向き・横向きの直接的な力を加える
        connectedRigidbody.AddForce(totalForceToPlatform, ForceMode.Force);
    }

    // 一番最初にロープパーツに接続した瞬間の初期化処理
    private void InitializeHinge(Transform connectedBody)
    {


        Player.transform.rotation = Quaternion.Euler(0, 90, 0);

        // 土台から見た、初期の相対回転と軸（アンカー）の相対位置を記録
        initialLocalRotation = Quaternion.Inverse(connectedBody.rotation) * Player.transform.rotation;
        Vector3 worldAnchor = Player.transform.TransformPoint(localAnchor);
        initialLocalAnchorOffset = connectedBody.InverseTransformPoint(worldAnchor);


        isInitialized = true;
    }


    // 衝突によるエネルギーをスクリプト用の回転力に変換
    private float CalculateExternalTorque()
    {
        Vector3 totalAngularVelocity = PlayerRb.angularVelocity;
        Vector3 worldAxis = connectedRigidbody.transform.rotation * (initialLocalRotation * rotationAxis);

        float externalTorque = Vector3.Dot(totalAngularVelocity, worldAxis) * Mathf.Rad2Deg * PlayerRb.mass;

        PlayerRb.linearVelocity = Vector3.zero;
        PlayerRb.angularVelocity = Vector3.zero;

        return externalTorque;
    }

    // 土台の動きを基準にして、位置と回転を最終決定する
    private void ApplyConnectedHingeTransforms(Transform connectedBody)
    {

        // 土台(WallRb)の現在の向き ＋ 現在のヒンジの回転角
        Quaternion hingeRotation = Quaternion.AngleAxis(currentAngle, rotationAxis);
        Quaternion targetRotation = WallRb.rotation * hingeRotation;

        // 土台の移動に合わせて、軸（アンカー）の現在あるべきワールド座標を計算
        Vector3 worldAnchorPosition = connectedBody.TransformPoint(initialLocalAnchorOffset);

        // 回転を先に適用
        Player.transform.localRotation = targetRotation;

        // 軸がズレないように、回転後のオブジェクトの位置を補正して固定
        Vector3 currentLocalAnchorWorld = Player.transform.TransformPoint(localAnchor);
        Player.transform.position += (worldAnchorPosition - currentLocalAnchorWorld);
    }

    // 軸の位置をインスペクター上で視覚化
    private void OnDrawGizmosSelected()
    {
        if (Player == null)
        {
            return;
        }

        // 支点（Anchor）を緑の球で描画
        Gizmos.color = Color.green;
        Vector3 worldAnchor = Player.transform.TransformPoint(localAnchor);
        Gizmos.DrawSphere(worldAnchor, 0.08f);
        Gizmos.DrawRay(worldAnchor, Player.transform.TransformDirection(rotationAxis) * 1.5f);

        // 重心（Center of Mass）を青い球で描画
        Gizmos.color = Color.blue;
        Vector3 worldCoM = Player.transform.TransformPoint(localCenterOfMass);
        Gizmos.DrawSphere(worldCoM, 0.08f);

        // 支点から重心への繋がりを黄色い線で描画（これがレバーの長さになります）
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(worldAnchor, worldCoM);
    }

    //=================================================================================
    // プレイヤーがロープの上にいる間の処理(壁がロープに当たっている間)
    //=================================================================================
    private void OnTriggerStay(Collider other)
    {
        // イベント中は棒・手・プレイヤーをイベント側へ完全に任せ、座標を上書きしません。
        if (isPausedForExternalEvent || isManagedFallActive)
        {
            return;
        }

        if (other.CompareTag("RopeParts"))
        {
            // 最初の初期化処理
            if (isInitialized == false) { InitializeHinge(other.transform); }

            // RigitBodyの設定
            Rigidbody OtherConnecctBody = other.GetComponent<Rigidbody>();
            if (OtherConnecctBody == null)
            {
                if (!hasLoggedMissingRopeRigidbody)
                {
                    Debug.LogError("TrolleyWall: RopePartsタグのColliderにRigidbodyがありません。", other);
                    hasLoggedMissingRopeRigidbody = true;
                }

                return;
            }

            connectedRigidbody = OtherConnecctBody;
            trolleyjoin.connectedBody = OtherConnecctBody;

            // 壁がロープに当たった地点を計算し、そこにtrolleyを移動させる
            Vector3 targetPosition = other.ClosestPointOnBounds(this.transform.position);
            trolley.transform.position = targetPosition;
            trolley.transform.parent = other.transform;

            // プレイヤーの位置調整用（なくてもいい）
            CapsuleCollider capsuleCollider = Player.GetComponent<CapsuleCollider>();
            Vector3 trolleyPosition = trolley.transform.position;


            // ロープから離れたら
            if (isTouchingRope == false)
            {
                // 一度だけ、壁がロープに当たった地点にプレイヤーを移動させる
                if (EndMove == false)
                {
                    Player.transform.position = targetPosition - new Vector3(0.0f, 0.09f, 0.0f);
                    EndMove = true;
                }

                // プレイヤーのRigitbodyをもとに戻す
                PlayerRb.useGravity = true;
                connectedRigidbody = null;

                PlayerRb.constraints = RigidbodyConstraints.None;

                return;
            }

            // trolleyの位置(ロープと壁が当たった地点)にプレイヤーを移動させる。new Vectorは調整
            Player.transform.position = trolleyPosition - new Vector3(0.0f, 0.09f, 0.0f);



            // 揺れを伝えるオブジェクトにプレイヤーの回転を反映させる
            // TODO : アティチュード・インジケーターのようなUIをやってみる
            //Quaternion shakingOb = Quaternion.Euler(0, 0, Player.transform.rotation.x);
            //ShakingObject.transform.rotation = Quaternion.Slerp(ShakingObject.transform.rotation, shakingOb, Time.deltaTime * 5.0f);

        }



        DebugLog("ropeに当たっているはず");


    }
    public void SetBarRotationAxis()
    {
        // 回転方向を設定する
        rotationAxis = WallRb.transform.right;
    }

    public void ResetRotation()
    {
        // 物理回転のリセット
        currentAngle = 0f;
        angularVelocity = 0f;
        latestBarRoll = 0f;

        // 棒の回転リセット
        if (balanceBarTransform != null)
        {
            ApplyBalanceBarRotation(0f);
        }

        // プレイヤーの回転リセット（必要なら）
        if (Player != null)
        {
            Player.transform.localRotation = Quaternion.identity;
        }
    }

    // 一時的に操作、回転をとめることができる
    public void IsStop(bool change)
    {
        if (!change && (isPausedForExternalEvent || isManagedFallActive))
        {
            isStop = true;
            return;
        }

        isStop = change;
        if (change)
        {
            StopRigidBodyMotion();
        }
    }

    public bool IsStop()
    {
        return isStop;
    }

    private void StopRigidBodyMotion()
    {
        if (WallRb != null)
        {
            WallRb.linearVelocity = Vector3.zero;
            WallRb.angularVelocity = Vector3.zero;
        }

        if (PlayerRb != null)
        {
            PlayerRb.linearVelocity = Vector3.zero;
            PlayerRb.angularVelocity = Vector3.zero;
        }

        if (trolley != null)
        {
            Rigidbody trolleyRigidbody = trolley.GetComponent<Rigidbody>();
            if (trolleyRigidbody != null)
            {
                trolleyRigidbody.linearVelocity = Vector3.zero;
                trolleyRigidbody.angularVelocity = Vector3.zero;
            }
        }
    }
    

    private void ResetBalanceStateAfterExternalEvent()
    {
        currentAngle = 0f;
        angularVelocity = 0f;
        latestBarRoll = 0f;
        isTouchingRope = true;

        if (balanceBarTransform != null)
        {
            ApplyBalanceBarRotation(0f);
        }

        StopRigidBodyMotion();
        EndMove = false;

        if (Player == null)
        {
            return;
        }

        // ルート方向のY回転は残し、綱渡りで使うZ方向の傾きだけを中央へ戻します。
        Vector3 playerEulerAngles = Player.transform.eulerAngles;
        playerEulerAngles.z = 0f;
        Player.transform.eulerAngles = playerEulerAngles;

        // 現在のロープと現在位置を基準にヒンジ情報を取り直し、開始点へ戻さないようにします。
        if (connectedRigidbody != null)
        {
            initialLocalRotation = Quaternion.Inverse(connectedRigidbody.rotation) * Player.transform.rotation;
            Vector3 worldAnchor = Player.transform.TransformPoint(localAnchor);
            initialLocalAnchorOffset = connectedRigidbody.transform.InverseTransformPoint(worldAnchor);
            isInitialized = true;
        }
    }

    private void BeginManagedFall()
    {
        if (isManagedFallActive || hasNotifiedManagedFall)
        {
            return;
        }

        CaptureManagedFallPhysicsState();

        // 通知先が同じフレーム内で別処理を行っても、先にガードを確定して二重通知を防ぎます。
        isManagedFallActive = true;
        hasNotifiedManagedFall = true;
        isStop = true;
        angularVelocity = 0f;
        latestBarRoll = 0f;
        StopRigidBodyMotion();

        Action<float> fallHandler = ManagedFallStarted;
        if (fallHandler == null)
        {
            Debug.LogWarning("TrolleyWall: 管理落下の通知先がありません。安全のため停止を維持します。", this);
            return;
        }

        fallHandler.Invoke(currentAngle);
    }
    
    private void CaptureManagedFallPhysicsState()
    {
        managedFallWallRigidbodyState = RigidbodyStateSnapshot.Capture(WallRb);
        managedFallPlayerRigidbodyState = RigidbodyStateSnapshot.Capture(PlayerRb);
        managedFallTrolleyRigidbodyState = RigidbodyStateSnapshot.Capture(trolleyRigidbody);
        managedFallConnectedRigidbody = connectedRigidbody;
        managedFallJointConnectedBody = trolleyjoin != null ? trolleyjoin.connectedBody : null;
        hasCapturedManagedFallPhysicsState = true;
    }

    private void RestoreManagedFallPhysicsState()
    {
        if (!hasCapturedManagedFallPhysicsState)
        {
            Debug.LogWarning("TrolleyWall: 落下前のRigidbody状態が保存されていないため、現在設定のまま復帰します。", this);
            return;
        }

        managedFallWallRigidbodyState.Restore(WallRb);
        managedFallPlayerRigidbodyState.Restore(PlayerRb);
        managedFallTrolleyRigidbodyState.Restore(trolleyRigidbody);
        PlayerRb.centerOfMass = localCenterOfMass;
    }

    private void BeginLegacyPhysicalFall()
    {
        // Legacy physical fall:
        // 従来どおりOnTriggerStay側で重力を有効化し、Constraintsを解除して物理落下させます。
        isTouchingRope = false;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log(message, this);
        }
    }
    public void SetBalanceOnlyMode(bool enabled)
    {
        balanceOnlyMode = enabled;
    }
}
