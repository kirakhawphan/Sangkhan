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
    [Tooltip("คูลดาวน์ระหว่างการโจมตีแต่ละครั้ง")]
    [SerializeField] private Cooldown attackCooldown = new Cooldown { duration = 0.5f };

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
    private static readonly int AttackHash = Animator.StringToHash("Attack");

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
        HandleAttackInput();
        ProcessLunge();
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
    /// ตรวจจับ Input คลิกเมาส์ซ้าย → เล่นแอนิเมชัน + พุ่ง
    /// เงื่อนไข: ต้องรอให้ Cooldown หมดก่อนถึงจะโจมตีครั้งถัดไปได้
    /// </summary>
    private void HandleAttackInput()
    {
        // เช็คว่าผู้เล่นกดคลิกเมาส์ซ้าย (Button 0)
        if (!Input.GetMouseButtonDown(0)) return;

        // เช็คว่า Cooldown พร้อมหรือยัง → ถ้ายังไม่หมดเวลา = ไม่ให้โจมตี (กันสแปมคลิก)
        if (!attackCooldown.IsReady()) return;

        // ═══ ผ่านเงื่อนไขทั้งหมด → ทำการโจมตี ═══

        // สั่ง Animator เล่นแอนิเมชัน Attack ผ่าน Trigger
        animator.SetTrigger(AttackHash);

        // เริ่มนับ Cooldown ใหม่ทันที เพื่อป้องกันการกดซ้ำก่อนเวลา
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
