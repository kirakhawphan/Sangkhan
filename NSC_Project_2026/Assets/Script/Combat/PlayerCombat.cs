using UnityEngine;

/// <summary>
/// ระบบรับ Input การโจมตีของผู้เล่น
/// หน้าที่: ตรวจจับคลิกเมาส์ซ้าย → สั่ง Animator เล่นแอนิเมชันฟันดาบ
/// พร้อมระบบ Cooldown กันกดรัว (ใช้คลาส Cooldown ที่มีอยู่แล้วใน Core/)
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    // ───────────────────────────── Inspector Fields ─────────────────────────────

    [Header("References")]
    [Tooltip("ลาก Animator ของตัวละครมาใส่ (ถ้าไม่ใส่ จะหาจาก GameObject นี้อัตโนมัติ)")]
    [SerializeField] private Animator animator;

    [Header("Attack Settings")]
    [Tooltip("คูลดาวน์ระหว่างการโจมตีแต่ละครั้ง")]
    [SerializeField] private Cooldown attackCooldown = new Cooldown { duration = 0.5f };

    // ───────────────────────────── Animator Parameter Hash ──────────────────────
    // Strict Rule: แปลงชื่อ Parameter เป็น Hash ล่วงหน้า
    // เพื่อหลีกเลี่ยงการสร้าง String ใหม่ทุกเฟรม (ลด GC Allocation)
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    // ════════════════════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // ถ้าลืมลาก Animator ใน Inspector → หาจาก Component ที่ติดอยู่กับ GameObject นี้
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void Update()
    {
        HandleAttackInput();
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  Private Methods
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ตรวจจับ Input คลิกเมาส์ซ้าย และสั่งเล่นแอนิเมชันโจมตี
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
    }
}
