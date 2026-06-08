using UnityEngine;

/// <summary>
/// ระบบสมองส่วนกลาง (Finite State Machine & Blackboard)
/// ทำหน้าที่เก็บอวัยวะทั้งหมด และควบคุมการทำงานของ State ปัจจุบัน
/// </summary>
public class EnemyBrain : MonoBehaviour
{
    [Header("Enemy Data (ศูนย์รวมค่าสเตตัส)")]
    public EnemyData data;

    [Header("Body Parts (อวัยวะของ AI)")]
    [Tooltip("ระบบกล้ามเนื้อ (เคลื่อนที่)")]
    public EnemyMovement movement;

    [Tooltip("ระบบสายตา (ค้นหาเป้าหมาย)")]
    public TargetDetector targetDetector;

    [Tooltip("ระบบต่อสู้ (สั่งโจมตี)")]
    public EnemyCombat combat;

    [Tooltip("ระบบเลือดและความถึก")]
    public HealthSystem health;

    [Tooltip("Animator สำหรับเล่นแอนิเมชันชะงัก/ตาย")]
    public Animator animator;

    [Header("Stagger Settings")]
    [Tooltip("ระยะเวลาชะงัก (วินาที) ก่อนกลับมาทำงานต่อ")]
    public float stunDuration = 0.8f; // Changed to public to allow StunnedState to read it if needed, or pass it directly.

    // --- Cached States (Zero GC) ---
    public IdleState idleState { get; private set; }
    public ChaseState chaseState { get; private set; }
    public AttackState attackState { get; private set; }
    public StunnedState stunnedState { get; private set; }
    public CircleState circleState { get; private set; }
    public CounterAttackState counterAttackState { get; private set; }
    public AlertState alertState { get; private set; }      // [เพิ่ม] สถานะตกใจเห็นผู้เล่น
    public B4AttackState b4AttackState { get; private set; } // [เพิ่ม] สถานะง้างก่อนตี

    // สถานะปัจจุบันที่ AI กำลังเป็นอยู่
    private IEnemyState currentState;
    
    // --- Counter Attack Data ---
    private int currentHitCount = 0;
    private float lastCounterTime = 0f;
    
    // [เพิ่ม] เปิดให้คลาสอื่นเช็คสถานะปัจจุบันได้
    public IEnemyState CurrentState => currentState;

    private void Awake()
    {
        // ส่งข้อมูลจาก EnemyData ไปตั้งค่าให้อวัยวะต่างๆ
        if (data != null)
        {
            if (movement != null) movement.Initialize(data);
            if (combat != null) combat.Initialize(data);
            if (health != null) health.Initialize(data.maxHealth, data.maxPoise);
            
            stunDuration = data.stunDuration;
            if (targetDetector != null)
            {
                targetDetector.maxDetectionDistance = data.detectionRange;
                targetDetector.aimRadius = data.aimRadius;
            }
        }

        // จอง Memory แค่ครั้งเดียวตอนเริ่มเกม (Zero GC Pattern)
        idleState = new IdleState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        stunnedState = new StunnedState(this, stunDuration);
        circleState = new CircleState(this);
        counterAttackState = new CounterAttackState(this);
        alertState = new AlertState(this);       // [เพิ่ม]
        b4AttackState = new B4AttackState(this); // [เพิ่ม]
    }

    private void Start()
    {
        // เมื่อเริ่มเกม ให้ AI เข้าสู่สถานะ Idle โดยใช้ค่าที่จองไว้
        ChangeState(idleState);
    }

    // [เพิ่ม] สมัครรับ Event จาก HealthSystem เมื่อเปิดใช้งาน
    private void OnEnable()
    {
        if (health == null) return;
        health.OnHurt += HandleHurt;
        health.OnDeath += HandleDeath;
        health.OnDamageTaken += HandleDamageTaken; // [เพิ่ม] ติดตามการโดนโจมตีทุกครั้ง
    }

    // [เพิ่ม] ยกเลิกการสมัครเมื่อปิดใช้งาน ป้องกัน Memory Leak

    private void Update()
    {
        // 1. อัปเดตการทำงานของอวัยวะพื้นฐานก่อน (เช่น สายตาต้องทำงานตลอดเวลา)
        // ใช้ตำแหน่งและทิศทางหน้าของตัวเองในการมองหา และส่งตัวเองเป็น excludeRoot เพื่อไม่ให้มองเห็นตัวเอง
        if (targetDetector != null)
            targetDetector.UpdateDetection(transform, transform.forward, transform);

        // 2. สั่งให้ State ปัจจุบันทำงาน
        currentState?.Update();
    }

    /// <summary>
    /// ฟังก์ชันสำหรับเปลี่ยนสถานะของ AI
    /// </summary>
    /// <param name="newState">State ใหม่ที่ต้องการเปลี่ยนไป</param>
    public void ChangeState(IEnemyState newState)
    {
        // ถ้ามี State เดิมอยู่ ให้ออกก่อน (เคลียร์ค่า)
        currentState?.Exit();

        // สลับเป็น State ใหม่
        currentState = newState;

        // เริ่มต้น State ใหม่
        currentState?.Enter();
    }

    // วาด Gizmos เส้นสายตาใน Editor
    private void OnDrawGizmos()
    {
        if (targetDetector != null)
        {
            targetDetector.DrawGizmos(transform.position, transform.forward);
        }
    }

    /// <summary>
    /// Unity Message ทำงานเมื่อ Component นี้ถูกปิดใช้งาน (เช่น ตอนที่ผู้เล่นสิงร่าง)
    /// </summary>
    private void OnDisable()
    {
        // ป้องกัน AI ไถลไปข้างหน้าหรือเดินค้างตอนโดนดึงปลั๊ก
        if (movement != null)
        {
            movement.StopMovement();
        }

        // [Safety Net] คืนบัตรคิวทันทีที่ถูก Disable/Destroy ไม่ว่าจะเกิดจากสาเหตุใดก็ตาม
        // List.Remove() ปลอดภัยถ้า element ไม่อยู่ในลิสต์ (คืนค่า false เฉยๆ ไม่ throw)
        // ดังนั้นการเรียกซ้ำจาก HandleDeath + OnDisable จึงไม่มีผลเสีย
        CombatSlotManager.Instance?.ReleaseSlot(this);

        // [เพิ่ม] ยกเลิกการสมัคร Event
        if (health == null) return;
        health.OnHurt -= HandleHurt;
        health.OnDeath -= HandleDeath;
        health.OnDamageTaken -= HandleDamageTaken;
    }

    // [เพิ่ม] เมื่อถูกโจมตี นับจำนวนครั้งเพื่อสวนกลับ
    private void HandleDamageTaken(DamageInfo info)
    {
        if (data == null) return;

        currentHitCount++;

        // เช็คจำนวนครั้ง และ Cooldown
        if (currentHitCount >= data.hitsToTriggerCounter)
        {
            if (Time.time >= lastCounterTime + data.counterAttackCooldown)
            {
                TriggerCounterAttack();
            }
        }
    }

    // [เพิ่ม] บังคับเริ่มสวนกลับ
    private void TriggerCounterAttack()
    {
        // 1. บังคับยึดคิวโจมตี
        CombatSlotManager.Instance?.ForceRequestSlot(this);

        // 2. รีเซ็ตการนับและเวลา
        currentHitCount = 0;
        lastCounterTime = Time.time;

        // 3. เปลี่ยน State เป็นสวนกลับ (Super Armor จะถูกเปิดใน Enter ของ CounterAttackState ทำให้การโจมตีครั้งนี้ไม่ติดชะงัก)
        ChangeState(counterAttackState);
    }

    // [เพิ่ม] เมื่อเกราะแตก (Poise <= 0) สั่งเล่นแอนิเมชันชะงัก + กระเด็น + หยุด AI ชั่วคราว
    //
    // กฎการขัดจังหวะ (Interrupt Rules):
    //   - AttackState:     ขัดไม่ได้เลย (กำลังตีจริงอยู่)
    //   - B4AttackState:   ขัดได้ (กำลังง้างอยู่) ยกเว้นมี SuperArmor
    //   - CounterAttackState: ขัดไม่ได้ (มี SuperArmor อยู่แล้ว)
    //   - อื่นๆ:           ขัดได้ปกติ
    private void HandleHurt(Vector3 knockbackForce)
    {
        // [กฎ] ถ้ากำลังตีจริงอยู่ (AttackState) → ขัดไม่ได้เด็ดขาด
        if (currentState == attackState) return;

        // [กฎ] ถ้ากำลังง้างอยู่ (B4AttackState) แต่มี SuperArmor → ขัดไม่ได้
        if (currentState == b4AttackState && health != null && health.IsSuperArmorActive) return;

        // [กฎ] ถ้า CounterAttack (มี SuperArmor) → ขัดไม่ได้ (HealthSystem จะไม่ยิง OnHurt อยู่แล้ว แต่ใส่ Safety Net ไว้)
        if (currentState == counterAttackState) return;

        if (animator != null) animator.SetTrigger("Hurt");

        // คืนบัตรคิวทันทีเมื่อโดน Stun (เพื่อให้ศัตรูที่รออยู่ได้รับโอกาสโจมตีแทน)
        CombatSlotManager.Instance?.ReleaseSlot(this);

        // สั่งกระเด็นตามแรงที่ได้รับจาก DamageInfo
        if (movement != null) movement.ApplyKnockback(knockbackForce);

        // เปลี่ยน State เป็น Stunned เพื่อหยุด AI ไม่ให้สั่ง MoveTo ทับ velocity ของ knockback
        ChangeState(stunnedState);
    }

    // [เพิ่ม] เมื่อตาย สั่งเล่นแอนิเมชันตาย
    private void HandleDeath()
    {
        // คืนบัตรคิวทันทีเมื่อตาย (ป้องกันคิวค้าง)
        CombatSlotManager.Instance?.ReleaseSlot(this);

        if (animator != null) animator.SetTrigger("Die");

        // ลบ GameObject ออกจากฉากหลังจากหน่วงเวลาให้แอนิเมชันตายเล่นจบ (เช่น 3 วินาที)
        Destroy(gameObject, 3f);
    }
}
