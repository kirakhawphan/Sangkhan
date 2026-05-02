using UnityEngine;

// นำสคริปต์นี้ไปแปะไว้ที่ตัวละครที่ผู้เล่นกำลังควบคุมอยู่
public class PossessionManager : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField, Tooltip("ระยะการตรวจจับเป้าหมายที่อยู่หน้ากล้อง (ความไกล)")]
    private float maxDetectionDistance = 15f;
    
    [SerializeField, Tooltip("ความกว้างของเป้าเล็ง (ยิ่งมากยิ่งเล็งโดนง่ายโดยไม่ต้องหันหน้าตรงเป๊ะ)")]
    private float aimRadius = 1.5f;

    [SerializeField, Tooltip("Layer ของเป้าหมาย (สำคัญ: ต้องตั้งให้ตรงกับ Layer ของ PossessableEntity)")]
    private LayerMask possessableLayer; 

    [SerializeField, Tooltip("กล้องของผู้เล่น (ลาก Main Camera มาใส่ช่องนี้)")]
    private Camera playerCamera;

    [Header("Target Info (Read Only)")]
    [SerializeField, Tooltip("เป้าหมายที่อยู่หน้ากล้องและใกล้ที่สุดในขณะนี้")]
    private PossessableEntity currentTarget;

    // --- Optimization Caching ---
    // ใช้ NonAlloc เพื่อลด Garbage Collection (Zero Allocation) ในแต่ละเฟรม
    // จองหน่วยความจำไว้ล่วงหน้า (สามารถปรับขนาด Array ได้ตามความเหมาะสมของเกม)
    private readonly RaycastHit[] hitResults = new RaycastHit[10]; 
    
    // เก็บค่ากึ่งกลางหน้าจอไว้เพื่อไม่ให้เกิดการสร้าง Vector3 ใหม่ทุกเฟรม
    private readonly Vector3 screenCenter = new Vector3(0.5f, 0.5f, 0f);

    private void Awake()
    {
        // ย้ายการเช็คค่าว่างมาไว้ใน Awake เพื่อแจังเตือนตั้งแต่เริ่มเกม แทนที่จะหาใน Update หรือใช้ Find
        if (playerCamera == null)
        {
            Debug.LogError("[PossessionManager] Player Camera is not assigned! Please drag the camera into the inspector.", this);
        }
    }

    private void Update()
    {
        // Clean Architecture: แยกหน้าที่ให้ชัดเจน (Single Responsibility)
        if (playerCamera != null)
        {
            UpdateTargetDetection();
        }

        HandlePossessionInput();
    }

    // ฟังก์ชันสำหรับตรวจหา PossessableEntity ตรงหน้ากล้อง
    private void UpdateTargetDetection()
    {
        Ray ray = playerCamera.ViewportPointToRay(screenCenter);
        
        // ใช้ SphereCastNonAlloc แทน SphereCastAll เพื่อไม่ให้มีการจอง Memory (GC Allocation) ใหม่ในทุกเฟรม
        int hitCount = Physics.SphereCastNonAlloc(ray, aimRadius, hitResults, maxDetectionDistance, possessableLayer);
        
        PossessableEntity closestEntity = null;
        float closestDistance = float.MaxValue;

        // วนลูปเฉพาะจำนวนที่โดนจริง (hitCount)
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitResults[i].collider;
            
            // ใช้ TryGetComponent แทน GetComponent ปกติ เพื่อลด Overhead ในการเช็ค Null และทำงานเร็วกว่า
            if (hitCollider.TryGetComponent(out PossessableEntity entity))
            {
                // ตรวจสอบว่าไม่ใช่ตัวเอง (กันการสิงร่างตัวเอง)
                if (entity.gameObject != this.gameObject)
                {
                    float distance = hitResults[i].distance;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestEntity = entity;
                    }
                }
            }
        }

        currentTarget = closestEntity;
    }

    // ฟังก์ชันรับ Input เพื่อสิงร่าง
    private void HandlePossessionInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentTarget != null)
            {
                ExecutePossession(currentTarget);
            }
            else
            {
                Debug.Log("ไม่มีเป้าหมายให้สิงร่างในเป้าเล็ง!");
            }
        }
    }

    // ฟังก์ชันดำเนินการสิงร่าง
    private void ExecutePossession(PossessableEntity targetEntity)
    {
        // เรียก Property EntityName ที่ถูก Encapsulate ไว้
        Debug.Log($"กำลังสิงร่าง: {targetEntity.EntityName}");
        
        // --- สิ่งที่ต้องทำต่อไปในระบบสลับร่าง ---
    }

    // แสดงลำแสงการตรวจจับสีเหลืองในหน้า Scene
    private void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); // อนุโลมใน Gizmos ได้เพราะไม่ได้รันตอนเล่นจริง
            
            Gizmos.DrawRay(ray.origin, ray.direction * maxDetectionDistance);
            Gizmos.DrawWireSphere(ray.origin + (ray.direction * maxDetectionDistance), aimRadius);
        }
    }
}
