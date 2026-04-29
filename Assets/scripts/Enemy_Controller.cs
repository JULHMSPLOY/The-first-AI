using UnityEngine;
using UnityEngine.AI;

public class Enemy_Controller : MonoBehaviour
{
    [Header("Detection & Targeting")]
    public string targetTag = "Player"; // ตัว Player ตั้งเป็น Enemy / ตัว Enemy ตั้งเป็น Player
    public Transform target; 
    public SphereCollider detectionCollider;
    public LayerMask obstacleMask;

    [Header("Health System")]
    public float maxHealth = 100f;
    public float currentHealth;
    [Range(0.1f, 5f)]
    public float damageMultiplier = 1f; // Player ตั้ง 0.5 (ถึก) / Enemy ตั้ง 2.0 (เลือดลดไว)

    [Header("Combat Settings")]
    public float attackRange = 1.5f; 
    public float attackCooldown = 1.5f;
    public float damagePerHit = 10f;

    [Header("Movement Settings")]
    public Transform[] waypoints;

    private NavMeshAgent agent;
    private Animator anim;
    private int currentWaypointIndex = 0;
    private float lastAttackTime;

    public enum AIState { Patrolling, Chasing, Attacking, Dead } 
    public AIState currentState; 

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        currentHealth = maxHealth;
        currentState = AIState.Patrolling;
    }

    void Update()
    {
        if (currentState == AIState.Dead) return;

        // --- จุดสำคัญ: ถ้าเป้าหมายหายไป (ถูก Destroy) ให้กลับไป Patrolling ทันที ---
        if ((currentState == AIState.Chasing || currentState == AIState.Attacking) && target == null)
        {
            currentState = AIState.Patrolling;
        }

        // ควบคุมการหยุดเดิน: ให้หยุดเฉพาะตอนกำลังโจมตีเท่านั้น
        if (agent != null)
        {
            agent.isStopped = (currentState == AIState.Attacking);
        }

        switch (currentState)
        {
            case AIState.Patrolling:
                Patrol();
                break;
            case AIState.Chasing:
                Chase();
                break;
            case AIState.Attacking:
                Attack(); 
                break;
        }

        if (anim != null)
        {
            // ส่งความเร็วไปที่ Animator Parameter "Speed"
            anim.SetFloat("Speed", agent.velocity.magnitude, 0.1f, Time.deltaTime);
        }
    }

    void Patrol()
    {
        if (waypoints.Length == 0) return;

        agent.SetDestination(waypoints[currentWaypointIndex].position);

        // เมื่อเดินถึงจุดพัก ให้เปลี่ยนไปจุดถัดไป
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    void Chase()
    {
        if (target == null) return;
        
        float distance = Vector3.Distance(transform.position, target.position);
        agent.SetDestination(target.position);

        if (distance <= attackRange)
        {
            currentState = AIState.Attacking;
        }
    }

    void Attack()
    {
        if (target == null) return;

        // หันหน้าหาศัตรูให้ตรง
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        // จับเวลา Cooldown การโจมตี
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            if (anim != null) anim.SetTrigger("Attack"); // เรียก Parameter "Attack" ใน Animator
            lastAttackTime = Time.time;

            // ส่งความเสียหายไปยังสคริปต์ของเป้าหมาย
            Player_Controller targetScript = target.GetComponent<Player_Controller>();
            if (targetScript != null)
            {
                targetScript.TakeDamage(damagePerHit);
            }
        }

        // ถ้าเป้าหมายวิ่งหนีออกนอกระยะ ให้กลับไปไล่ตาม
        if (Vector3.Distance(transform.position, target.position) > attackRange + 0.5f)
        {
            currentState = AIState.Chasing;
        }
    }

    public void TakeDamage(float amount)
    {
        if (currentState == AIState.Dead) return;

        float finalDamage = amount * damageMultiplier; 
        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        currentState = AIState.Dead;
        if (agent != null) agent.isStopped = true;
        
        // ลบทิ้งทันทีโดยไม่ใช้อนิเมชันท่าตาย
        Destroy(gameObject, 0.1f); 
    }

    // --- ระบบ Sensor ตรวจจับด้วย Trigger ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            target = other.transform;
            currentState = AIState.Chasing;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            target = null;
            currentState = AIState.Patrolling;
        }
    }
}