using UnityEngine;

/// <summary>
/// สถานะยืนเฝ้ายาม (หยุดนิ่ง รอจนกว่าจะเจอเป้าหมาย)
/// </summary>
public class IdleState : IEnemyState
{
    private EnemyBrain brain;

    // รับสมองเข้ามาตอนสร้าง State นี้ เพื่อให้ดึงอวัยวะไปใช้ได้
    public IdleState(EnemyBrain brain)
    {
        this.brain = brain;
    }

    public void Enter()
    {
        Debug.Log("Enter Idle State");
        // สั่งกล้ามเนื้อให้หยุดเดินทันที (ถ้ามี)
        if (brain.movement != null)
        {
            brain.movement.StopMovement();
            brain.movement.SetSpeed(3.5f);
        }
    }

    public void Update()
    {
        // ตรวจสอบว่าระบบสายตาเห็นเป้าหมาย (PossessableEntity) หรือไม่
        if (brain.targetDetector.CurrentTarget != null)
        {
            // [แก้ไข] ขอบัตรคิวตั้งแต่เห็นหน้าเลย
            if (CombatSlotManager.Instance != null && CombatSlotManager.Instance.RequestSlot(brain))
            {
                // คิวว่าง -> วิ่งไล่เพื่อโจมตี
                brain.ChangeState(brain.chaseState);
            }
            else
            {
                // คิวเต็ม -> ไปเดินวนดูเชิงที่ระยะ 4 เมตรทันที (ไม่วิ่งเข้าไปใกล้ๆ แล้วค่อยถอย)
                brain.ChangeState(brain.circleState);
            }
        }
    }

    public void Exit()
    {
        Debug.Log("Exit Idle State");
    }
}
