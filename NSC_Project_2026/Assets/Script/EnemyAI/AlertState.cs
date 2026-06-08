using UnityEngine;

/// <summary>
/// สถานะตกใจ/เตรียมตัว (Alert)
/// เมื่อ AI เห็นผู้เล่นครั้งแรก จะหยุดนิ่งชั่วครู่ (หันหน้าจ้อง) ก่อนเริ่มไล่ล่า
/// ทำให้ผู้เล่นรู้ตัวว่า "ศัตรูเห็นแล้ว" และ AI ดูเป็นธรรมชาติมากขึ้น
/// </summary>
public class AlertState : IEnemyState
{
    private EnemyBrain brain;
    private float timer;
    private float AlertDuration => brain.data != null ? brain.data.alertDuration : 0.8f;

    public AlertState(EnemyBrain brain)
    {
        this.brain = brain;
    }

    public void Enter()
    {
        Debug.Log("Enter Alert State");
        timer = 0f;

        // หยุดเดินทันที
        if (brain.movement != null)
        {
            brain.movement.StopMovement();
        }

        // เล่นแอนิเมชันตกใจ (ถ้ามี Trigger "Alert" ใน Animator)
        if (brain.animator != null) brain.animator.SetTrigger("Alert");
    }

    public void Update()
    {
        // หันหน้าหาเป้าหมายตลอดเวลาที่ตกใจ
        if (brain.targetDetector != null && brain.targetDetector.CurrentTarget != null)
        {
            Vector3 targetPos = brain.targetDetector.CurrentTarget.transform.position;
            if (brain.movement != null) brain.movement.FaceTarget(targetPos);
        }

        timer += Time.deltaTime;

        // ถ้ายังไม่หมดเวลา Alert ให้รอต่อ
        if (timer < AlertDuration) return;

        // ถ้าเป้าหมายหายไปแล้ว กลับไป Idle
        if (brain.targetDetector == null || brain.targetDetector.CurrentTarget == null)
        {
            brain.ChangeState(brain.idleState);
            return;
        }

        // หมดเวลา Alert → เริ่มไล่ล่า
        // ขอบัตรคิวตั้งแต่ตอนนี้เลย
        if (CombatSlotManager.Instance != null && CombatSlotManager.Instance.RequestSlot(brain))
        {
            // คิวว่าง → วิ่งไล่เพื่อโจมตี
            brain.ChangeState(brain.chaseState);
        }
        else
        {
            // คิวเต็ม → ไปเดินวนดูเชิง
            brain.ChangeState(brain.circleState);
        }
    }

    public void Exit()
    {
        Debug.Log("Exit Alert State");
    }
}
