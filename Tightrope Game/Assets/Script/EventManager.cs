using System.Data;
using System.Runtime.CompilerServices;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    [SerializeField] private GameObject player;//プレイヤーの座標取得用
    private int ramdomCount;//ランダムな数字
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(ramdomCount==1)//カウントがこの数字だったらこのイベントを呼ぶ
        {
            Destroy(this.gameObject);//イベントが開始したらフラグ用オブジェクトを消す
        }

        if (ramdomCount == 2)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 3)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 4)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 5)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 6)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 7)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 8)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 9)
        {
            Destroy(this.gameObject);
        }

        if (ramdomCount == 10)
        {
            Destroy(this.gameObject);
        }
    }
    void OnTriggerEnter(Collider t)
    {
        if (t.gameObject.CompareTag("Player"))
        {
            Debug.Log("イベント開始");
            RandomEvent();
        }
            
       
    }
    void RandomEvent()
    {
        // 0から11までの乱数
        ramdomCount = Random.Range(0, 11);
        Debug.Log($"乱数（整数）: {ramdomCount}");
    }
   
}
