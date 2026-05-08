using UnityEngine;

/// <summary>
/// สถานะการโจมตี: เมื่อ AI เข้าประชิดเป้าหมาย จะหยุดเดินและเล่นท่าโจมตี
/// </summary>
public class AttackState : IEnemyState
{
    private EnemyBrain brain;
    private float exitTime;
    private const float ATTACK_ANIMATION_TIME = 1.2f; // ระยะเวลาจำลองของท่าตี (ควรสัมพันธ์กับแอนิเมชัน)

    public AttackState(EnemyBrain brain)
    {
        this.brain = brain;
    }

    public void Enter()
    {
        // 1. สั่งหยุดเดินก่อนตี
        if (brain.movement != null)
        {
            brain.movement.StopMovement();
        }

        // 2. สั่งโจมตี
        if (brain.combat != null)
        {
            brain.combat.PerformAttack();
        }

        // ตั้งเวลาที่จะออกจาก State นี้ (เพื่อให้แอนิเมชันเล่นจบก่อน)
        exitTime = Time.time + ATTACK_ANIMATION_TIME;
    }

    public void Update()
    {
        // หันหน้าหาผู้เล่นตลอดเวลาที่ตี เพื่อความแม่นยำ
        Transform target = brain.targetDetector != null ? brain.targetDetector.CurrentTarget?.transform : null;
        if (target != null && brain.movement != null)
        {
            brain.movement.FaceTarget(target.position);
        }

        // เมื่อเวลาผ่านไปจนจบแอนิเมชัน ให้กลับไปไล่ล่าต่อ
        if (Time.time >= exitTime)
        {
            brain.ChangeState(new ChaseState(brain));
        }
    }

    public void Exit()
    {
        // เมื่อออกจากสถานะตี ให้รีเซ็ตค่าคอมโบเป็น 0 เพื่อให้ Animator กลับไปท่า Idle/Run
        if (brain.combat != null)
        {
            brain.combat.ResetAttack();
        }
    }
}
