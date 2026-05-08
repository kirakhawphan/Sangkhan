using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// คลาสควบคุมการเคลื่อนที่ของ Enemy (Locomotion / Action)
/// ออกแบบเป็น "กล้ามเนื้อ" รับคำสั่งเดิน/หยุด/หมุน จาก Brain หรือ State Machine เท่านั้น
/// ห้ามใส่ Logic ตัดสินใจ (AI Decision) ลงในนี้เด็ดขาด
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ตัวจัดการแอนิเมชัน (ถ้าไม่ใส่จะค้นหาใน Child อัตโนมัติ)")]
    [SerializeField] private Animator animator;

    [Header("Settings")]
    [Tooltip("ความเร็วในการหันหน้าเข้าหาเป้าหมาย (ใช้ในฟังก์ชัน FaceTarget)")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float stoppingDistance = 2.0f; // ระยะที่จะให้ AI หยุดยืนห่างจากผู้เล่น
    [SerializeField] private float rotationSpeed = 10f;     // ความเร็วในการหันหน้าไปหาผู้เล่น

    [Header("Distance Maintenance")]
    [SerializeField] private bool keepDistance = false;    // [เพิ่ม] สวิตช์เปิด-ปิดระบบเดินหนี
    [SerializeField] private float retreatDistance = 1.5f;  // [เพิ่ม] ถ้าน้อยกว่าระยะนี้ จะเริ่มเดินหนี
    [SerializeField] private float retreatMultiplier = 2f;  // [เพิ่ม] ตัวคูณระยะถอย (ยิ่งเยอะยิ่งถอยไกล)

    // --- Cached Components (Optimization) ---
    private NavMeshAgent agent;

    // [เพิ่ม] เก็บแรงกระเด็นที่ยังค้างอยู่ เพื่อใช้ใน Update
    private Vector3 knockbackVelocity;

    // แคชพารามิเตอร์ Animator เป็น Hash (int) แทนการใช้ String (Zero String Allocation)
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isWalkingHash = Animator.StringToHash("isWalking");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");

    private void Awake()
    {
        // ดึง NavMeshAgent บน GameObject เดียวกัน
        agent = GetComponent<NavMeshAgent>();

        // [เพิ่ม] ตั้งค่าระยะหยุดให้กับ Agent
        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.speed = moveSpeed;
        }

        // ถ้าลืมใส่ Animator ใน Inspector ให้หาอัตโนมัติ
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        // ถ้าถูกผู้เล่นสิงร่าง (NavMeshAgent ถูกปิด) ให้หยุดซิงค์แอนิเมชัน เพื่อไม่ให้ไปแย่งทำงานกับสคริปต์ Playermovement
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        // ==========================================
        // [เพิ่ม] Knockback: เลื่อนตัวละครทุกเฟรมด้วย agent.Move()
        // ==========================================
        if (knockbackVelocity.sqrMagnitude > 0.1f)
        {
            Debug.Log($"[Knockback] Moving agent by {knockbackVelocity * Time.deltaTime}");
            // บังคับให้ NavMeshAgent อัปเดตตำแหน่ง
            agent.Move(knockbackVelocity * Time.deltaTime);

            // ค่อยๆ ชะลอตัว (เปลี่ยนจาก 8 เป็น 5 เพื่อให้กระเด็นไกลขึ้นและนานขึ้น)
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 5f * Time.deltaTime);
        }

        // ==========================================
        // Animation Sync: ซิงค์แอนิเมชันตามความเร็วจริง
        // ==========================================
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            // ดึงความเร็วแนวนอนจาก agent.velocity
            Vector3 horizontalVelocity = new Vector3(agent.velocity.x, 0f, agent.velocity.z);
            float currentSpeed = horizontalVelocity.magnitude;

            // อัปเดตพารามิเตอร์ Speed ให้ Animator
            animator.SetFloat(speedHash, currentSpeed, 0.1f, Time.deltaTime);
            
            // [เพิ่ม] อัปเดตสถานะการเดินและการติดพื้น
            animator.SetBool(isWalkingHash, currentSpeed > 0.1f);
            animator.SetBool(isGroundedHash, true); // AI ถือว่าติดพื้นเสมอ
        }
    }

    // ==========================================
    // Public API (สำหรับให้ AI Brain / State Machine สั่งการ)
    // ==========================================

    /// <summary>
    /// สั่งให้เอเจนต์เดินไปที่พิกัดเป้าหมาย (Destination)
    /// </summary>
    /// <param name="destination">พิกัดบนโลก (World Space) ที่ต้องการให้เดินไป</param>
    public void MoveTo(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        float distanceToTarget = Vector3.Distance(transform.position, destination);

        // [เพิ่ม] ระบบเดินหนี (Kiting Logic)
        if (keepDistance && distanceToTarget < retreatDistance)
        {
            Debug.Log($"[Kiting] Player too close! Distance: {distanceToTarget:F2} < {retreatDistance}. Fleeing...");
            
            // คำนวณทิศทางหนี (จากเป้าหมายมาหาตัวเอง)
            Vector3 fleeDirection = (transform.position - destination).normalized;
            Vector3 fleePosition = transform.position + (fleeDirection * retreatMultiplier);

            // ตรวจสอบพิกัดบน NavMesh ว่าถอยไปได้ไหม
            if (NavMesh.SamplePosition(fleePosition, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                agent.stoppingDistance = 0; // [สำคัญ] ปิดระยะหยุดชั่วคราวเพื่อให้มันยอมถอย
                agent.SetDestination(hit.position);
                FaceTarget(destination);
                return;
            }
            else
            {
                Debug.LogWarning("[Kiting] No space to flee behind!");
            }
        }

        // --- ลอจิกเดิม: เดินเข้าหาปกติ ---
        if (agent.isStopped) agent.isStopped = false;
        
        agent.stoppingDistance = stoppingDistance; // [คืนค่า] กลับไปใช้ระยะหยุดที่ตั้งไว้ใน Inspector
        agent.SetDestination(destination);

        if (distanceToTarget <= agent.stoppingDistance)
        {
            FaceTarget(destination);
        }
    }

    /// <summary>
    /// สั่งให้เอเจนต์หยุดเดินทันที (ลบเป้าหมายการเดินทิ้ง)
    /// </summary>
    public void StopMovement()
    {
        if (agent != null && agent.isOnNavMesh && !agent.isStopped)
        {
            agent.isStopped = true;
            agent.ResetPath();
            
            // เคลียร์ความเร็วเฉพาะตอนที่ไม่ได้กระเด็นอยู่เท่านั้น
            // ป้องกันไม่ให้ State.Exit() มาสั่งเบรกหัวทิ่มตอนกำลังกระเด็น
            if (knockbackVelocity.sqrMagnitude < 0.1f)
            {
                agent.velocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// ปรับระดับความเร็วในการเดิน/วิ่ง (ตัวอย่าง: เดินช้า = 2, วิ่งไล่ = 6)
    /// </summary>
    /// <param name="newSpeed">ความเร็วปลายทางที่ต้องการ</param>
    public void SetSpeed(float newSpeed)
    {
        if (agent != null)
        {
            agent.speed = newSpeed;
        }
    }

    /// <summary>
    /// สั่งให้เอเจนต์หมุนตัวหันหน้าเข้าหาเป้าหมายอย่างนุ่มนวล
    /// (ใช้ตอนที่หยุดเดินแล้ว เช่น ยืนจ้องหน้าผู้เล่น หรือเตรียมโจมตี)
    /// </summary>
    /// <param name="targetPosition">พิกัดของสิ่งที่ต้องการจะหันไปมอง</param>
    public void FaceTarget(Vector3 targetPosition)
    {
        // 1. หา direction ว่าเป้าหมายอยู่ทิศไหน
        Vector3 direction = (targetPosition - transform.position).normalized;

        // 2. สนใจแค่แนวแกน Y (ซ้าย/ขวา) ไม่ให้ศัตรูก้มหัวทิ่มดินหรือแหงนมองฟ้า
        direction.y = 0f;

        // 3. ป้องกันบั๊กกรณีที่เป้าหมายอยู่ที่พิกัดเดียวกันเป๊ะๆ (Direction = 0)
        if (direction != Vector3.zero)
        {
            // คำนวณ Rotation ที่ควรมองไป
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Slerp หมุนตัวอย่างนุ่มนวล
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// [เพิ่ม] สั่งให้ศัตรูกระเด็นตามแรงที่ได้รับ (ใช้ตอน Poise แตก)
    /// ใช้ agent.Move() ทุกเฟรมใน Update เพื่อให้มองเห็นการกระเด็นจริงๆ
    /// </summary>
    /// <param name="force">แรงกระเด็น (ทิศทาง x ความแรง)</param>
    public void ApplyKnockback(Vector3 force)
    {
        // ... (โค้ดเดิม) ...
        knockbackVelocity = force;
    }

    // [เพิ่ม] วาดวงกลมในหน้า Scene เพื่อให้เห็นระยะหยุดและระยะถอย
    private void OnDrawGizmosSelected()
    {
        // วงกลมสีเขียว = ระยะหยุด (Stopping Distance)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);

        // วงกลมสีแดง = ระยะถอยหนี (Retreat Distance)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}
