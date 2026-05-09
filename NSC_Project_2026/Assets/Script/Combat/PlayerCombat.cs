using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField] private int maxComboStep = 3;
    [SerializeField] private float inputBufferTime = 0.2f; // ระยะเวลาในการจำ Input ล่วงหน้า

    [Header("Cooldowns")]
    [SerializeField] private float attackCooldown = 0.3f; // Tier 1: เวลาหน่วงระหว่างการโจมตีแต่ละครั้ง (กันกดรัวเกินไป)
    [SerializeField] private float comboResetTime = 1.0f; // Tier 2: เวลาที่ให้ผู้เล่นตัดสินใจกดตีต่อ (Combo Window)
    [SerializeField] private float comboFinishCooldown = 1.5f; // Tier 3: เวลาหน่วงหลังจากจบคอมโบ (หรือโดนทำโทษจาก Combo Penalty)
    
    [Header("Movement Locks")]
    [SerializeField] private float normalAttackLockTime = 0.3f; // ระยะเวลาล็อกการเดินท่าปกติ
    [SerializeField] private float finishAttackLockTime = 0.6f; // ระยะเวลาล็อกการเดินเฉพาะท่าจบคอมโบ

    [Header("Attack Dash")]
    [SerializeField] private AnimationCurve dashCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)); // กราฟปรับความเร็ว
    [SerializeField] private float dashDistance = 2f; // ระยะทางรวมที่ต้องการให้พุ่ง
    [SerializeField] private float dashDuration = 0.2f; // ระยะเวลาที่ใช้ในการพุ่ง

    [Header("Combat Targeting (Soft Lock-on)")]
    [SerializeField] private TargetDetector targetDetector;
    [SerializeField] private Transform aimOrigin; // จุดกำเนิดในการเล็งเป้า (เช่น Main Camera หรือ ตัวละคร)

    // --- แคช Component จากร่างกายตัวเอง (ดึงครั้งเดียวตอน Awake) ---
    private MeleeHitbox[] currentWeaponHitboxes;
    private Animator currentAnimator;
    private CharacterController currentController;
    private Transform currentBodyTransform;

    // ตัวแปรจับเวลาต่างๆ
    private float currentBufferTimer;
    private float currentAttackCooldownTimer;
    private float currentMovementLockTimer; // แยกการล็อกเดินออกมาเพื่อ Separation of Concerns
    private float comboTimer;
    private float currentComboFinishCooldownTimer;

    // ตัวแปรสำหรับ Dash แบบ Absolute Zero GC
    private bool isDashing;
    private float currentDashTime;

    // สถานะคอมโบปัจจุบัน
    private int currentComboStep;

    // เปิดให้สคริปต์อื่น (เช่น Playermovement) เช็คสถานะการโจมตีได้ (ล็อกการเดินเฉพาะตอนกำลังเหวี่ยงอาวุธ)
    public bool IsAttacking
    {
        get { return currentMovementLockTimer > 0f; } // เช็คจาก Movement Lock Timer แทน
    }

    // Zero GC: สร้าง Hash รอไว้ใช้งานแทนการส่งค่าเป็น String ตรงๆ
    private readonly int hashComboStep = Animator.StringToHash("ComboStep");
    private readonly int hashAttack = Animator.StringToHash("Attack");

    /// <summary>
    /// Awake: ดึง Component จากร่างกายตัวเองครั้งเดียว (ทำงานแม้สคริปต์จะ disabled อยู่)
    /// </summary>
    private void Awake()
    {
        currentAnimator = GetComponentInChildren<Animator>();
        currentWeaponHitboxes = GetComponentsInChildren<MeleeHitbox>();
        currentController = GetComponent<CharacterController>();
        currentBodyTransform = transform;

        Debug.Log($"[PlayerCombat] Awake: {gameObject.name} | Hitbox: {(currentWeaponHitboxes != null ? currentWeaponHitboxes.Length : 0)}");
    }

    /// <summary>
    /// OnEnable: รีเซ็ตสถานะคอมโบทุกครั้งที่ถูกเปิดใช้งาน (ตอนถูกสิงร่าง)
    /// </summary>
    private void OnEnable()
    {
        currentComboStep = 0;
        currentBufferTimer = 0f;
        currentAttackCooldownTimer = 0f;
        currentMovementLockTimer = 0f;
        comboTimer = 0f;
        currentComboFinishCooldownTimer = 0f;
        isDashing = false;

        if (currentAnimator != null)
        {
            currentAnimator.SetInteger(hashComboStep, 0);
        }

    }

    private void Update()
    {
        // Guard Clause: ถ้าร่างกายปัจจุบันว่างเปล่า ให้หยุดทำงาน
        if (currentAnimator == null) return;

        UpdateTimers();
        UpdateCombatTargeting(); // อัปเดตการหาเป้าหมาย
        HandleDash(); // ย้ายลอจิกพุ่งมาไว้ใน Update เพื่อความเป็น Absolute Zero GC
        HandleInput();
        ProcessInputBuffer();
        CheckComboTimeout();
    }

    /// <summary>
    /// อัปเดตระบบค้นหาเป้าหมายในการต่อสู้
    /// </summary>
    private void UpdateCombatTargeting()
    {
        if (targetDetector != null && aimOrigin != null && currentBodyTransform != null)
        {
            // ส่ง currentBodyTransform เป็น excludeRoot เพื่อไม่ให้สิงร่างแล้วเล็งเป้าเข้าตัวเอง
            targetDetector.UpdateDetection(aimOrigin, aimOrigin.forward, currentBodyTransform);
        }
    }

    /// <summary>
    /// ทำหน้าที่ลดค่าเวลาของ Timer ทุกตัวที่มีการทำงานอยู่
    /// </summary>
    private void UpdateTimers()
    {
        if (currentBufferTimer > 0f) currentBufferTimer -= Time.deltaTime;
        if (currentAttackCooldownTimer > 0f) currentAttackCooldownTimer -= Time.deltaTime;
        if (currentMovementLockTimer > 0f) currentMovementLockTimer -= Time.deltaTime;
        if (comboTimer > 0f) comboTimer -= Time.deltaTime;
        if (currentComboFinishCooldownTimer > 0f) currentComboFinishCooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// ประมวลผลการพุ่งตัวตอนโจมตีแบบ Absolute Zero GC
    /// </summary>
    private void HandleDash()
    {
        if (!isDashing) return;

        currentDashTime += Time.deltaTime;

        if (currentDashTime >= dashDuration)
        {
            isDashing = false; // จบการพุ่ง
            return;
        }

        // คำนวณเวลาแบบ Normalized (0 ถึง 1)
        float normalizedTime = currentDashTime / dashDuration;

        // หาค่าความแรง ณ เวลานั้นจาก Curve
        float force = dashCurve.Evaluate(normalizedTime);
        
        // คำนวณระยะการเคลื่อนที่ในเฟรมนี้ โดยอ้างอิงจากหน้าของร่างที่ถูกสิงอยู่
        Vector3 dashMovement = currentBodyTransform.forward * ((dashDistance / dashDuration) * force * Time.deltaTime);

        if (currentController != null)
        {
            currentController.Move(dashMovement);
        }
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
        if (!IsGrounded())
        {
            Debug.Log($"[PlayerCombat] ตีไม่ได้! ตัวละครไม่ได้อยู่บนพื้น (isGrounded = false)");
            return;
        }

        // เมื่อผ่านทุก Guard Clauses หมายความว่าพร้อมโจมตี
        ExecuteAttack();
    }

    /// <summary>
    /// รันคำสั่งการโจมตี, อัปเดต Animator และจัดการระบบคูลดาวน์
    /// </summary>
    private void ExecuteAttack()
    {
        Debug.Log($"[PlayerCombat] ⚔️ โจมตี! ComboStep={currentComboStep} Max={maxComboStep}");

        // ล้างค่า Buffer เป็น 0 ทันทีที่ถูกนำมาใช้งาน
        currentBufferTimer = 0f;

        // Safeguard: หากตีจบคอมโบที่แล้วไปแล้ว ให้รีเซ็ตกลับเป็น 0 เพื่อเริ่มฮิตที่ 1 ใหม่
        // (ป้องกันบัคกดรัวในเฟรมหมดคูลดาวน์พอดี แล้วค่า Step ทะลุเกิน Max ทำให้ไม่มีอนิเมชั่น)
        if (currentComboStep >= maxComboStep)
        {
            currentComboStep = 0;
        }

        // Absolute Zero GC Dash: เริ่มต้นการพุ่งใหม่
        isDashing = true;
        currentDashTime = 0f;

        // หันหน้าและทิศทางไปหา Target ทันทีเมื่อโจมตี
        RotateTowardsTarget();

        // บวกค่าคอมโบไปอีก 1 ขั้น
        currentComboStep++;

        // ส่งค่าไปให้ Animator ทำงานตาม Step ปัจจุบัน
        currentAnimator.SetInteger(hashComboStep, currentComboStep);
        currentAnimator.SetTrigger(hashAttack);

        // ตรวจสอบว่าจบคอมโบ (Max Combo) แล้วหรือยัง
        if (currentComboStep >= maxComboStep)
        {
            // หากตีจนครบ Max Combo แล้ว
            // ไม่รีเซ็ต currentComboStep เป็น 0 ทันที เพื่อให้ Animator มีเวลาเปลี่ยน State
            currentComboFinishCooldownTimer = comboFinishCooldown; // ติดคูลดาวน์ใหญ่ (Tier 3)
            
            // ล็อกการเดินของท่าจบ
            currentMovementLockTimer = finishAttackLockTime; 
            
            // รอให้กลับเป็น 0 อย่างเป็นธรรมชาติผ่าน CheckComboTimeout
            comboTimer = comboFinishCooldown; 
        }
        else
        {
            // หากยังไม่จบคอมโบ (ไปต่อได้)
            currentAttackCooldownTimer = attackCooldown; // เซ็ตคูลดาวน์ระหว่างฮิต (Tier 1)
            currentMovementLockTimer = normalAttackLockTime; // ล็อกการเดินท่าปกติ
            comboTimer = comboResetTime; // ให้เวลาสำหรับกดตีจังหวะต่อไป (Tier 2)
        }
    }

    /// <summary>
    /// ฟังก์ชันสำหรับหันหน้าหาเป้าหมายทันทีแบบ Zero GC
    /// </summary>
    private void RotateTowardsTarget()
    {
        // Guard Clause: ถ้าไม่ได้เปิดระบบหรือไม่มี Target ให้ข้ามไป
        if (targetDetector == null || targetDetector.CurrentTarget == null || currentBodyTransform == null) return;

        Vector3 directionToTarget = targetDetector.CurrentTarget.transform.position - currentBodyTransform.position;
        directionToTarget.y = 0f; // ล็อกแกน Y ไว้เพื่อไม่ให้ตัวละครก้มหรือเงย

        // ถ้าเป้าหมายไม่ได้อยู่จุดเดียวกับตัวละคร (ป้องกัน Error จาก LookRotation)
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            currentBodyTransform.rotation = Quaternion.LookRotation(directionToTarget);
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

        // เช็คว่าเป็นการจบคอมโบแบบสมบูรณ์หรือไม่ (ถึง Max แล้ว)
        bool isMaxComboFinish = (currentComboStep >= maxComboStep);

        // เมื่อปล่อยให้เวลาขาดตอน (หมดเวลา comboTimer)
        // ทำการรีเซ็ตคอมโบเป็น 0
        currentComboStep = 0;
        currentAnimator.SetInteger(hashComboStep, 0);

        // ถ้าไม่ใช่การตีครบ Max Combo แล้วปล่อยเวลาหมด (คือตีพลาด/ขาดตอน) ให้ติดคูลดาวน์ทำโทษ
        if (!isMaxComboFinish)
        {
            currentComboFinishCooldownTimer = comboFinishCooldown; // ติดคูลดาวน์ใหญ่ (Tier 3)
        }
    }

    /// <summary>
    /// ฟังก์ชัน Animation Event สำหรับเรียกใช้จากในหน้าต่าง Animation
    /// </summary>
    public void AE_TriggerWeaponAttack()
    {
        Debug.Log($"[PlayerCombat] เรียก AE_TriggerWeaponAttack! (จำนวน Hitbox: {(currentWeaponHitboxes != null ? currentWeaponHitboxes.Length : 0)})");

        // Guard Clause: ถ้าไม่ได้ใส่ Hitbox ไว้เลย ให้ข้ามไป
        if (currentWeaponHitboxes == null || currentWeaponHitboxes.Length == 0) return;

        // วนลูปสั่งโจมตีทุก Hitbox ที่มีอยู่ใน Array (Zero GC ด้วย for loop)
        for (int i = 0; i < currentWeaponHitboxes.Length; i++)
        {
            if (currentWeaponHitboxes[i] != null) currentWeaponHitboxes[i].PerformAttack();
        }
    }

    /// <summary>
    /// ฟังก์ชันตรวจสอบการติดพื้น (Grounded Check)
    /// </summary>
    /// <returns>สถานะการเหยียบพื้น</returns>
    private bool IsGrounded()
    {
        // ปรับแก้บรรทัดนี้ตามโครงสร้างของโปรเจกต์ของคุณ (เช่นอ้างอิงจากตัวแปร Ground Check ของ PlayerMovement)
        if (currentController != null) return currentController.isGrounded;
        
        return true; // สำรองไว้ในกรณีที่ไม่ได้ใส่ Controller ให้ตีได้ตลอด
    }

    /// <summary>
    /// วาดวงกลม/เส้นสีแดงใน Scene View เพื่อให้เห็นระยะ Target Detector
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (targetDetector != null && aimOrigin != null)
        {
            targetDetector.DrawGizmos(aimOrigin.position, aimOrigin.forward);
        }
        else if (targetDetector != null) // กรณีลืมใส่ aimOrigin ก็ยังให้วาดออกมาจากตัวเอง
        {
            targetDetector.DrawGizmos(transform.position, transform.forward);
        }
    }
}
