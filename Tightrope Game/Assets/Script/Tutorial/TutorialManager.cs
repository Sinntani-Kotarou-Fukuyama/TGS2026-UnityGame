using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] RopeWalkManager ropeWalkManager;
    [SerializeField] TutorialUIController tutorialUI;
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
    }

    void StartPlayer()
    {
        if (ropeWalkManager != null)
        {
            if (ropeWalkManager.IsPlayerStop())
            {
                ropeWalkManager.MovePlayer();
                Invoke(nameof(StopPlayer), 1f);
                Debug.Log("動きました");
            }
        }
    }
    void StopPlayer()
    {
        //ジョイコンが無かったら
        if(jc==null)
        {
            //キーボードへ
            tutorialUI.ShowLines(new string[]
            {
                "ロープの上でバランスを取ろう！",
                "キーボード←　→で左右に倒してみよう。",
                "倒れすぎると落ちるよ！"
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
          "倒れすぎると落ちるよ！"
           }
           );
        }
           
        if (ropeWalkManager != null) { ropeWalkManager.StopPlayer(); }
    }
    void Update()
    {
        
    }
}
