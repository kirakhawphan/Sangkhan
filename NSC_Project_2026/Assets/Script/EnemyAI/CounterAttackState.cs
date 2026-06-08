using UnityEngine;

/// <summary>
/// สถานะการโจมตีสวนกลับ (Counter Attack)
/// ศัตรูจะมี Super Armor (โดนตีไม่ชะงัก) ระหว่างนี้
/// </summary>
public class CounterAttackState : IEnemyState
{
    private EnemyBrain brain;
    private float exitTime;
    private float AttackAnimationTime => brain.data != null ? brain.data.attackAnimationTime : 1.2f;

    public CounterAttackState(EnemyBrain brain)
    {
        this.brain = brain;
    }

    public void Enter()
    {
        // 1. สั่งเปิด Super Armor เพื่อไม่ให้ชะงัก
        if (brain.health != null)
        {
            brain.health.IsSuperArmorActive = true;
        }

        // 2. สั่งหยุดเดิน + ล็อกการเดิน
        if (brain.movement != null)
        {
            brain.movement.StopMovement();
            brain.movement.LockMovement();
        }

        // 3. สั่งโจมตี (อาจจะเพิ่มท่าสวนกลับแยกต่างหากได้ในอนาคต ตอนนี้ใช้ PerformAttack ปกติไปก่อน)
        if (brain.combat != null)
        {
            brain.combat.PerformAttack();
        }

        exitTime = Time.time + AttackAnimationTime;
    }

    public void Update()
    {
        if (brain.movement != null)
        {
            brain.movement.StopMovement();
        }

        // หันหน้าหาเป้าหมายตลอดเวลา (สามารถปรับเปลี่ยนได้ตามดีไซน์)
        Transform target = brain.targetDetector != null ? brain.targetDetector.CurrentTarget?.transform : null;
        if (target != null && brain.movement != null)
        {
            brain.movement.FaceTarget(target.position);
        }

        if (Time.time >= exitTime)
        {
            brain.ChangeState(brain.circleState);
        }
    }

    public void Exit()
    {
        // 1. คืนบัตรคิวให้ CombatSlotManager
        CombatSlotManager.Instance?.ReleaseSlot(brain);

        // 2. ปลดล็อกการเดิน
        if (brain.movement != null)
        {
            brain.movement.UnlockMovement();
        }

        // 3. ปิด Super Armor
        if (brain.health != null)
        {
            brain.health.IsSuperArmorActive = false;
        }

        if (brain.combat != null)
        {
            brain.combat.ResetAttack();
        }
    }
}
