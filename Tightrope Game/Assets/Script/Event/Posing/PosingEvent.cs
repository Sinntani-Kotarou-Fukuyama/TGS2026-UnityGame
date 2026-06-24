using UnityEngine;

public class PosingEvent : MonoBehaviour
{
    
    [SerializeField] GameObject PosingDino;//イベント怪獣のプレハブ用
    [SerializeField] Transform Dino;//イベント中は怪獣を見えないところへ移動させる
    [SerializeField] Transform Bill;//イベントで破壊されるビルの座標
    [SerializeField] CameraSwhich cam;//カメラを切り替えれるように
    [SerializeField] float speed = 1f;//怪獣の移動速度
    [SerializeField] GameObject Text;
    bool DinoStoping = false;//怪獣を動かなくするフラグ
    bool Flag = false;
    bool DinoIdouflag = true;//怪獣移動フラグ
    private GameObject spawneDino;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Flag == true)//イベント開始フラグ
        {
           PoseEvent();
           Flag = false;
        }
        if (DinoStoping == true)//怪獣を動かなくするフラグ
        {
            Dino.transform.position = new Vector3(50.0f, 0.0f, 50.0f);
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
    }
    public void EventFlag() //イベントマネージャーで呼び出す
    {
        Flag = true;
    }

    void PoseEvent()
    {
        DinoStoping = true;//怪獣を固定
        Invoke(nameof(DinoEvent), 0.1f);//0.1秒語にポーズイベントを始める
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
    }
}
