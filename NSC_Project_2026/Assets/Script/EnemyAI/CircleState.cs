using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// สถานะดูเชิง/เดินวน (CircleState)
/// ศัตรูจะรักษาระยะห่างจากผู้เล่น เดิน Strafe ไปด้านข้าง
/// และพยายามขอบัตรคิวจาก CombatSlotManager ซ้ำๆ จนกว่าจะได้รับอนุญาตให้โจมตี
/// </summary>
public class CircleState : IEnemyState
{
    private EnemyBrain brain;

    [Header("Circling Settings")]
    private float circleRadius = 4f;       // รัศมีที่รักษาจากผู้เล่น
    private float strafeSpeed = 2f;        // ความเร็วตอนเดิน Strafe
    private float retryInterval = 1.2f;   // ขอบัตรคิวใหม่ทุก X วินาที
    private float tooCloseDistance = 2.5f; // ถ้าเข้าใกล้กว่านี้ ให้ถอยออก

    private float retryTimer;
    private float strafeDirection = 1f;        // 1 = ขวา, -1 = ซ้าย
    private float strafeChangeTimer;
    private float strafeChangeDuration = 2.5f; // เปลี่ยนทิศทาง Strafe ทุก X วินาที

    // --- Off-Screen Delay ---
    // นับเวลาที่ศัตรูอยู่ในมุมกล้อง (on-screen) ต่อเนื่อง
    // ต้องอยู่ในจอครบ offScreenSlotDelay วินาที จึงจะขอ Slot ได้
    private float onScreenAccumulator = 0f;

    public CircleState(EnemyBrain brain)
    {
        this.brain = brain;
    }

    public void Enter()
    {
        // ลดความเร็วลงเพื่อเดินวน (ไม่วิ่ง)
        if (brain.movement != null) brain.movement.SetSpeed(strafeSpeed);

        // รีเซ็ต Timer และสุ่มทิศเดิน Strafe เริ่มต้น
        retryTimer = retryInterval * 0.5f; // หน่วงครึ่งรอบแรกก่อนขอ
        strafeChangeTimer = 0f;
        strafeDirection = Random.value > 0.5f ? 1f : -1f;

        // รีเซ็ต Off-Screen Delay ทุกครั้งที่เข้า State นี้
        // ทำให้ศัตรูต้องรออยู่ในจออีกครั้งก่อนขอ Slot ใหม่
        onScreenAccumulator = 0f;
    }

    public void Update()
    {
        // 1. ถ้าสูญหายเป้าหมาย กลับ Idle
        if (brain.targetDetector == null || brain.targetDetector.CurrentTarget == null)
        {
            brain.ChangeState(brain.idleState);
            return;
        }

        Transform target = brain.targetDetector.CurrentTarget.transform;
        Vector3 targetPos = target.position;
        float distToTarget = Vector3.Distance(brain.movement != null
            ? brain.movement.transform.position
            : brain.transform.position, targetPos);

        // [เพิ่ม] ปรับความเร็วตามระยะทาง ถ้าอยู่ไกลให้สับขาวิ่ง (6f) ถ้าถึงระยะเดินวนให้เดินช้าลง (strafeSpeed)
        if (distToTarget > circleRadius * 1.5f)
        {
            if (brain.movement != null) brain.movement.SetSpeed(6f); // วิ่ง
        }
        else
        {
            if (brain.movement != null) brain.movement.SetSpeed(strafeSpeed); // เดินวน
        }

        // 2. ถอยออกถ้าผู้เล่นเข้ามาใกล้เกินไป
        if (distToTarget < tooCloseDistance)
        {
            Vector3 retreatDir = (brain.transform.position - targetPos).normalized;
            Vector3 retreatPos = brain.transform.position + retreatDir * 2f;
            if (brain.movement != null) brain.movement.MoveTo(retreatPos);
            brain.movement?.FaceTarget(targetPos);
            return;
        }

        // 3. เดิน Strafe รอบๆ ผู้เล่น (เปลี่ยนทิศทางเป็นระยะ)
        strafeChangeTimer += Time.deltaTime;
        if (strafeChangeTimer >= strafeChangeDuration)
        {
            strafeChangeTimer = 0f;
            strafeDirection *= -1f; // สลับทิศ Strafe
        }

        // คำนวณตำแหน่ง Strafe โดยใช้ Vector ขวาของศัตรู
        Vector3 toTarget = (targetPos - brain.transform.position).normalized;
        Vector3 strafeVec = Vector3.Cross(toTarget, Vector3.up) * strafeDirection;

        // เดินไปด้านข้างพร้อมรักษาระยะห่างจากผู้เล่น
        Vector3 desiredPos = targetPos + (-toTarget * circleRadius) + (strafeVec * 1.5f);
        if (brain.movement != null) brain.movement.MoveTo(desiredPos);
        brain.movement?.FaceTarget(targetPos);

        // 4. อัปเดต On-Screen Accumulator สำหรับ Off-Screen Delay
        bool isOnScreen = CombatSlotManager.IsEnemyOnScreen(brain.transform.position);
        if (isOnScreen)
        {
            // ศัตรูอยู่ในจอ: สะสมเวลา (จำกัดไม่เกิน delay เพื่อ memory)
            float delay = CombatSlotManager.Instance != null
                ? CombatSlotManager.Instance.offScreenSlotDelay
                : 1.5f;
            onScreenAccumulator = Mathf.Min(onScreenAccumulator + Time.deltaTime, delay + 1f);
        }
        else
        {
            // ศัตรูออกนอกจอ: รีเซ็ต timer ทันที (ต้องรออยู่ในจอใหม่อีกครั้ง)
            onScreenAccumulator = 0f;
        }

        // 4. พยายามขอบัตรคิวทุก retryInterval วินาที
        retryTimer += Time.deltaTime;
        if (retryTimer >= retryInterval)
        {
            retryTimer = 0f;
            TryClaimSlot();
        }
    }

    public void Exit()
    {
        if (brain.movement != null) brain.movement.StopMovement();
    }

    /// <summary>
    /// ขอบัตรคิวจาก CombatSlotManager
    /// ถ้าได้รับอนุญาต ให้กลับไป ChaseState เพื่อวิ่งเข้าหาเป้าหมายและโจมตี
    /// จะไม่ขอถ้าศัตรูยังอยู่นอกมุมกล้องไม่นานพอ (Off-Screen Delay)
    /// </summary>
    private void TryClaimSlot()
    {
        if (CombatSlotManager.Instance == null) return;

        // เช็คแค่ว่าพร้อมตีหรือยัง (คูลดาวน์โจมตีเสร็จไหม)
        if (brain.combat == null || !brain.combat.CanAttack()) return;

        // [Off-Screen Delay] ถ้าศัตรูอยู่นอกหน้าจอ และยังสะสมเวลาไม่ครบ → ยังขอ Slot ไม่ได้
        float requiredDelay = CombatSlotManager.Instance.offScreenSlotDelay;
        if (requiredDelay > 0f && onScreenAccumulator < requiredDelay)
        {
            // ยังรออยู่ในจอไม่นานพอ ข้ามรอบนี้ไป
            return;
        }

        // ขอบัตรคิว
        if (CombatSlotManager.Instance.RequestSlot(brain))
        {
            // ได้คิวแล้ว กลับไปวิ่งไล่เพื่อเข้าโจมตี (ChaseState จะเช็คระยะแล้วเข้า Attack อีกที)
            brain.ChangeState(brain.chaseState);
        }
    }
}
