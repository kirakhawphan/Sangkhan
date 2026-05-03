using UnityEngine;

/// <summary>
/// ระบบตรวจจับเป้าหมายแบบ Reusable
/// ใช้ [System.Serializable] เพื่อให้โชว์ใน Inspector ของสคริปต์ที่ฝังมัน
/// ไม่ได้สืบทอดจาก MonoBehaviour — สามารถใช้ร่วมกับระบบอะไรก็ได้ (Player, AI, Boss ฯลฯ)
/// </summary>
[System.Serializable]
public class TargetDetector
{
    [Header("Detection Settings")]
    [Tooltip("ระยะการตรวจจับเป้าหมาย (ความไกล)")]
    public float maxDetectionDistance = 15f;

    [Tooltip("ความกว้างของเป้าเล็ง (ยิ่งมากยิ่งเล็งโดนง่าย)")]
    public float aimRadius = 1.5f;

    [Tooltip("Layer ของเป้าหมาย (สำคัญ: ต้องตั้งให้ตรงกับ Layer ของ PossessableEntity)")]
    public LayerMask targetLayer;

    // --- ผลลัพธ์ล่าสุด (Read Only จากภายนอก) ---
    public PossessableEntity CurrentTarget { get; private set; }

    // --- Optimization Caching (Zero Allocation) ---
    private readonly RaycastHit[] hitResults = new RaycastHit[10];

    /// <summary>
    /// รัน SphereCast เพื่อค้นหา PossessableEntity ที่ใกล้ที่สุดในทิศทางที่ระบุ
    /// ออกแบบไม่ผูกกับ Camera เพื่อให้ AI/Boss นำไปใช้ได้ด้วย
    /// </summary>
    /// <param name="origin">จุดกำเนิดของ Ray (เช่น ตำแหน่งกล้อง หรือ ตำแหน่งตาของ AI)</param>
    /// <param name="direction">ทิศทางของ Ray (เช่น กล้อง forward หรือ AI มองไปทางเป้าหมาย)</param>
    /// <param name="excludeRoot">Transform ที่ต้องการกรองออก (เช่น ตัวผู้เล่นเอง หรือ ตัว AI เอง) ส่ง null ได้ถ้าไม่ต้องกรอง</param>
    public void UpdateDetection(Vector3 origin, Vector3 direction, Transform excludeRoot)
    {
        // วาดเส้นสีแดงใน Scene View เพื่อ Debug ง่าย
        Debug.DrawRay(origin, direction * maxDetectionDistance, Color.red);

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            aimRadius,
            direction,
            hitResults,
            maxDetectionDistance,
            targetLayer
        );

        PossessableEntity closestEntity = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitResults[i].collider;

            // ใช้ GetComponentInParent เพื่อดักจับกรณีที่ Collider อยู่ที่ลูก แต่สคริปต์อยู่แม่
            PossessableEntity entity = hitCollider.GetComponentInParent<PossessableEntity>();

            if (entity == null) continue;

            // ตรวจสอบว่าไม่ใช่ตัวเอง (ถ้ามี excludeRoot ระบุ)
            if (excludeRoot != null)
            {
                if (entity.gameObject == excludeRoot.gameObject ||
                    entity.transform.IsChildOf(excludeRoot) ||
                    excludeRoot.IsChildOf(entity.transform))
                {
                    continue;
                }
            }

            float distance = hitResults[i].distance;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEntity = entity;
            }
        }

        CurrentTarget = closestEntity;
    }

    /// <summary>
    /// ล้างเป้าหมายปัจจุบัน (ใช้ตอนเข้า Cooldown หรือต้องการรีเซ็ต)
    /// </summary>
    public void ClearTarget()
    {
        CurrentTarget = null;
    }

    /// <summary>
    /// วาด Gizmo แสดงระยะตรวจจับ (เรียกจาก OnDrawGizmos ของ MonoBehaviour ที่ฝังอยู่)
    /// </summary>
    /// <param name="origin">จุดกำเนิดของ Ray</param>
    /// <param name="direction">ทิศทางของ Ray</param>
    public void DrawGizmos(Vector3 origin, Vector3 direction)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, direction * maxDetectionDistance);
        Gizmos.DrawWireSphere(origin + (direction * maxDetectionDistance), aimRadius);
    }
}
