using System;
using System.Data;
using System.Runtime.CompilerServices;
using UnityEngine;

/*
  ◆説明
　プレイヤーにあたるとイベントを起こすEventFlagの生成をして、イベントの発生を行います。
　◆使い方
　① イベントの処理を新しいスクリプトに書きます。
　➁ ヒエラルキーウィンドウのEventManagerの下階層に新しいゲームオブジェクトを作ります。
　③ ②で作ったゲームオブジェクトに①で作ったスクリプトをアタッチします。
　④ 「// イベントを起こすためのゲームオブジェクト」の部分に①で作ったクラスの変数を宣言します。
　⑤ インスペクターウィンドウから②で作ったゲームオブジェクトと④を紐付けます
　⑥ 必要であれば、EventTypeに新しい状態を追加してください
　⑦ RandomEvent()のイベントの種類の判定の部分に④から処理を呼び出すコードを記述してください
 */
public class EventManager : MonoBehaviour
{
    [SerializeField] private GameObject player;//プレイヤーの座標取得用
    private int ramdomCount; // ランダムな数字
    [SerializeField]EventFlag eventFlag;  // イベントフラグのprefab

    // イベントを起こすためのゲームオブジェクト
    [SerializeField,Header("地震イベントを起こすゲームオブジェクト")] private Earthquake e_earthquake;

    //イベントの種類
    public enum EventType
    {
        Earthquake,   // 地震
    }


    private void Start()
    {
        EventFlag e;

        // イベントフラグの生成、設定
        e = Instantiate(eventFlag);
        e.SetManager(this);

    }

    // イベントフラグがプレイヤーにあたると呼び出されるメソッド
    public void RandomEvent()
    {
        // 0からEventTypeの要素数までの乱数
        ramdomCount = UnityEngine.Random.Range((int)0, (int)Enum.GetValues(typeof(EventType)).Length);
        Debug.Log($"乱数（整数）: {ramdomCount}");

        // イベントの種類を判定する
        switch (ramdomCount)
        {
            // 地震イベントの場合
            case (int)EventType.Earthquake:
                e_earthquake.StartEvent(); // 地震を起こす
                break;
            default:
                Debug.Log("イベントが起こりました。");
                break;
        }

    }

}
