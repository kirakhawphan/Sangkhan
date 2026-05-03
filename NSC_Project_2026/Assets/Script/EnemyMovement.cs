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
        if (agent.isOnNavMesh && !agent.isStopped)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero; // หยุดไถล
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
}
