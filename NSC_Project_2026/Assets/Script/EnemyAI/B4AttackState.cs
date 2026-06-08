using UnityEngine;

/// <summary>
/// สถานะง้างก่อนตี (Before Attack / Windup / Telegraph)
/// ศัตรูจะหยุดนิ่งเล่นท่าง้างอาวุธ เพื่อให้ผู้เล่นเห็นสัญญาณเตือนก่อนโดนตี
/// 
/// กฎการขัดจังหวะ (Interrupt Rules):
///   - สถานะนี้ **ขัดได้** (โดนตีจนเกราะแตก → StunnedState)
///   - ยกเว้นถ้ามี **SuperArmor** เปิดอยู่ (เช่น Counter Attack) จะขัดไม่ได้
///   - เทียบกับ AttackState ซึ่งขัดไม่ได้เลยไม่ว่ากรณีใด
/// </summary>
public class B4AttackState : IEnemyState
{
    private EnemyBrain brain;
    private float timer;
    private float B4AttackDelay => brain.data != null ? brain.data.b4AttackDelay : 0.5f;

    public B4AttackState(EnemyBrain brain)
    {
        this.brain = brain;
    }

    public void Enter()
    {
        Debug.Log("Enter B4Attack State (Windup)");
        timer = 0f;

        // หยุดเดิน + ล็อกการเคลื่อนที่
        if (brain.movement != null)
        {
            brain.movement.StopMovement();
            brain.movement.LockMovement();
        }

        // เล่นแอนิเมชันง้างอาวุธ (ถ้ามี Trigger "Windup" ใน Animator)
        if (brain.animator != null) brain.animator.SetTrigger("Windup");
    }

    public void Update()
    {
        // บังคับหยุดเดินทุกเฟรม (Safety Net)
        if (brain.movement != null)
        {
            brain.movement.StopMovement();
        }

        // หันหน้าหาผู้เล่นตลอดเวลาที่ง้าง เพื่อล็อกทิศทางการโจมตี
        Transform target = brain.targetDetector != null ? brain.targetDetector.CurrentTarget?.transform : null;
        if (target != null && brain.movement != null)
        {
            brain.movement.FaceTarget(target.position);
        }

        timer += Time.deltaTime;

        // เมื่อง้างครบเวลา → เข้าสู่ AttackState ตีจริง
        if (timer >= B4AttackDelay)
        {
            brain.ChangeState(brain.attackState);
        }
    }

    public void Exit()
    {
        Debug.Log("Exit B4Attack State (Windup)");

        // ปลดล็อกการเดิน (AttackState จะล็อกใหม่อีกครั้งเอง)
        if (brain.movement != null)
        {
            brain.movement.UnlockMovement();
        }
    }
}
