using UnityEngine;

/// <summary>
/// สถานะวิ่งไล่ล่าเป้าหมาย
/// </summary>
public class ChaseState : IEnemyState
{
    private EnemyBrain brain;

    public ChaseState(EnemyBrain brain)
    {
        this.brain = brain;
    }

    public void Enter()
    {
        Debug.Log("Enter Chase State");
        // เพิ่มความเร็วในการวิ่งไล่ล่า
        brain.movement.SetSpeed(6f);
    }

    public void Update()
    {
        // 1. เช็คก่อนว่าเป้าหมายยังอยู่ในสายตาไหม
        if (brain.targetDetector.CurrentTarget == null)
        {
            // ถ้าเป้าหมายหลุดระยะ หรือถูกลบออกไป ให้กลับไปสถานะ Idle
            brain.ChangeState(new IdleState(brain));
            return;
        }

        // 2. ถ้ายังมีเป้าหมายอยู่ ก็ดึงพิกัดล่าสุดของเป้าหมาย
        Vector3 targetPosition = brain.targetDetector.CurrentTarget.transform.position;

        // 3. สั่งกล้ามเนื้อให้วิ่งไปที่พิกัดนั้น
        brain.movement.MoveTo(targetPosition);
    }

    public void Exit()
    {
        Debug.Log("Exit Chase State");
        // สั่งหยุดเดินเมื่อเลิกไล่ล่า
        brain.movement.StopMovement();
    }
}
