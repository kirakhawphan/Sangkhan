using UnityEngine;

/// <summary>
/// ระบบสมองส่วนกลาง (Finite State Machine & Blackboard)
/// ทำหน้าที่เก็บอวัยวะทั้งหมด และควบคุมการทำงานของ State ปัจจุบัน
/// </summary>
public class EnemyBrain : MonoBehaviour
{
    [Header("Body Parts (อวัยวะของ AI)")]
    [Tooltip("ระบบกล้ามเนื้อ (เคลื่อนที่)")]
    public EnemyMovement movement;

    [Tooltip("ระบบสายตา (ค้นหาเป้าหมาย)")]
    public TargetDetector targetDetector;

    [Tooltip("ระบบเลือดและความถึก")]
    public HealthSystem health;

    [Tooltip("Animator สำหรับเล่นแอนิเมชันชะงัก/ตาย")]
    public Animator animator;

    [Header("Stagger Settings")]
    [Tooltip("ระยะเวลาชะงัก (วินาที) ก่อนกลับมาทำงานต่อ")]
    [SerializeField] private float stunDuration = 0.8f;

    // สถานะปัจจุบันที่ AI กำลังเป็นอยู่
    private IEnemyState currentState;

    private void Start()
    {
        // เมื่อเริ่มเกม ให้ AI เข้าสู่สถานะ Idle เป็นค่าเริ่มต้น
        // เราส่ง this (ตัวสมองเอง) ไปให้ State เพื่อให้ State ดึงอวัยวะไปใช้ได้
        ChangeState(new IdleState(this));
    }

    // [เพิ่ม] สมัครรับ Event จาก HealthSystem เมื่อเปิดใช้งาน
    private void OnEnable()
    {
        if (health == null) return;
        health.OnHurt += HandleHurt;
        health.OnDeath += HandleDeath;
    }

    // [เพิ่ม] ยกเลิกการสมัครเมื่อปิดใช้งาน ป้องกัน Memory Leak

    private void Update()
    {
        // 1. อัปเดตการทำงานของอวัยวะพื้นฐานก่อน (เช่น สายตาต้องทำงานตลอดเวลา)
        // ใช้ตำแหน่งและทิศทางหน้าของตัวเองในการมองหา และส่งตัวเองเป็น excludeRoot เพื่อไม่ให้มองเห็นตัวเอง
        if (targetDetector != null)
            targetDetector.UpdateDetection(transform, transform.forward, transform);

        // 2. สั่งให้ State ปัจจุบันทำงาน
        currentState?.Update();
    }

    /// <summary>
    /// ฟังก์ชันสำหรับเปลี่ยนสถานะของ AI
    /// </summary>
    /// <param name="newState">State ใหม่ที่ต้องการเปลี่ยนไป</param>
    public void ChangeState(IEnemyState newState)
    {
        // ถ้ามี State เดิมอยู่ ให้ออกก่อน (เคลียร์ค่า)
        currentState?.Exit();

        // สลับเป็น State ใหม่
        currentState = newState;

        // เริ่มต้น State ใหม่
        currentState?.Enter();
    }

    // วาด Gizmos เส้นสายตาใน Editor
    private void OnDrawGizmos()
    {
        if (targetDetector != null)
        {
            targetDetector.DrawGizmos(transform.position, transform.forward);
        }
    }

    /// <summary>
    /// Unity Message ทำงานเมื่อ Component นี้ถูกปิดใช้งาน (เช่น ตอนที่ผู้เล่นสิงร่าง)
    /// </summary>
    private void OnDisable()
    {
        // ป้องกัน AI ไถลไปข้างหน้าหรือเดินค้างตอนโดนดึงปลั๊ก
        if (movement != null)
        {
            movement.StopMovement();
        }

        // [เพิ่ม] ยกเลิกการสมัคร Event
        if (health == null) return;
        health.OnHurt -= HandleHurt;
        health.OnDeath -= HandleDeath;
    }

    // [เพิ่ม] เมื่อเกราะแตก (Poise <= 0) สั่งเล่นแอนิเมชันชะงัก + กระเด็น + หยุด AI ชั่วคราว
    private void HandleHurt(Vector3 knockbackForce)
    {
        if (animator != null) animator.SetTrigger("Hurt");

        // สั่งกระเด็นตามแรงที่ได้รับจาก DamageInfo
        if (movement != null) movement.ApplyKnockback(knockbackForce);

        // เปลี่ยน State เป็น Stunned เพื่อหยุด AI ไม่ให้สั่ง MoveTo ทับ velocity ของ knockback
        ChangeState(new StunnedState(this, stunDuration));
    }

    // [เพิ่ม] เมื่อตาย สั่งเล่นแอนิเมชันตาย
    private void HandleDeath()
    {
        if (animator != null) animator.SetTrigger("Die");
    }
}
