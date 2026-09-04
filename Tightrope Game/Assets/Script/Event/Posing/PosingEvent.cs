using UnityEngine;
using UnityEngine.AI;

public class PosingEvent : MonoBehaviour
{
    [SerializeField] public AudioSource keikokuAudio;//警告音
    [SerializeField] GameObject PosingDino;//イベント怪獣のプレハブ用
    [SerializeField] Transform Dino;//イベント中は怪獣を見えないところへ移動させる
    [SerializeField] Transform Bill;//イベントで破壊されるビルの座標
    [SerializeField] Transform player;//プレイヤーの座標
    [SerializeField] Transform stick;//棒の座標
    [SerializeField] Transform playerRightHund;//右手の座標
    [SerializeField] Transform playerLeftHund;//左手の座標
    [SerializeField] CameraSwhich cam;//カメラを切り替えれるように
    [SerializeField] BalanceManager balance;
    [SerializeField] float speed = 1f;//怪獣の移動速度
    [SerializeField] float Rotatespeed = 1f;//回転速度
    [SerializeField] float Stickspeed = 0.00000001f;//棒を持ち上げる速度
    [SerializeField] GameObject Text;
    [SerializeField] GameObject Timer;//タイマーを非表示にする用
    [SerializeField] private Behaviour _target;//点滅させる対象
    [SerializeField] public GameObject Porsemp4;//動画
    [SerializeField] private float _cycle = 1; // 点滅周期[秒]
    [SerializeField] ParentKaizyo exoloOarentReset;//親解除
    [SerializeField] NavMeshAgent agent;
    [SerializeField] KaijuAI AI;
    Quaternion startplayer;//最初の回転を記録する
    Quaternion startstick;//最初の回転を記録する
    Quaternion startplayerRightHund;//最初の回転を記録する
    Quaternion startplayerLeftHund;//最初の回転を記録する
    Vector3 startstickposition;//最初の座標を記録する
    Vector3 startplayerRightHundposition;//最初の座標を記録する
    Vector3 startplayerLeftHubdposition;//最初の座標を記録する
    private double _time;
    int StickOver = 0;
    bool DinoStoping = false;//怪獣を動かなくするフラグ
    bool Flag = false;//イベント開始フラグ
    bool DinoIdouflag = true;//怪獣移動フラグ
    bool PlayerRotation = false;
    bool KeikokuFlag;//警告フラグ
    bool PlayerResetFlag = true;//プレイヤーの回転リセット
    [SerializeField] TightropeAutoGoalMover playerMover;//プレイヤーの動き取得
    [Tooltip("新しいTrolley移動をポーズイベント中だけ停止・再開するManagerです。")]
    [SerializeField] RopeWalkManager ropeWalkManager;
    private GameObject spawneDino;
    private bool isRopeWalkPausedForPosingEvent;
    private bool hasSavedTimerVisibility;
    private bool timerWasActiveBeforePosing;
    private bool hasWarnedMissingTimer;
    Joycon jc;
    float rotationY = 0f; // キャラの現在の回転角
    float prevTwist = 0f;
    float sensitivity = 200.0f;//ジャイロの回転の強さ
    float smooth = 0.1f;//ジャイロの滑らかさ
    float stickSensitivity = 1.0f;
    private float prevPitch = 0f;

    private void Awake()
    {
        if (ropeWalkManager == null)
        {
            ropeWalkManager = FindFirstObjectByType<RopeWalkManager>(FindObjectsInactive.Include);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var joycons = JoyconManager.Instance.j;

        Debug.Log($"[Joycon Debug] Joy-Con の検出数: {joycons.Count}");

        if (joycons.Count > 0)
        {
            jc = joycons[0];
            Debug.Log("[Joycon Debug] Joy-Con が正常にセットされました。");
        }
        else
        {
            Debug.LogWarning("[Joycon Debug] Joy-Con が見つかりません。接続を確認してください。");
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (KeikokuFlag == true)
        {
            // 内部時刻を経過させる
            _time += Time.deltaTime;

            // 周期cycleで繰り返す値の取得
            // 0～cycleの範囲の値が得られる
            var repeatValue = Mathf.Repeat((float)_time, _cycle);

            // 内部時刻timeにおける明滅状態を反映
            _target.enabled = repeatValue >= _cycle * 0.5f;
           
        }
        if (Flag == true)//イベント開始フラグ
        {
            PoseEvent();
            Flag = false;
        }
        if (DinoStoping == true)//怪獣を動かなくするフラグ
        {
            agent.Warp(new Vector3(-20.8f, 0.0f, -15.0f));
           // Dino.transform.position = new Vector3(50.0f, 0.0f, 50.0f);
        }
        if (DinoIdouflag == true)
        {
            //怪獣の移動
            Vector3 move = new Vector3(0.0f, 0.0f, -2.0f) * speed * Time.deltaTime;
            if (spawneDino != null)
            {
                spawneDino.transform.position += move;
            }

        }
        if (PlayerRotation == true)
        {

            if (Input.GetKey(KeyCode.RightArrow))//右矢印を押したら左回転する
            {
                player.transform.Rotate(new Vector3(0, -Rotatespeed, 0));
            }
            if (Input.GetKey(KeyCode.LeftArrow))//左矢印を押したら右回転する
            {
                player.transform.Rotate(new Vector3(0, Rotatespeed, 0));
            }
            if (Input.GetKey(KeyCode.UpArrow))//上矢印を押したら棒を持ち上げる
            {
                if (StickOver <= 30)
                {
                    stick.transform.Translate(0.0f, 0.0f, -Stickspeed * 0.3f * Time.deltaTime);
                    playerRightHund.Translate(0.0f, 0.0f, Stickspeed * Time.deltaTime);
                    playerLeftHund.Translate(0.0f, 0.0f, Stickspeed * Time.deltaTime);
                    StickOver++;
                    Debug.Log("StickOverの値" + StickOver);
                }

            }
            if (Input.GetKey(KeyCode.DownArrow))//下矢印を押したら棒を下げる
            {
                if (StickOver >= -25)
                {
                    stick.transform.Translate(0.0f, 0.0f, Stickspeed * 0.3f * Time.deltaTime);
                    playerRightHund.Translate(0.0f, 0.0f, -Stickspeed * Time.deltaTime);
                    playerLeftHund.Translate(0.0f, 0.0f, -Stickspeed * Time.deltaTime);
                    StickOver--;
                    Debug.Log("StickOverの値" + StickOver);
                }

            }
            if (jc == null)
            {
                Debug.LogWarning("[Joycon Debug] jc が null のため Joy-Con 回転処理をスキップします。");
                return;
            }

            //ジャイロ
            Vector3 gyro = jc.GetGyro();

            //横持ちなら Z軸が回転速度
            float yawSpeed = gyro.z;

            //積分して角度にする
            rotationY += yawSpeed * Time.deltaTime * sensitivity;

            //滑らかに回す
            float newY = Mathf.LerpAngle(player.transform.eulerAngles.y, rotationY, smooth);

            player.transform.rotation = Quaternion.Euler(0, newY, 0);


            //上下の動きは X軸の回転速度
            float pitch = gyro.x;

            //ノイズ除去（しきい値）
            float threshold = 0.15f;   // ← ここが重要。0.05 は小さすぎてノイズを拾う

            //減衰フィルタ（ノイズを弱める）
            pitch = Mathf.Lerp(prevPitch, pitch, 0.2f);
            prevPitch = pitch;

            //上に動かす
            if (pitch > threshold)
            {
                if (StickOver <= 30)
                {
                    stick.transform.Translate(0.0f, 0.0f, -Stickspeed * 0.3f * Time.deltaTime);
                    playerRightHund.Translate(0.0f, 0.0f, Stickspeed * Time.deltaTime);
                    playerLeftHund.Translate(0.0f, 0.0f, Stickspeed * Time.deltaTime);
                    StickOver++;
                }
            }

            //下に動かす
            if (pitch < -threshold)
            {
                if (StickOver >= -25)
                {
                    stick.transform.Translate(0.0f, 0.0f, Stickspeed * 0.3f * Time.deltaTime);
                    playerRightHund.Translate(0.0f, 0.0f, -Stickspeed * Time.deltaTime);
                    playerLeftHund.Translate(0.0f, 0.0f, -Stickspeed * Time.deltaTime);
                    StickOver--;
                }
            }

        }

    }
    public void EventFlag() //イベントマネージャーで呼び出す
    {
        PauseRopeWalkForPosingEvent();
        HideBalanceTimerForPosingEvent();
        Flag = true;
    }

    void PoseEvent()
    {
        // プレイヤー・棒・手を書き換える前に、新しい綱渡り更新を停止します。
        PauseRopeWalkForPosingEvent();
        cam.RopeCameraCansel = true;
        //1秒後に爆発ポイントを子じゃなくする
        Invoke(nameof(ParentReset),1f);
        Vector3 currentRotation = player.transform.eulerAngles;
        currentRotation.x = 0f;
        player.transform.eulerAngles = currentRotation;
        DinoStoping = true;//怪獣を固定
        playerMover.PlayerStoping = true;//プレイヤーを固定
        startplayer = player.transform.rotation;//イベント前の回転を格納
        startstick = stick.transform.rotation;//イベント前の回転を格納
        startplayerRightHund = playerRightHund.transform.rotation;//イベント前の回転を格納
        startplayerLeftHund = playerLeftHund.transform.rotation;//イベント前の回転を格納
        startstickposition = stick.transform.position;//イベント前の座標を格納
        startplayerRightHundposition = playerRightHund.transform.position;//イベント前の座標を格納
        startplayerLeftHubdposition = playerLeftHund.transform.position;//イベント前の座標を格納
        player.transform.LookAt(Bill.transform); //プレイヤーの向きを調整する
        Invoke(nameof(DinoEvent), 0.1f);//0.1秒語にポーズイベントを始める
        rotationY = player.transform.eulerAngles.y;
        prevTwist = 0f;   // Gyro方式なら prevGyro に変更
    }
    void DinoEvent()
    {
        Quaternion rotation = Quaternion.Euler(0, 180, 0);//怪獣の向き
        //ビルの横に怪獣を生成
        Vector3 billpos = Bill.transform.position + new Vector3(0.0f, 0, 10.0f);
        spawneDino = Instantiate(PosingDino, billpos, rotation);
        //4秒後に怪獣を停止
        Invoke(nameof(DinoIdouFlag), 4.0f);
        cam.PosingCameraSet();
        balance.PauseNormalBalanceGauge();//バランスゲージを止める
        HideBalanceTimerForPosingEvent();

    }
    void ParentReset()
    {
        exoloOarentReset.ParentReset();
    }
    void DinoIdouFlag()
    {
        DinoIdouflag = false;

    }
    public void HahenTextTrue()
    {
        Text.SetActive(true);
    }
    public void HahenTextFalse()
    {
        Text.SetActive(false);
        PlayerRotation = true;
        Porsemp4.SetActive(true);
        KeikokuFlag = true;
        keikokuAudio.Play();
    }
    public void PosingFinish()
    {
       
        KeikokuFlag = false;//点滅を消す
        _target.enabled = false;
        PlayerRotation = false;//プレイヤーを回転できなくする
        cam.CameraSet();
        Invoke(nameof(GameSet), 3.5f);
        player.transform.rotation = startplayer;//回転を元に戻す
        stick.transform.rotation = startstick;//回転を元に戻す
        stick.transform.position = startstickposition;//座標を元に戻す
        playerRightHund.transform.rotation = startplayerRightHund;//回転を元に戻す
        playerLeftHund.transform.rotation = startplayerLeftHund;//回転を元に戻す
        playerRightHund.transform.position = startplayerRightHundposition;//座標を元に戻す
        playerLeftHund.transform.position = startplayerLeftHubdposition;//座標を元に戻す
    }
    
        
    void GameSet()
    {
        cam.RopeCameraCansel = false;
        RestoreBalanceTimerAfterPosingEvent();
        playerMover.PlayerStoping = false;//プレイヤーを動けるように
        DinoStoping = false;//怪獣を動けるように
        agent.Warp(new Vector3(-18.05f, 0.1f, 8.0f));
        AI.PointReset();
        //Dino.transform.position = new Vector3(-18.05f, 0.1f, 8.0f); //ビルの横に怪獣を移動
        balance.ResumeNormalBalanceGauge();
        // プレイヤー・棒・手・カメラ・ゲージを戻した後にだけ綱渡りを再開します。
        ResumeRopeWalkAfterPosingEvent();
    }

    private void HideBalanceTimerForPosingEvent()
    {
        if (Timer == null)
        {
            WarnAboutMissingTimer();
            return;
        }

        if (!hasSavedTimerVisibility)
        {
            timerWasActiveBeforePosing = Timer.activeSelf;
            hasSavedTimerVisibility = true;
        }

        Timer.SetActive(false);
    }

    private void RestoreBalanceTimerAfterPosingEvent()
    {
        if (!hasSavedTimerVisibility)
        {
            if (Timer == null)
            {
                WarnAboutMissingTimer();
            }

            return;
        }

        if (Timer != null)
        {
            Timer.SetActive(timerWasActiveBeforePosing);
        }
        else
        {
            WarnAboutMissingTimer();
        }

        hasSavedTimerVisibility = false;
    }

    private void WarnAboutMissingTimer()
    {
        if (hasWarnedMissingTimer)
        {
            return;
        }

        Debug.LogWarning("PosingEvent: Timer is not assigned. Timer visibility control was skipped.", this);
        hasWarnedMissingTimer = true;
    }

    private void PauseRopeWalkForPosingEvent()
    {
        if (isRopeWalkPausedForPosingEvent)
        {
            return;
        }

        if (ropeWalkManager == null)
        {
            Debug.LogWarning("PosingEvent: Rope Walk Managerが未設定のため、新しい綱渡りの停止をスキップします。", this);
            return;
        }

        ropeWalkManager.PauseForPosingEvent();
        isRopeWalkPausedForPosingEvent = true;
    }

    private void ResumeRopeWalkAfterPosingEvent()
    {
        if (!isRopeWalkPausedForPosingEvent)
        {
            return;
        }

        if (ropeWalkManager != null)
        {
            ropeWalkManager.ResumeAfterPosingEvent();
        }

        isRopeWalkPausedForPosingEvent = false;
    }

    private void OnDisable()
    {
        RestoreBalanceTimerAfterPosingEvent();
        // 強制終了やScene遷移でも、ポーズイベントの停止理由だけは残さないようにします。
        ResumeRopeWalkAfterPosingEvent();
    }
}
