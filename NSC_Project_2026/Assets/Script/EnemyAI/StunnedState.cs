using UnityEngine;

/// <summary>
/// สถานะชะงัก (Poise แตก) — AI หยุดทำงานชั่วคราว รอแอนิเมชันชะงักเล่นจบ แล้วกลับไป Idle
/// </summary>
public class StunnedState : IEnemyState
{
    private EnemyBrain brain;
    private float stunDuration;
    private float timer;

    /// <param name="brain">สมองของ AI</param>
    /// <param name="duration">ระยะเวลาชะงัก (วินาที)</param>
    public StunnedState(EnemyBrain brain, float duration)
    {
        this.brain = brain;
        this.stunDuration = duration;
    }

    public void Enter()
    {
        timer = 0f;
        // ไม่ต้องสั่ง StopMovement() ตรงนี้ เพราะ ApplyKnockback() จัดการ velocity ไว้แล้ว
        // ถ้าสั่งหยุดตรงนี้จะไปเขียนทับแรงกระเด็นทันที
    }

    public void Update()
    {
        timer += Time.deltaTime;

        // Guard Clause: ถ้ายังชะงักอยู่ ไม่ต้องทำอะไร
        if (timer < stunDuration) return;

        // หมดเวลาชะงัก กลับไปสถานะ Idle
        brain.ChangeState(brain.idleState);
    }

    public void Exit()
    {
        // ไม่ต้องทำอะไร — State ถัดไปจะจัดการเอง
    }
}
