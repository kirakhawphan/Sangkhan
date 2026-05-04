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

    // สถานะปัจจุบันที่ AI กำลังเป็นอยู่
    private IEnemyState currentState;

    private void Start()
    {
        // เมื่อเริ่มเกม ให้ AI เข้าสู่สถานะ Idle เป็นค่าเริ่มต้น
        // เราส่ง this (ตัวสมองเอง) ไปให้ State เพื่อให้ State ดึงอวัยวะไปใช้ได้
        ChangeState(new IdleState(this));
    }

    private void Update()
    {
        // 1. อัปเดตการทำงานของอวัยวะพื้นฐานก่อน (เช่น สายตาต้องทำงานตลอดเวลา)
        // ใช้ตำแหน่งและทิศทางหน้าของตัวเองในการมองหา และส่งตัวเองเป็น excludeRoot เพื่อไม่ให้มองเห็นตัวเอง
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
    }
}
