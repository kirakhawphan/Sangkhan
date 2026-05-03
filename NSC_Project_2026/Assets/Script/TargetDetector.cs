using UnityEngine;

/// <summary>
/// ระบบตรวจจับเป้าหมายแบบ Reusable (Zero GC Allocation)
/// ใช้ [System.Serializable] เพื่อให้โชว์ใน Inspector ของสคริปต์ที่ฝังมัน
/// ไม่ได้สืบทอดจาก MonoBehaviour — สามารถใช้ร่วมกับระบบอะไรก็ได้ (Player, AI, Boss ฯลฯ)
/// </summary>
[System.Serializable]
public class TargetDetector
{
    // ==================== Detection Mode ====================

    public enum DetectionMode
    {
        /// <summary>ยิง SphereCast เป็นเส้นตรงตามทิศทาง (เหมาะกับ Player ที่เล็งจาก Camera)</summary>
        SphereCast,
        /// <summary>ตรวจจับรอบทิศทาง (เหมาะกับ AI / Boss ที่ต้องหาเป้าหมายรอบตัว)</summary>
        Sphere
    }

    // ==================== Settings (รวมอยู่ในคลาสเดียว) ====================

    [Header("Detection Settings")]
    [Tooltip("โหมดการค้นหาเป้าหมาย: SphereCast = ยิงเส้นตรง, Sphere = ตรวจรอบทิศ")]
    public DetectionMode detectionMode = DetectionMode.SphereCast;

    [Tooltip("ระยะการตรวจจับเป้าหมาย (ความไกล — ใช้กับทั้ง SphereCast และ Sphere)")]
    public float maxDetectionDistance = 15f;

    [Tooltip("ความกว้างของเป้าเล็ง / รัศมีทรงกลม (ยิ่งมากยิ่งเล็งโดนง่าย)")]
    public float aimRadius = 1.5f;

    [Tooltip("Layer ของเป้าหมาย (สำคัญ: ต้องตั้งให้ตรงกับ Layer ของ PossessableEntity)")]
    public LayerMask targetLayer;

    // ==================== ผลลัพธ์ล่าสุด (Read Only จากภายนอก) ====================

    public PossessableEntity CurrentTarget { get; private set; }

    // ==================== Pre-allocated Arrays (Zero GC) ====================

    private readonly RaycastHit[] hitResults = new RaycastHit[10];
    private readonly Collider[] overlapResults = new Collider[10];

    // ==================== Public API ====================

    /// <summary>
    /// ค้นหา PossessableEntity ที่ใกล้ที่สุด ตามโหมดที่เลือก
    /// ออกแบบไม่ผูกกับ Camera เพื่อให้ AI/Boss นำไปใช้ได้ด้วย
    /// </summary>
    /// <param name="origin">จุดกำเนิด (เช่น transform ของกล้อง หรือตำแหน่งตาของ AI)</param>
    /// <param name="direction">ทิศทาง (ใช้กับ SphereCast, โหมด Sphere จะไม่สนใจค่านี้)</param>
    /// <param name="excludeRoot">Transform ที่ต้องการกรองออก (เช่น ตัวผู้เล่นเอง) ส่ง null ได้ถ้าไม่ต้องกรอง</param>
    public void UpdateDetection(Transform origin, Vector3 direction, Transform excludeRoot)
    {
        switch (detectionMode)
        {
            case DetectionMode.SphereCast:
                DetectBySphereCast(origin.position, direction, excludeRoot);
                break;

            case DetectionMode.Sphere:
                DetectBySphere(origin.position, excludeRoot);
                break;
        }
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

        switch (detectionMode)
        {
            case DetectionMode.SphereCast:
                Gizmos.DrawRay(origin, direction * maxDetectionDistance);
                Gizmos.DrawWireSphere(origin + (direction * maxDetectionDistance), aimRadius);
                break;

            case DetectionMode.Sphere:
                Gizmos.DrawWireSphere(origin, maxDetectionDistance);
                break;
        }
    }

    // ==================== Private Detection Methods ====================

    /// <summary>
    /// โหมด SphereCast — ยิง SphereCast ตามทิศทาง (เหมาะกับ Player ที่เล็งจาก Camera)
    /// </summary>
    private void DetectBySphereCast(Vector3 originPos, Vector3 direction, Transform excludeRoot)
    {
        // วาดเส้นสีแดงใน Scene View เพื่อ Debug ง่าย
        Debug.DrawRay(originPos, direction * maxDetectionDistance, Color.red);

        int hitCount = Physics.SphereCastNonAlloc(
            originPos,
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

            // ตรวจสอบว่าไม่ใช่ตัวเอง
            if (IsExcluded(entity, excludeRoot)) continue;

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
    /// โหมด Sphere — ตรวจจับรอบทิศทางแบบทรงกลม (เหมาะกับ AI / Boss)
    /// ใช้ maxDetectionDistance เป็นรัศมี
    /// </summary>
    private void DetectBySphere(Vector3 originPos, Transform excludeRoot)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            originPos,
            maxDetectionDistance,
            overlapResults,
            targetLayer
        );

        PossessableEntity closestEntity = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapResults[i];

            PossessableEntity entity = hitCollider.GetComponentInParent<PossessableEntity>();
            if (entity == null) continue;

            if (IsExcluded(entity, excludeRoot)) continue;

            // ใช้ sqrMagnitude แทน Vector3.Distance เพื่อหลีกเลี่ยง sqrt (ประหยัด CPU)
            float distSqr = (hitCollider.transform.position - originPos).sqrMagnitude;
            if (distSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distSqr;
                closestEntity = entity;
            }
        }

        CurrentTarget = closestEntity;
    }

    // ==================== Utility ====================

    /// <summary>
    /// ตรวจสอบว่า entity เป็นตัวที่ต้องกรองออกหรือไม่ (ป้องกันสิงร่างตัวเอง)
    /// </summary>
    private bool IsExcluded(PossessableEntity entity, Transform excludeRoot)
    {
        if (excludeRoot == null) return false;

        return entity.gameObject == excludeRoot.gameObject ||
               entity.transform.IsChildOf(excludeRoot) ||
               excludeRoot.IsChildOf(entity.transform);
    }
}
