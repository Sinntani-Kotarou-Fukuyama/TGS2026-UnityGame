using UnityEngine;

public class PosingDino : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] GameObject Hahen;//破片のプレハブ用
  
    private GameObject spawnedHahen;
    public AudioSource asiato;
    BreakBuilding bill;
    void Awake()
    {
       
        anim = GetComponentInChildren<Animator>();
            Debug.Log("Animator 自動取得：" + anim);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Invoke(nameof(DinoStop), 4.0f);
        Invoke(nameof(DinoAttack), 4.5f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void DinoStop()
    {
        anim.SetTrigger("Stop");
    }
    public void DinoAttack()
    {
        anim.SetTrigger("Attack");
    }
    public void Asiato()
    {
        asiato.Play();
    }
    public void BreakBill()
    {
        GameObject targetObject = GameObject.Find("破片飛ばすビル");
        if (targetObject != null)
        {
           bill = targetObject.GetComponent<BreakBuilding>();
        }
        bill.Break(transform.position);//ビルを壊す
        Debug.Log("ビル破壊");
        Vector3 pos = new Vector3(-21.144f, 2.25f, -4.15f);//+が左、-が右
        spawnedHahen=Instantiate(Hahen,pos,Quaternion.identity);
        Invoke(nameof(DestroyDino), 3.0f);
    }
   
    void DestroyDino()
    {
        Destroy(gameObject);
    }

}
