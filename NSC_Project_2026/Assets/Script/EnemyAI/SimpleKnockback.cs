using UnityEngine;

/// <summary>
/// ระบบกระเด็นแบบง่าย สำหรับศัตรูหรือหุ่นฟางที่ไม่ได้เดิน (ไม่มี NavMeshAgent / EnemyMovement)
/// </summary>
public class SimpleKnockback : MonoBehaviour
{
    [Tooltip("ระบบเลือด (สำหรับรับ Event ตอนโดนตี)")]
    public HealthSystem health;

    [Tooltip("Animator สำหรับเล่นแอนิเมชันชะงัก (ใส่หรือไม่ใส่ก็ได้)")]
    public Animator animator;

    private Vector3 knockbackVelocity;
    private float verticalVelocity; // [เพิ่ม] แรงโน้มถ่วงสะสม ป้องกันลอย/จมดิน
    private CharacterController characterController;

    private void Awake()
    {
        // เช็คว่ามี CharacterController ติดมาด้วยไหม
        characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        // สมัครรับ Event เมื่อเกราะแตก จะได้เรียกฟังก์ชัน ApplyKnockback อัตโนมัติ
        if (health != null) health.OnHurt += ApplyKnockback;
    }

    private void OnDisable()
    {
        if (health != null) health.OnHurt -= ApplyKnockback;
    }

    // ฟังก์ชันนี้จะถูกเรียกอัตโนมัติเมื่อ HealthSystem ยิง OnHurt
    private void ApplyKnockback(Vector3 force)
    {
        // [แก้ไข] บังคับให้แรงกระเด็นเป็นแนวนอนเท่านั้น (ป้องกันจมดิน)
        force.y = 0f;
        knockbackVelocity = force;
        verticalVelocity = -2f; // รีเซ็ตแรงโน้มถ่วง
        
        // เล่นแอนิเมชันโดนตี (ถ้ามี)
        if (animator != null)
        {
            animator.SetTrigger("Hurt");
        }
    }

    private void Update()
    {
        // ถ้ายากกระเด็นยังมีอยู่ ให้เลื่อนตัวละครไปด้านหลัง
        if (knockbackVelocity.sqrMagnitude > 0.1f)
        {
            // [แก้ไข] บวกแรงโน้มถ่วงเพื่อกดตัวลงพื้น ป้องกันลอย/จมดิน
            if (characterController != null && characterController.enabled && characterController.isGrounded)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += -9.81f * Time.deltaTime;
            }

            // รวมแรง Knockback (แนวนอน) + แรงโน้มถ่วง (แนวตั้ง)
            Vector3 moveVector = knockbackVelocity * Time.deltaTime;
            moveVector.y = verticalVelocity * Time.deltaTime;

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(moveVector);
            }
            else
            {
                transform.position += moveVector;
            }

            // ค่อยๆ ชะลอแรงกระเด็น (เฉพาะแนวนอน)
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 5f * Time.deltaTime);
        }
    }
}
