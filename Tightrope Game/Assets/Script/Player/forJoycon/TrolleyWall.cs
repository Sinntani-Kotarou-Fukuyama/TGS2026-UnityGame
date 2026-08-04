using System.Collections.Generic;
using UnityEditor.Build;
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


    [SerializeField, Header("操作タイプ")] private Type type;
    [SerializeField, Header("棒の最大角度(Joyconは例外)")] private float BarMaxAngle = 90f;
    [SerializeField, Header("Playerを自動で歩かせる")] private bool autoWalk = false;
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


    // JoyconLibのクラスを保持する変数
    private List<Joycon> joycons;
    private Joycon myJoycon;

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

    [Header("Limits (角度制限)")]
    [SerializeField] private bool useLimits = true;
    [SerializeField] private float minAngle = -90f;
    [SerializeField] private float maxAngle = 90f;
    [SerializeField] private float bounciness = 0.2f; // 壁に衝突した時の跳ね返り強度

    [Header("Physics Settings")]
    [SerializeField] private float angularDrag = 1.0f; // 摩擦・空気抵抗（大きいほど早く止まる）
    [SerializeField] private bool applyGravity = true; // 傾いたときに重力で戻るか


    [Header("Fall Assist (自動で傾く力)")]
    [SerializeField] private bool applyFallForce = true;
    [SerializeField] private float fallSpeedScale = 50.0f; // この数値を大きくするほど、傾いた方向へ強く・速く倒れていく

    [Header("Posture Control (姿勢制御の強さ)")]
    [SerializeField] private float postureControlStrength = 15.0f; // 【この値を大きくすると、棒の傾きに体が素早く追従する

    [Header("Reaction Force (土台への反作用)")]
    [SerializeField] private float reactionForceScale = 1.0f;

    [Header("Move Settings (前進とブレ)")]
    [SerializeField] private float moveSpeed = 1.5f; // 前進速度
    [SerializeField] private float wobbleIntensity = 0.5f; // 【追加】前進時に発生する「ブレ」の強さ

    private Rigidbody PlayerRb; // プレイヤーのRigidBody
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalAnchorOffset;

    private float currentAngle = 0f;
    private float angularVelocity = 0f; // 角速度 (度/秒)
    private bool isInitialized = false;
    private bool isStop = false; //カメラが離れて見えづらい時などに一時的に操作、回転を止める用

    //[Header("揺れを伝えるオブジェクト")]
    //[SerializeField] private GameObject ShakingObject;



    // ロープの接続に関する判定をするフラグ
    private bool isTouchingRope = true;   // ロープの上に乗っているか？
    private bool EndMove = false;         // ロープから離れるときに一度だけ使うフラグ(プレイヤーを移動させるため1)



    private void Start()
    {
        // 壁のRigitbodyを取得
        WallRb = this.GetComponent<Rigidbody>();

        // プレイヤーのRigitbodyを取得
        PlayerRb = Player.GetComponent<Rigidbody>();

        // 物理の自動衝突検知は残しつつ、自動の移動・回転をカット（スクリプトで制御するため）
        PlayerRb.isKinematic = false;
        PlayerRb.useGravity = false;
        PlayerRb.constraints = RigidbodyConstraints.FreezeAll;

        // Rigidbody自体に自作の重心位置を覚えさせる（物理的な回転の慣性に影響する）
        PlayerRb.centerOfMass = localCenterOfMass;

        // 回転軸
        rotationAxis = rotationAxis.normalized;

        // trolleyのConfigurableJointを取得
        trolleyjoin = trolley.GetComponent<ConfigurableJoint>();


        // joyconの設定
        joycons = JoyconManager.Instance.j;

        if (joycons != null && joycons.Count > 0)
        {
            // 最初に見つかったJoy-Con（左でも右でも可）を操作用として割り当て
            myJoycon = joycons[0];
        }


    }

    //=================================================================================
    // Update()では主に操作(棒)の処理をする
    //=================================================================================
    private void Update()
    {
        // カメラがはなれている時などは止まる
        if (isStop) { return; }
        // ロープから離れていれば処理しない
        if (isTouchingRope == false) { return; }
        

        float centerXorYPos, ratio, targetZAngle, distanceFromCenter;
        Quaternion targetRotation;
        switch (type)
        {
            case Type.Joycon:
                // ---Joy-Conの傾き入力 ---
                if (myJoycon != null)
                {
                    // --- Joy-Conが接続されている場合の処理 ---
                    Quaternion joyconRotation = myJoycon.GetVector();

                    Vector3 euler = joyconRotation.eulerAngles;

                    // Z軸の回転を -180〜180 に補正
                    latestBarRoll = euler.z + 90f;
                    if (latestBarRoll > (180f + 80f)) latestBarRoll -= (360f + 90f);

                }
                else
                {
                    type = Type.X;
                }
                break;

            case Type.X: // マウスのX軸の位置で動かす

                // 画面の横幅の中心座標を取得
                centerXorYPos = Screen.width / 2f;

                mousePos = Mouse.current.position.ReadValue();

                // 中心からの距離を計算（右がプラス、左がマイナスになる）
                distanceFromCenter = mousePos.x - centerXorYPos;

                // 画面中央から端までの割合（-1.0 ～ 1.0）を計算
                // Clampで画面外にマウスが出ても範囲内に収めます
                ratio = Mathf.Clamp(distanceFromCenter / centerXorYPos, -1.0f, 1.0f);

                // 割合に応じて角度を計算
                // 右（プラス）に行くと右回転（Unityではマイナス）させたいので、-maxAngleを掛け算
                targetZAngle = ratio * BarMaxAngle;


                latestBarRoll = targetZAngle;

                // 行き過ぎ防止のために限界の傾き角度（例: ±45度）で制限をかける
                latestBarRoll = Mathf.Clamp(latestBarRoll, -BarMaxAngle, BarMaxAngle);
                break;

            case Type.Y:  // マウスのY軸の位置で動かす

                // 画面の縦幅の中心座標を取得
                centerXorYPos = Screen.height / 2f;

                mousePos = Mouse.current.position.ReadValue();

                // 中心からの距離を計算（右がプラス、左がマイナスになる）
                distanceFromCenter = mousePos.y - centerXorYPos;

                // 画面中央から端までの割合（-1.0 ～ 1.0）を計算
                ratio = Mathf.Clamp(distanceFromCenter / centerXorYPos, -1.0f, 1.0f);

                // 割合に応じて角度を計算
                targetZAngle = ratio * -BarMaxAngle;


                latestBarRoll = targetZAngle;

                // 行き過ぎ防止のために限界の傾き角度（例: ±45度）で制限をかける
                latestBarRoll = Mathf.Clamp(latestBarRoll, -BarMaxAngle, BarMaxAngle);
                break;

            case Type.Keyboard: // キーボード操作

                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null)
                {
                    // Aキーで左に傾く（プラス）、Dキーで右に傾く（マイナス）
                    // ※押し続けるとじわじわ傾くように Time.deltaTime を掛けて調整
                    // 「30f」の数値を大きくすると、キーを押したときの回転スピードが上がる
                    if (keyboard.aKey.isPressed) latestBarRoll -= 30f * Time.deltaTime;
                    if (keyboard.dKey.isPressed) latestBarRoll += 30f * Time.deltaTime;



                    // 何も押していない時は、自動でゆっくり水平（0度）に戻る（なくてもいい）
                    if (!keyboard.aKey.isPressed && !keyboard.dKey.isPressed)
                    {
                        latestBarRoll = Mathf.MoveTowards(latestBarRoll, 0f, 20f * Time.deltaTime);
                    }

                    // 行き過ぎ防止のために限界の傾き角度（例: ±45度）で制限をかける
                    latestBarRoll = Mathf.Clamp(latestBarRoll, -BarMaxAngle, BarMaxAngle);
                }
                break;
        }


        // 決定した角度を棒のグラフィックに適用（Joy-Conでもキーボードでも共通）
        if (balanceBarTransform != null)
        {
            balanceBarTransform.rotation = Quaternion.Euler(0, 0, latestBarRoll);
        }
    }

    //=================================================================================
    // オブジェクトを動かす処理
    //=================================================================================
    private void FixedUpdate()
    {
        // カメラがはなれている時などは止まる
        if (isStop)
        {
            WallRb.linearVelocity = Vector3.zero; // 移動停止
            return;  // 回転停止
        }

        // ロープから離れていれば処理しない
        if (isTouchingRope == false) return;
        
        Debug.Log("止まっていない");
        float currentWobble = 0f;
        if (autoWalk)
        {
            WallRb.linearVelocity = WallRb.transform.forward * moveSpeed;
            // サイン波（Mathf.Sin）を使い、時間の経過（Time.time）に合わせて、大きな波のようにじわ〜っと揺らす。
            //  Time.time * 1.5f の「1.5f」を小さくするほど、よりスローでぬるっとした波になる。
            float slowWave = Mathf.Sin(Time.time * 1.0f);

            // 滑らかな波に wobbleIntensity（揺れの強さ）をかけ算して、回転力（トルク）に変換します。
            currentWobble = slowWave * wobbleIntensity;
        }
        else
        {
            // Wキーによる前進処理と、前進時のブレ（キッカケ）の計算 ───

            if (Keyboard.current.wKey.isPressed)
            {

                // 壁をうごかす
                WallRb.linearVelocity = WallRb.transform.forward * moveSpeed;

                // サイン波（Mathf.Sin）を使い、時間の経過（Time.time）に合わせて、大きな波のようにじわ〜っと揺らす。
                //  Time.time * 1.5f の「1.5f」を小さくするほど、よりスローでぬるっとした波になる。
                float slowWave = Mathf.Sin(Time.time * 1.0f);

                // 滑らかな波に wobbleIntensity（揺れの強さ）をかけ算して、回転力（トルク）に変換します。
                currentWobble = slowWave * wobbleIntensity;

            }
            else
            {
                WallRb.linearVelocity = Vector3.zero;
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
                float fallTorque = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * fallSpeedScale * torqueArmLength;
                totalTorque += fallTorque;


            }
            else
            {
                // 重心が支点と全く同じ位置にある場合のフォールバック計算
                float fallTorque = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * fallSpeedScale;
                totalTorque += fallTorque;
            }
        }

        // 棒の回転とその傾きによる回転を与える
        angularVelocity += -latestBarRoll * postureControlStrength * Time.fixedDeltaTime;

        // 3. 運動方程式による角速度の計算 (トルク / 質量 = 角加速度)
        float angularAcceleration = totalTorque / PlayerRb.mass;
        angularVelocity += angularAcceleration * Time.fixedDeltaTime;

        // 4. 摩擦（空気抵抗）の適用
        angularVelocity *= Mathf.Clamp01(1.0f - angularDrag * Time.fixedDeltaTime);



        // 5. 角度の更新と制限（Limits）の処理
        currentAngle += angularVelocity * Time.fixedDeltaTime;

        if (useLimits)
        {
            if (currentAngle < minAngle)
            {
                isTouchingRope = false;

                //currentAngle = minAngle;
                //angularVelocity = -angularVelocity * bounciness; // 跳ね返り
            }
            else if (currentAngle > maxAngle)
            {
                isTouchingRope = false;

                //currentAngle = maxAngle;
                //angularVelocity = -angularVelocity * bounciness; // 跳ね返り
            }
        }

        // 6. 土台の位置・回転に同期させながらオブジェクトを配置
        ApplyConnectedHingeTransforms(connectedBody);

        // ================================================================
        // 7. 台（棒）に対して「回転力」と「重力・直線的な力」の両方を伝える
        // ================================================================

        // --- 7a. 回転力（トルク）の伝達 ---
        Vector3 currentWorldAxis = connectedBody.rotation * (initialLocalRotation * rotationAxis);
        float reactionTorqueMagnitude = -(angularAcceleration * PlayerRb.mass) * reactionForceScale;
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
        Vector3 totalForceToPlatform = (capsuleGravityForce + centrifugalForce) * reactionForceScale;

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
        if (other.CompareTag("RopeParts"))
        {
            // 最初の初期化処理
            if (isInitialized == false) { InitializeHinge(other.transform); }

            // RigitBodyの設定
            Rigidbody OtherConnecctBody = other.GetComponent<Rigidbody>();
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



        Debug.Log("ropeに当たっているはず");


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
        balanceBarTransform.localRotation = Quaternion.identity;

        // プレイヤーの回転リセット（必要なら）
        Player.transform.localRotation = Quaternion.identity;
    }

    // 一時的に操作、回転をとめることができる
    public void IsStop(bool change)
    {
        isStop = change;
    }
    public bool IsStop()
    {
        return isStop;
    }
}
