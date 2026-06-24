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
        GameObject targetObject = GameObject.Find("破壊可能青黄ビル (3)");
        if (targetObject != null)
        {
           bill = targetObject.GetComponent<BreakBuilding>();
        }
        bill.Break(transform.position);//ビルを壊す
        Vector3 pos = new Vector3(-21.144f, 2.354f, -4.095f);
        spawnedHahen=Instantiate(Hahen,pos,Quaternion.identity);
       
    }
    
}
