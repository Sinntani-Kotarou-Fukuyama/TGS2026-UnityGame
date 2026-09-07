using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] RopeWalkManager ropeWalkManager;
    [SerializeField] TutorialUIController tutorialUI;
    [SerializeField] TrolleyWall wall;//プレイヤーの移動処理
    [SerializeField] Earthquake earthquake;

    Joycon jc;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (ropeWalkManager != null) { ropeWalkManager.StopPlayer(); }
        Invoke(nameof(StartPlayer), 3f);

        //ジョイコンが接続されているか確かめる
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
        if (jc == null)
        {
           tutorialUI.nextHintText.text = "SPACEで次へ";
        }
        else
        {
           tutorialUI.nextHintText.text = "Xボタンで次へ";

        }
    }
    void Update()
    {
        // 毎フレーム Joy-Con の接続状態を更新する
        var joycons = JoyconManager.Instance.j;

        if (joycons != null && joycons.Count > 0)
        {
            jc = joycons[0];
        }
        else
        {
            jc = null;
        }
    }
    void StartPlayer()
    {
        if (ropeWalkManager != null && ropeWalkManager.IsPlayerStop())
        {
            ropeWalkManager.MovePlayer();

            
            bool isJoyCon = ControlSelectionSession.SelectedControlType == GameplayControlType.JoyCon;

            if (isJoyCon)
            {
                
                var joyWait = FindFirstObjectByType<JoyConStartWaitController>();
                if (joyWait != null)
                {
                    joyWait.OnJoyConReady = () =>
                    {
                        Invoke(nameof(Tutorial_1), 1f);
                    };
                }
            }
            else
            {
                
                Invoke(nameof(Tutorial_1), 1f);
            }

            Debug.Log("動きました");
        }
        var eq = FindFirstObjectByType<Earthquake>();
if (eq != null)
{
    eq.OnEarthquakeStart = () =>
    {
        Invoke(nameof(Tutorial_2), 1f);
    };
}

    }
    void Tutorial_1()//1つ目に出てくるチュートリアル
    {
        //ジョイコンが無かったら
        if(jc==null)
        {
            //キーボードへ
            tutorialUI.ShowLines(new string[]
            {
                "ロープの上でバランスを取ろう！",
                "キーボード←　→で左右に傾けてみよう！",
                "マウスを左右に動かしてもできるよ！",
                "傾けすぎると落ちちゃうから気を付けて！",
                "実際にやってみよう！"
            },
            () => {
                //UIが消えた瞬間に呼ばれる
                wall.IsStop(false);
            }
            );
        }
        else//ジョイコンがあったら
        {
            //ジョイコンへ
           tutorialUI.ShowLines(new string[]
           {
              "ロープの上でバランスを取ろう！",
              "棒を左右に倒してみよう。",
              "傾けすぎると落ちちゃうから気を付けて！",
              "実際にやってみよう！"
           },
           () => {
               //UIが消えた瞬間に呼ばれる
               wall.IsStop(false);
           }
           );
        }

        wall.IsStop(true);
    }
   void Tutorial_2()//2つ目に出てくるチュートリアル
    {
        //ジョイコンが無かったら
        if (jc == null)
        {
            //キーボードへ
            tutorialUI.ShowLines(new string[]
            {
                "ロープを渡っていると異常が発生するよ！",
                "異常中は何らかの邪魔が入るよ！",
                "ロープから落とされないように気を付けて！"
                
            },
            () => {
                //UIが消えた瞬間に呼ばれる
                wall.IsStop(false);
                Invoke(nameof(Tutorial_3), 5f);
            }
            );
        }
        else//ジョイコンがあったら
        {
            //ジョイコンへ
            tutorialUI.ShowLines(new string[]
            {
          "",
          
            },
            () => {
                //UIが消えた瞬間に呼ばれる
                wall.IsStop(false);
                Invoke(nameof(Tutorial_3), 5f);
            }
            );
        }

        wall.IsStop(true);
    }

    void Tutorial_3()//3つ目に出てくるチュートリアル
    {
        //ジョイコンが無かったら
        if (jc == null)
        {
            //キーボードへ
            tutorialUI.ShowLines(new string[]
            {
                "これでチュートリアルは終わり！",
                "↑や↓を使う異常もあるから気を付けて！",
                "本番では怪獣が町で大暴れ！",
                "頑張ってゴールを目指そう！",
                "クリックするとゲームスタート！"
            },
            () => {
                //UIが消えた瞬間に呼ばれる
                wall.IsStop(false);
                SceneManager.LoadScene("SampleScene");
            }
            );
        }
        else//ジョイコンがあったら
        {
            //ジョイコンへ
            tutorialUI.ShowLines(new string[]
            {
          "",
         
            },
            () => {
                //UIが消えた瞬間に呼ばれる
                wall.IsStop(false);
                SceneManager.LoadScene("SampleScene");
            }
            );
        }

        wall.IsStop(true);
    }
}
