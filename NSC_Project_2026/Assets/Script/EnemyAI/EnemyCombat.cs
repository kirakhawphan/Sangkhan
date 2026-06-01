using UnityEngine;

/// <summary>
/// ระบบต่อสู้ของศัตรู (สั่งโจมตีและคุมคูลดาวน์)
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private int comboStep = 1; // [เพิ่ม] ท่าที่ต้องการให้ AI ใช้ (เช่น 1, 2 หรือ 3)

    [Header("Movement Locks")]
    [SerializeField] private float normalAttackLockTime = 0.5f; // ระยะเวลาล็อกการเดินท่าปกติ
    [SerializeField] private float finishAttackLockTime = 1f; // ระยะเวลาล็อกการเดินเฉพาะท่าจบคอมโบ

    [Header("References")]
    [SerializeField] private Animator animator;

    private float nextAttackTime;
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int comboStepHash = Animator.StringToHash("ComboStep");
    private readonly int groundedHash = Animator.StringToHash("IsGrounded");
    private readonly int isWalkingHash = Animator.StringToHash("isWalking"); // [เพิ่ม] สั่งปิดเดินตอนต่อย

    // ตัวแปรสำหรับล็อกการเดิน
    private float currentMovementLockTimer;

    public bool IsAttacking
    {
        get { return currentMovementLockTimer > 0f; }
    }

    /// <summary>
    /// [เพิ่ม] รับค่าจาก EnemyData เพื่อเขียนทับค่า Inspector
    /// </summary>
    public void Initialize(EnemyData data)
    {
        if (data == null) return;
        attackCooldown = data.attackCooldown;
        attackRange = data.attackRange;
        comboStep = data.comboStep;
        normalAttackLockTime = data.normalAttackLockTime;
        finishAttackLockTime = data.finishAttackLockTime;
    }

    private void Update()
    {
        if (currentMovementLockTimer > 0f) currentMovementLockTimer -= Time.deltaTime;
    }

    /// <summary>
    /// เช็คว่าพร้อมโจมตีหรือยัง (ดูคูลดาวน์)
    /// </summary>
    public bool CanAttack()
    {
        return Time.time >= nextAttackTime;
    }

    /// <summary>
    /// สั่งเริ่มท่าโจมตี
    /// </summary>
    public void PerformAttack()
    {
        if (!CanAttack()) return;

#if UNITY_EDITOR
        Debug.Log($"[EnemyCombat] {gameObject.name} is performing attack!");
#endif
        
        // ล็อกการเดิน (อิงจากท่าที่กำลังตี ถ้าเป็นท่า 3 ให้ล็อกนานขึ้น)
        if (comboStep >= 3)
        {
            currentMovementLockTimer = finishAttackLockTime;
        }
        else
        {
            currentMovementLockTimer = normalAttackLockTime;
        }

        // เล่นแอนิเมชัน
        if (animator != null)
        {
            // [แก้ไข] สั่งปิดเดิน และยืนยันว่าอยู่บนพื้น เพื่อให้ Animator ยอมเปลี่ยนท่า
            animator.SetBool(isWalkingHash, false);
            animator.SetBool(groundedHash, true); 

            // ล้าง Trigger เก่า (เผื่อค้าง) และส่งค่า ComboStep
            animator.ResetTrigger(attackHash);
            animator.SetInteger(comboStepHash, comboStep);
            animator.SetTrigger(attackHash);
        }
        else
        {
            Debug.LogError($"[EnemyCombat] {gameObject.name} has no Animator assigned!");
        }

        // ตั้งเวลาคูลดาวน์ครั้งต่อไป
        nextAttackTime = Time.time + attackCooldown;
    }

    public float GetAttackRange() => attackRange;

    /// <summary>
    /// [แก้ไข] เปลี่ยนชื่อให้เหมือน Player และเรียกฟังก์ชันที่ถูกต้องเพื่อแก้บั๊ก
    /// </summary>
    public void AE_TriggerWeaponAttack()
    {
        // ค้นหา MeleeHitbox ที่ติดอยู่กับตัวศัตรู (หรือลูกๆ เช่น ในมือ)
        MeleeHitbox hitbox = GetComponentInChildren<MeleeHitbox>();
        
        if (hitbox != null)
        {
            hitbox.PerformAttack(); // แก้จาก ExecuteAttack เป็น PerformAttack ตามสคริปต์จริง
        }
        else
        {
            Debug.LogWarning($"[EnemyCombat] {gameObject.name} has no MeleeHitbox found in children!");
        }
    }

    /// <summary>
    /// [เพิ่ม] รีเซ็ตค่าแอนิเมชันให้กลับเป็นท่าปกติ
    /// </summary>
    public void ResetAttack()
    {
        if (animator != null)
        {
            animator.SetInteger(comboStepHash, 0);
        }
    }
}
