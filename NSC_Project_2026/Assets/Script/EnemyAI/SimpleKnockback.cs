using UnityEngine;

/// <summary>
/// ระบบกระเด็นแบบง่าย สำหรับศัตรูหรือหุ่นฟางที่ไม่ได้เดิน (ไม่มี NavMeshAgent / EnemyMovement)
/// </summary>
public class SimpleKnockback : MonoBehaviour
{
    [Tooltip("ระบบเลือด (สำหรับรับ Event ตอนโดนตี)")]
    public HealthSystem health;

    private Vector3 knockbackVelocity;
    private CharacterController characterController;

    private void Awake()
    {
        // เช็คว่ามี CharacterController ติดมาด้วยไหม
        characterController = GetComponent<CharacterController>();
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
        knockbackVelocity = force;
    }

    private void Update()
    {
        // ถ้ายากกระเด็นยังมีอยู่ ให้เลื่อนตัวละครไปด้านหลัง
        if (knockbackVelocity.sqrMagnitude > 0.1f)
        {
            // ถ้ามี CharacterController ให้ใช้ Move() เพื่อป้องกันการดีดกลับ
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(knockbackVelocity * Time.deltaTime);
            }
            else
            {
                // ถ้าไม่มี ให้เลื่อนตำแหน่ง transform ตรงๆ
                transform.position += knockbackVelocity * Time.deltaTime;
            }

            // ค่อยๆ ชะลอแรงลง
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 5f * Time.deltaTime);
        }
    }
}
