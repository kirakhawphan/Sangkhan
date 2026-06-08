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
            brain.movement.SetSpeed(brain.data != null ? brain.data.idleSpeed : 3.5f);
        }
    }

    public void Update()
    {
        // ตรวจสอบว่าระบบสายตาเห็นเป้าหมาย (PossessableEntity) หรือไม่
        if (brain.targetDetector.CurrentTarget != null)
        {
            // [แก้ไข] เจอผู้เล่น → ตกใจก่อน (AlertState) แทนที่จะวิ่งไล่ทันที
            brain.ChangeState(brain.alertState);
        }
    }

    public void Exit()
    {
        Debug.Log("Exit Idle State");
    }
}
