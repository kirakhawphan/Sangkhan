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
        // เพิ่มความเร็วในการวิ่งไล่ล่า (ถ้ามี)
        if (brain.movement != null) brain.movement.SetSpeed(6f);
    }

    public void Update()
    {
        // 1. เช็คก่อนว่าเป้าหมายยังอยู่ในสายตาไหม
        if (brain.targetDetector == null || brain.targetDetector.CurrentTarget == null)
        {
            // ถ้าเป้าหมายหลุดระยะ หรือถูกลบออกไป ให้กลับไปสถานะ Idle
            brain.ChangeState(new IdleState(brain));
            return;
        }

        // 2. ถ้ายังมีเป้าหมายอยู่ ก็ดึงพิกัดล่าสุดของเป้าหมาย
        Vector3 targetPosition = brain.targetDetector.CurrentTarget.transform.position;

        // 3. สั่งกล้ามเนื้อให้วิ่งไปที่พิกัดนั้น (ถ้ามี)
        if (brain.movement != null) brain.movement.MoveTo(targetPosition);

        // 4. [เพิ่ม] เช็คระยะโจมตี: ถ้าเข้าใกล้พอ และคูลดาวน์เสร็จแล้ว ให้เปลี่ยนไปตีทันที
        if (brain.combat != null && brain.combat.CanAttack())
        {
            float distance = Vector3.Distance(brain.transform.position, targetPosition);
            float range = brain.combat.GetAttackRange();
            
            // [แก้ไข] จะต่อยก็ต่อเมื่อ: เข้าขอบระยะต่อยแล้ว และ NavMeshAgent เดินเข้ามาถึงระยะหยุด (Stopping Distance) แล้วจริงๆ
            bool isCloseEnough = (distance <= range);
            bool isNearStoppingPoint = (brain.movement.GetRemainingDistance() <= brain.movement.GetStoppingDistance() + 0.1f);

            if (isCloseEnough && isNearStoppingPoint)
            {
                brain.ChangeState(new AttackState(brain));
            }
        }
    }

    public void Exit()
    {
        Debug.Log("Exit Chase State");
        // สั่งหยุดเดินเมื่อเลิกไล่ล่า (ถ้ามี)
        if (brain.movement != null) brain.movement.StopMovement();
    }
}
