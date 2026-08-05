using UnityEngine;
using System.Linq;
using System.Collections;
using UnityEngine.AI;
//using System;

public class KaijuAI : MonoBehaviour
{
    [Header("Move / Rotate")]
    public float moveSpeed = 1.0f;
    public float rotateSpeed = 5.0f;

    [Header("Attack")]
    public float attackDistance = 4.5f;
    public float attackCooldown = 2f;

    [Header("Effects")]
    public AudioSource asiato;
    public ParticleSystem footSmokeLeft;
    public ParticleSystem footSmokeRight;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    [SerializeField] Transform player;//プレイヤーの座標を検知
    [Header("ロープクールタイム")]
    [SerializeField] public float RopeCoolTime = 3.0f;
    private float CoolTime=0.0f;
    [SerializeField] float spawnRadius = 30f; // ランダム範囲
    Animator anim;
    NavMeshAgent agent;

    GameObject targetBuilding;
    BreakBuilding targetBreakScript;
    

    float attackTimer = 0f;
    bool isAttacking = false;
    int patrolIndex = 0;
    bool RopeMove = false;
    float time = 0;
    bool timeFlag = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        Vector3 randomPos = GetRandomNavMeshPosition(transform.position, spawnRadius);

        agent.Warp(randomPos);

        agent.speed = moveSpeed;
        agent.angularSpeed = rotateSpeed * 100f;
        agent.acceleration = 20f;
        agent.stoppingDistance = attackDistance * 0.65f;

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("[KaijuAI] NavMesh 上にいません。", this);
        }

        FindNewBuilding();
    }

    void Update()
    {
       

        if (agent.speed == 0)
        {
            time += Time.deltaTime;
            timeFlag = true;
        }
        else
        {
            time = 0;
            timeFlag = false;
        }
        if (timeFlag == true)
        {
            if (time >= 6)//speedが6秒間停止したままだったらPointResetする
            {
                PointReset();
                Debug.Log("Dinoが6秒間動いていません、目標をリセットします");
                time = 0;
            }
        }
        

        if (CoolTime<=0)
        {
            this.gameObject.layer = 0;
        }
        if(RopeMove==true)
        {
            Vector3 forward = transform.forward * 0.1f;
            agent.velocity = forward;
        }
        
        CoolTime-= Time.deltaTime * 1;

        transform.Rotate(0, 50 * Time.deltaTime, 0);

        anim.speed = 0.7f;

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (targetBuilding == null)
        {
            FindNewBuilding();
            if (targetBuilding == null)
            {
                Patrol();
                return;
            }
        }

        Vector3 toTarget = targetBuilding.transform.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // 攻撃開始
        if (!isAttacking && dist <= attackDistance && attackTimer <= 0f)
        {
            StartAttack();
            return;
        }

        // 攻撃中は停止
        if (isAttacking)
        {
            agent.ResetPath();
            anim.SetFloat("Speed", 0f);
            return;
        }

        // 通常移動
        if (agent.isOnNavMesh)
            agent.SetDestination(targetBuilding.transform.position);

        float v = agent.velocity.magnitude;
        if (v < 0.05f) v = 0.05f;

        anim.SetFloat("Speed", v);

    }
    Vector3 GetRandomNavMeshPosition(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position; // NavMesh上の安全な位置
        }

        return center; // 見つからなかったら元の位置
    }



    void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackCooldown;

        if (agent.isOnNavMesh)
            agent.ResetPath();

        anim.SetTrigger("Attack");
        anim.SetFloat("Speed", 0f);
    }

    void FindNewBuilding()
    {
        var buildings = GameObject.FindGameObjectsWithTag("Building")
            .Where(b => b != null && b.activeInHierarchy)
            .ToArray();

        if (buildings.Length == 0)
        {
            targetBuilding = null;
            targetBreakScript = null;
            return;
        }

        //プレイヤーの近くの建物をセットする
        targetBuilding = buildings
            .OrderBy(b => Vector3.Distance(player.position, b.transform.position))
            .FirstOrDefault();

        targetBreakScript = targetBuilding?.GetComponent<BreakBuilding>();
       
    }

    public void PunchHit()
    {
        StartCoroutine(PunchHitRoutine());
    }

    IEnumerator PunchHitRoutine()
    {
        if (agent.isOnNavMesh)
            agent.ResetPath();

        if (targetBreakScript != null)
        {
            targetBreakScript.Break(transform.position);

            if (targetBuilding != null)
                targetBuilding.SetActive(false);

            targetBuilding = null;
            targetBreakScript = null;

            attackTimer = 0f;
            FindNewBuilding();
        }

        //硬直中は待機状態にする
        isAttacking = true;
        anim.SetFloat("Speed", 0f);

        float stunTime = 1.2f;
        yield return new WaitForSeconds(stunTime);

        isAttacking = false;

        //移動させる
        if (targetBuilding != null && agent.isOnNavMesh)
            agent.SetDestination(targetBuilding.transform.position);

       
    }



    public void FootStepLeft()
    {
        if (footSmokeLeft != null) footSmokeLeft.Play();
        if (asiato != null) asiato.Play();
    }

    public void FootStepRight()
    {
        if (footSmokeRight != null) footSmokeRight.Play();
        if (asiato != null) asiato.Play();
    }

    void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            anim.SetFloat("Speed", 0f);
            if (agent.isOnNavMesh)
                agent.ResetPath();
            return;
        }

        Transform target = patrolPoints[patrolIndex];

        if (agent.isOnNavMesh)
            agent.SetDestination(target.position);

        float v = agent.velocity.magnitude;
        if (v < 0.05f) v = 0.05f;

        anim.SetFloat("Speed", v);

        if (Vector3.Distance(transform.position, target.position) < 2.0f)
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
    }



    private void OnCollisionEnter(Collision collision)
    {
        // ロープに当たった時
        if (collision.gameObject.tag == "RopeParts")
        {
            if(CoolTime<=0)
            {
               
                //自動回転をオフにする
                //agent.updateRotation = false;
                //agent.updatePosition = true;
                //くぐりながら歩く
                //RopeMove = true;
                //Apply Root Motionを無効にする
                anim.applyRootMotion = false;

                // ロープをくぐるアニメーション発生
                anim.SetTrigger("UnderTheRope");
                agent.speed = 0.0f;
            }
            
            
        }
    }

    //アニメーションイベントから呼び出される
    public void ExitUnderTheRope()
    {
        //Apply Root Motionを有効にする
        anim.applyRootMotion = true;
        // 歩くようにする
        // agent.speed = moveSpeed;
        PointReset();
        //クールタイムを設定
        CoolTime = RopeCoolTime;
        this.gameObject.layer = 6;
        //自動回転をオンにする
        //agent.updateRotation = true;
        //agent.updatePosition = false;
    }

    public void PointReset()
    {
        agent.updateRotation = true;
        agent.speed = moveSpeed;
        agent.angularSpeed = rotateSpeed * 100f;
        agent.acceleration = 20f;
        agent.stoppingDistance = attackDistance * 0.65f;
        // NavMesh 上なら目的地を再設定
        if (agent.isOnNavMesh && targetBuilding != null)
        {
            agent.SetDestination(targetBuilding.transform.position);
        }
    }
    

}
