using UnityEngine;

/// <summary>
/// ระบบรับ Input การโจมตีของผู้เล่น
/// หน้าที่: ตรวจจับคลิกเมาส์ซ้าย → สั่ง Animator เล่นแอนิเมชันฟันดาบ
/// พร้อมระบบ Cooldown กันกดรัว (ใช้คลาส Cooldown ที่มีอยู่แล้วใน Core/)
/// พร้อมระบบ Lunge (โน้มตัวพุ่งไปข้างหน้า) เพื่อให้การโจมตีดูทรงพลัง
/// พร้อมระบบล็อกการเคลื่อนที่ขณะโจมตี (Playermovement จะอ่านค่า IsAttacking)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerCombat : MonoBehaviour
{
    // ───────────────────────────── Inspector Fields ─────────────────────────────

    [Header("References")]
    [Tooltip("ลาก Animator ของตัวละครมาใส่ (ถ้าไม่ใส่ จะหาจาก GameObject นี้อัตโนมัติ)")]
    [SerializeField] private Animator animator;

    [Tooltip("ลาก Transform ของกล้องมาใส่ (ถ้าไม่ใส่ จะใช้ Camera.main)")]
    [SerializeField] private Transform cameraTransform;

    [Header("Attack Settings")]
    [Tooltip("คูลดาวน์ระหว่างการโจมตีแต่ละครั้ง (กันกดรัวในแต่ละหมัด และระยะเวลาล็อกการเดิน)")]
    [SerializeField] private Cooldown attackCooldown = new Cooldown { duration = 0.5f };

    [Tooltip("คูลดาวน์ใหญ่หลังจากจบคอมโบ (ฟันครบทุกท่า) ถึงจะเริ่มโจมตีชุดใหม่ได้")]
    [SerializeField] private Cooldown comboFinishCooldown = new Cooldown { duration = 1.5f };

    [Tooltip("จำนวนคอมโบสูงสุดที่สามารถกดได้ (ปรับได้ตามจำนวนแอนิเมชันที่มี)")]
    [SerializeField] private int maxCombo = 3;

    [Tooltip("เวลาที่จะจดจำการกดปุ่มล่วงหน้า (วินาที) ช่วยให้กดคอมโบต่อได้ลื่นไหลขึ้นแม้แอนิเมชันเก่ายังไม่จบ")]
    [SerializeField] private float inputBufferTime = 0.2f;

    [Tooltip("เวลาที่จะรีเซ็ตคอมโบกลับไป 0 ถ้าผู้เล่นไม่กดโจมตีต่อ")]
    [SerializeField] private float comboResetTime = 1.0f;

    // สถานะคอมโบ (0 = ไม่ได้ตี, 1 = หมัด1, 2 = หมัด2, ...)
    private int comboStep = 0;
    
    // ตัวจับเวลารีเซ็ตคอมโบ
    private float comboTimer = 0f;

    // ตัวจับเวลาสำหรับ Input Buffer
    private float currentBufferTimer = 0f;

    [Header("Attack Lunge (โน้มตัวพุ่งไปข้างหน้า)")]
    [Tooltip("ระยะทางที่ตัวละครจะพุ่งไปข้างหน้าเมื่อโจมตี (หน่วย: เมตร)")]
    [SerializeField] private float lungeDistance = 1.5f;

    [Tooltip("ระยะเวลาที่ใช้ในการพุ่ง (วินาที) — ยิ่งน้อย = ยิ่งเร็วและดุดัน")]
    [SerializeField] private float lungeDuration = 0.2f;

    [Tooltip("เส้นโค้งความเร็วของการพุ่ง (แนะนำ Ease-Out: เร็วตอนแรก ชะลอตอนท้าย)")]
    [SerializeField] private AnimationCurve lungeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // ───────────────────────────── Animator Parameter Hash ──────────────────────
    // Strict Rule: แปลงชื่อ Parameter เป็น Hash ล่วงหน้า
    // เพื่อหลีกเลี่ยงการสร้าง String ใหม่ทุกเฟรม (ลด GC Allocation)
    private static readonly int ComboStepHash = Animator.StringToHash("ComboStep");

    // ───────────────────────────── Cached Components ────────────────────────────
    private CharacterController controller;

    // ───────────────────────────── Lunge State ──────────────────────────────────
    private bool    isLunging;          // กำลังพุ่งอยู่หรือไม่
    private float   lungeTimer;         // เวลาที่ผ่านไปตั้งแต่เริ่มพุ่ง
    private Vector3 lungeDirection;     // ทิศทางที่พุ่ง (หน้าตัวละคร ณ ตอนกด)

    // ───────────────────────────── Public Property ──────────────────────────────

    /// <summary>
    /// ตัวละครกำลังอยู่ในท่าโจมตีหรือไม่ (Cooldown ยังไม่หมด = กำลังโจมตี)
    /// Playermovement ใช้ค่านี้เพื่อล็อกการเดิน/กระโดดขณะฟันดาบ
    /// </summary>
    public bool IsAttacking => !attackCooldown.IsReady();

    // ════════════════════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // แคช CharacterController (ใช้ตัวเดียวกับ Playermovement)
        controller = GetComponent<CharacterController>();

        // ถ้าลืมลาก Animator ใน Inspector → หาจาก Component ที่ติดอยู่กับ GameObject นี้
        if (animator == null)
            animator = GetComponent<Animator>();

        // ถ้าลืมลาก Camera Transform → ใช้ Camera.main แทน
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        HandleComboReset();
        HandleAttackInput();
        ProcessInputBuffer();
        ProcessLunge();
    }

    /// <summary>
    /// ตัวจับเวลาสำหรับรีเซ็ตคอมโบกลับเป็น 0 เมื่อไม่ได้โจมตีต่อเนื่อง หรือจบคอมโบแล้ว
    /// </summary>
    private void HandleComboReset()
    {
        // ทำงานเฉพาะตอนที่อยู่ในคอมโบ (comboStep > 0)
        if (comboStep > 0)
        {
            // ถ้าฟันครบจนถึงท่าสุดท้ายแล้ว ให้ใช้คูลดาวน์ใหญ่ (comboFinishCooldown) เป็นตัวรีเซ็ตกลับ 0
            if (comboStep >= maxCombo)
            {
                if (comboFinishCooldown.IsReady())
                {
                    ResetCombo();
                }
            }
            else
            {
                // ถ้ายังฟันไม่จบชุด (อยู่ท่า 1 หรือ 2) ให้ใช้เวลา comboResetTime ปกติ
                comboTimer += Time.deltaTime;
                
                // ถ้าหมดเวลาให้กดต่อ (หลุดคอมโบ) -> ลงโทษคูลดาวน์ใหญ่!
                if (comboTimer >= comboResetTime)
                {
                    comboFinishCooldown.StartCooldown();
                    ResetCombo();
                }
            }
        }
    }

    /// <summary>
    /// รีเซ็ตสถานะคอมโบกลับไปที่ 0 (Idle)
    /// </summary>
    private void ResetCombo()
    {
        comboStep = 0;
        comboTimer = 0f;
        animator.SetInteger(ComboStepHash, comboStep);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  Public Methods
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// รับ Camera Transform จากภายนอก (ใช้ตอน PossessionManager สลับร่าง)
    /// </summary>
    public void SetupCamera(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  Private Methods
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ตรวจจับ Input คลิกเมาส์ซ้าย และเก็บคำสั่งลงกระเป๋า (Input Buffer)
    /// </summary>
    private void HandleAttackInput()
    {
        // เมื่อมีการกดปุ่มโจมตี ให้เซ็ตเวลา Buffer ทันทีเพื่อจำคำสั่งล่วงหน้า
        if (Input.GetMouseButtonDown(0))
        {
            currentBufferTimer = inputBufferTime;
        }
    }

    /// <summary>
    /// ประมวลผลคำสั่งใน Buffer ว่าถึงเวลาและเข้าเงื่อนไขที่จะทำการโจมตีได้หรือยัง
    /// </summary>
    private void ProcessInputBuffer()
    {
        // ลดเวลาของ Buffer ลงเรื่อยๆ ตามเวลาจริง
        if (currentBufferTimer > 0)
        {
            currentBufferTimer -= Time.deltaTime;
        }

        // 🛑 ถ้าไม่มีคำสั่งค้างอยู่ในกระเป๋า Buffer ก็เตะออกไปเลยไม่ต้องทำต่อ
        if (currentBufferTimer <= 0) return;

        // 🛑 เช็คว่าเท้าติดพื้นหรือไม่ (ห้ามโจมตีกลางอากาศ)
        if (!controller.isGrounded) return;

        // ⛔ เช็คว่าติดคูลดาวน์ใหญ่หรือไม่? (เกิดจากจบคอมโบ หรือ หลุดคอมโบ) 
        if (!comboFinishCooldown.IsReady()) return;

        // ⏱️ เช็คว่า Cooldown การฟันของหมัดที่แล้วเสร็จหรือยัง?
        if (!attackCooldown.IsReady()) return;

        // ═══ 🟢 ผ่านเงื่อนไขทั้งหมด และมีคำสั่งรออยู่ ➡️ ทำการโจมตี ═══

        // ล้างคำสั่งใน Buffer ทันทีที่ถูกดึงไปใช้ เพื่อป้องกันการดึงไปโจมตีซ้ำซ้อน
        currentBufferTimer = 0f;

        // เพิ่มขั้นคอมโบ (จาก 0 -> 1, 1 -> 2, ไปจนถึง maxCombo)
        comboStep++;

        // สั่ง Animator เปลี่ยนท่าตามขั้นคอมโบ
        animator.SetInteger(ComboStepHash, comboStep);

        if (comboStep >= maxCombo)
        {
            // ถ้าฟันมาถึงท่าสุดท้ายแล้ว ให้เริ่มคูลดาวน์จบคอมโบ (ชุดใหญ่)
            comboFinishCooldown.StartCooldown();
        }
        else
        {
            // ถ้ายืนอยู่ท่ากลางๆ ให้รีเซ็ตตัวจับเวลาคอมโบใหม่
            comboTimer = 0f;
        }

        // เริ่มนับ Cooldown ของการฟันแต่ละหมัดใหม่ทันที เพื่อป้องกันสแปมและล็อกการเดิน
        attackCooldown.StartCooldown();

        // เริ่มระบบพุ่งไปข้างหน้า
        StartLunge();
    }



    /// <summary>
    /// เริ่มการพุ่งไปข้างหน้า — ใช้ทิศทาง forward ของตัวละคร ณ ตอนกดโจมตี
    /// </summary>
    private void StartLunge()
    {
        isLunging      = true;
        lungeTimer     = 0f;
        lungeDirection = transform.forward; // จำทิศทางตอนเริ่มฟัน
    }

    /// <summary>
    /// ประมวลผลการพุ่งทุกเฟรม
    /// ใช้ AnimationCurve ควบคุมความเร็ว → เริ่มเร็ว ชะลอตัวตอนท้าย (Ease-Out)
    /// ทำให้การโจมตีดูทรงพลังและเป็นธรรมชาติ
    /// </summary>
    private void ProcessLunge()
    {
        if (!isLunging) return;

        // นับเวลาที่ผ่านไป
        lungeTimer += Time.deltaTime;

        // คำนวณ progress (0 → 1) ของการพุ่ง
        float progress = Mathf.Clamp01(lungeTimer / lungeDuration);

        // อ่านค่าความเร็วจาก AnimationCurve ณ จุด progress นี้
        // Curve จะกำหนดว่าแต่ละช่วงเวลาเคลื่อนที่เร็วแค่ไหน
        float curveValue = lungeCurve.Evaluate(progress);

        // คำนวณระยะทางที่ต้องเคลื่อนที่ในเฟรมนี้
        // สูตร: (ระยะทางทั้งหมด / เวลาทั้งหมด) × ค่า Curve × deltaTime
        float frameDistance = (lungeDistance / lungeDuration) * curveValue * Time.deltaTime;

        // สั่ง CharacterController เคลื่อนที่ไปข้างหน้า
        controller.Move(lungeDirection * frameDistance);

        // ถ้าเวลาครบ → หยุดพุ่ง
        if (progress >= 1f)
        {
            isLunging = false;
        }
    }
}
