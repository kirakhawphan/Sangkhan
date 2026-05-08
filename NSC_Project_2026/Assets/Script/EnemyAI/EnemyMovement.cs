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
    [SerializeField] private float rotationSpeed = 5f;

    // --- Cached Components (Optimization) ---
    private NavMeshAgent agent;

    // [เพิ่ม] เก็บแรงกระเด็นที่ยังค้างอยู่ เพื่อใช้ใน Update
    private Vector3 knockbackVelocity;

    // แคชพารามิเตอร์ Animator เป็น Hash (int) แทนการใช้ String (Zero String Allocation)
    private readonly int speedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        // ดึง NavMeshAgent บน GameObject เดียวกัน
        agent = GetComponent<NavMeshAgent>();

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
            // ดึงความเร็วแนวนอนจาก agent.velocity (ตัดแกน Y ทิ้ง ป้องกันบั๊กแอนิเมชันเดินเวลากระโดด/ตกจากที่สูง)
            Vector3 horizontalVelocity = new Vector3(agent.velocity.x, 0f, agent.velocity.z);
            float currentSpeed = horizontalVelocity.magnitude;

            // อัปเดตพารามิเตอร์ Speed ให้ Animator (ใช้ damp time 0.1f ให้เปลี่ยนผ่านสมูท)
            animator.SetFloat(speedHash, currentSpeed, 0.1f, Time.deltaTime);
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
        if (agent.isOnNavMesh)
        {
            // ปลดล็อกการหยุดเดิน หากโดนสั่ง StopMovement ไว้
            if (agent.isStopped) agent.isStopped = false;
            
            agent.SetDestination(destination);
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
        Debug.Log($"[Knockback] ApplyKnockback Called! Force Received: {force}");
        
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) 
        {
            Debug.LogWarning("[Knockback] Failed! Agent is null, disabled, or not on NavMesh.");
            return;
        }

        // บังคับให้ขยับได้แม้อยู่ในสถานะ Stopped
        agent.isStopped = false;
        agent.ResetPath();

        // เก็บแรงไว้ แล้วให้ Update จัดการเลื่อนตัวทุกเฟรม
        knockbackVelocity = force;
        Debug.Log($"[Knockback] Velocity Set to: {knockbackVelocity}");
    }
}
