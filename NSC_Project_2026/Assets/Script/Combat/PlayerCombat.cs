using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat References")]
    [SerializeField] private MeleeHitbox[] weaponHitboxes;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller; // ใช้สำหรับ Grounded Check (ปรับเปลี่ยนได้ตาม PlayerMovement ของคุณ)

    [Header("Combo Settings")]
    [SerializeField] private int maxComboStep = 3;
    [SerializeField] private float inputBufferTime = 0.2f; // ระยะเวลาในการจำ Input ล่วงหน้า

    [Header("Cooldowns")]
    [SerializeField] private float attackCooldown = 0.3f; // Tier 1: เวลาหน่วงระหว่างการโจมตีแต่ละครั้ง (กันกดรัวเกินไป)
    [SerializeField] private float comboResetTime = 1.0f; // Tier 2: เวลาที่ให้ผู้เล่นตัดสินใจกดตีต่อ (Combo Window)
    [SerializeField] private float comboFinishCooldown = 1.5f; // Tier 3: เวลาหน่วงหลังจากจบคอมโบ (หรือโดนทำโทษจาก Combo Penalty)

    // ตัวแปรจับเวลาต่างๆ
    private float currentBufferTimer;
    private float currentAttackCooldownTimer;
    private float comboTimer;
    private float currentComboFinishCooldownTimer;

    // สถานะคอมโบปัจจุบัน
    private int currentComboStep;

    // เปิดให้สคริปต์อื่น (เช่น Playermovement) เช็คสถานะการโจมตีได้ (ล็อกการเดินเฉพาะตอนกำลังเหวี่ยงอาวุธ)
    public bool IsAttacking
    {
        get { return currentAttackCooldownTimer > 0f; }
    }

    // Zero GC: สร้าง Hash รอไว้ใช้งานแทนการส่งค่าเป็น String ตรงๆ
    private readonly int hashComboStep = Animator.StringToHash("ComboStep");
    private readonly int hashAttack = Animator.StringToHash("Attack");

    private void Update()
    {
        UpdateTimers();
        HandleInput();
        ProcessInputBuffer();
        CheckComboTimeout();
    }

    /// <summary>
    /// ทำหน้าที่ลดค่าเวลาของ Timer ทุกตัวที่มีการทำงานอยู่
    /// </summary>
    private void UpdateTimers()
    {
        if (currentBufferTimer > 0f) currentBufferTimer -= Time.deltaTime;
        if (currentAttackCooldownTimer > 0f) currentAttackCooldownTimer -= Time.deltaTime;
        if (comboTimer > 0f) comboTimer -= Time.deltaTime;
        if (currentComboFinishCooldownTimer > 0f) currentComboFinishCooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// รับ Input จากผู้เล่น หน้าที่คือเติมเวลาให้ Buffer อย่างเดียว (ไม่ประมวลผลการตีตรงนี้)
    /// </summary>
    private void HandleInput()
    {
        // Guard Clause: หากไม่ได้กดคลิกซ้าย ไม่ต้องทำอะไร
        if (!Input.GetMouseButtonDown(0)) return;

        // เติมเวลา Input Buffer ไว้ล่วงหน้า
        currentBufferTimer = inputBufferTime;
    }

    /// <summary>
    /// นำ Input ที่จำไว้ใน Buffer มาประมวลผลเมื่อเงื่อนไขทุกอย่างพร้อม
    /// </summary>
    private void ProcessInputBuffer()
    {
        // Guard Clause 1: หากไม่มี Input ค้างอยู่ใน Buffer ให้ข้ามไป
        if (currentBufferTimer <= 0f) return;

        // Guard Clause 2: หากติด Cooldown ระหว่างฮิต (Tier 1) ให้ข้ามไป
        if (currentAttackCooldownTimer > 0f) return;

        // Guard Clause 3: หากติด Cooldown จบคอมโบ / ดีเลย์การลงโทษ (Tier 3) ให้ข้ามไป
        if (currentComboFinishCooldownTimer > 0f) return;

        // Guard Clause 4: ต้องอยู่บนพื้นเท่านั้น (Grounded Check)
        if (!IsGrounded()) return;

        // เมื่อผ่านทุก Guard Clauses หมายความว่าพร้อมโจมตี
        ExecuteAttack();
    }

    /// <summary>
    /// รันคำสั่งการโจมตี, อัปเดต Animator และจัดการระบบคูลดาวน์
    /// </summary>
    private void ExecuteAttack()
    {
        // ล้างค่า Buffer เป็น 0 ทันทีที่ถูกนำมาใช้งาน
        currentBufferTimer = 0f;

        // บวกค่าคอมโบไปอีก 1 ขั้น
        currentComboStep++;

        // ส่งค่าไปให้ Animator ทำงานตาม Step ปัจจุบัน
        animator.SetInteger(hashComboStep, currentComboStep);
        animator.SetTrigger(hashAttack);

        // ตรวจสอบว่าจบคอมโบ (Max Combo) แล้วหรือยัง
        if (currentComboStep >= maxComboStep)
        {
            // หากตีจนครบ Max Combo แล้ว
            currentComboStep = 0; // รีเซ็ตสถานะกลับเป็น 0
            animator.SetInteger(hashComboStep, 0);

            currentComboFinishCooldownTimer = comboFinishCooldown; // ติดคูลดาวน์ใหญ่ (Tier 3)
            comboTimer = 0f; // เคลียร์เวลา Combo Reset ทิ้ง
        }
        else
        {
            // หากยังไม่จบคอมโบ (ไปต่อได้)
            currentAttackCooldownTimer = attackCooldown; // เซ็ตคูลดาวน์ระหว่างฮิต (Tier 1)
            comboTimer = comboResetTime; // ให้เวลาสำหรับกดตีจังหวะต่อไป (Tier 2)
        }
    }

    /// <summary>
    /// ตรวจสอบว่าปล่อยให้เวลาขาดตอนจนหลุดระยะเวลาคอมโบ (Combo Window) หรือไม่
    /// </summary>
    private void CheckComboTimeout()
    {
        // Guard Clause 1: หากไม่ได้อยู่ในสถานะคอมโบ (Step เป็น 0 อยู่แล้ว) ไม่ต้องเช็ค
        if (currentComboStep == 0) return;

        // Guard Clause 2: หากเวลายังไม่หมด (ผู้เล่นยังกดตีต่อได้อยู่) ไม่ต้องเช็ค
        if (comboTimer > 0f) return;

        // เมื่อปล่อยให้เวลาขาดตอน (หมดเวลา comboTimer)
        // ทำการรีเซ็ตคอมโบเป็น 0 และติดคูลดาวน์ทำโทษ (Combo Penalty)
        currentComboStep = 0;
        animator.SetInteger(hashComboStep, 0);
        currentComboFinishCooldownTimer = comboFinishCooldown; // ติดคูลดาวน์ใหญ่ (Tier 3)
    }

    /// <summary>
    /// ฟังก์ชัน Animation Event สำหรับเรียกใช้จากในหน้าต่าง Animation
    /// </summary>
    public void AE_TriggerWeaponAttack()
    {
        // Guard Clause: ถ้าไม่ได้ใส่ Hitbox ไว้เลย ให้ข้ามไป
        if (weaponHitboxes == null || weaponHitboxes.Length == 0) return;

        // วนลูปสั่งโจมตีทุก Hitbox ที่มีอยู่ใน Array (Zero GC ด้วย for loop)
        for (int i = 0; i < weaponHitboxes.Length; i++)
        {
            if (weaponHitboxes[i] != null) weaponHitboxes[i].PerformAttack();
        }
    }

    /// <summary>
    /// ฟังก์ชันตรวจสอบการติดพื้น (Grounded Check)
    /// </summary>
    /// <returns>สถานะการเหยียบพื้น</returns>
    private bool IsGrounded()
    {
        // ปรับแก้บรรทัดนี้ตามโครงสร้างของโปรเจกต์ของคุณ (เช่นอ้างอิงจากตัวแปร Ground Check ของ PlayerMovement)
        if (controller != null) return controller.isGrounded;
        
        return true; // สำรองไว้ในกรณีที่ไม่ได้ใส่ Controller ให้ตีได้ตลอด
    }
}
