using UnityEngine;

// นำสคริปต์นี้ไปแปะไว้ที่ GameManager หรือ GameObject เปล่าๆ ในฉาก (ไม่ต้องแปะไว้ที่ตัวละครแล้ว)
public class PossessionManager : MonoBehaviour
{
    [Header("System References")]
    [SerializeField, Tooltip("ตัวละครเริ่มต้นที่ผู้เล่นควบคุม (ลากตัวละครที่มี Playermovement มาใส่)")]
    private Playermovement currentBody;

    [SerializeField, Tooltip("กล้องของผู้เล่น (ลาก Main Camera มาใส่ช่องนี้)")]
    private Camera playerCamera;

    [SerializeField, Tooltip("สคริปต์ควบคุม UI ที่สร้างไว้ (ลากจาก Hierarchy มาใส่)")]
    private PossessionUIController uiController;

    [Header("Detection Settings")]
    [SerializeField, Tooltip("ระยะการตรวจจับเป้าหมายที่อยู่หน้ากล้อง (ความไกล)")]
    private float maxDetectionDistance = 15f;
    
    [SerializeField, Tooltip("ความกว้างของเป้าเล็ง (ยิ่งมากยิ่งเล็งโดนง่ายโดยไม่ต้องหันหน้าตรงเป๊ะ)")]
    private float aimRadius = 1.5f;

    [SerializeField, Tooltip("Layer ของเป้าหมาย (สำคัญ: ต้องตั้งให้ตรงกับ Layer ของ PossessableEntity)")]
    private LayerMask possessableLayer; 

    [Header("Target Info (Read Only)")]
    [SerializeField, Tooltip("เป้าหมายที่อยู่หน้ากล้องและใกล้ที่สุดในขณะนี้")]
    private PossessableEntity currentTarget;

    // --- Optimization Caching ---
    private readonly RaycastHit[] hitResults = new RaycastHit[10]; 
    private readonly Vector3 screenCenter = new Vector3(0.5f, 0.5f, 0f);

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                Debug.LogError("[PossessionManager] Player Camera is not assigned! Please drag the camera into the inspector.", this);
        }

        if (currentBody != null && playerCamera != null)
        {
            // เซ็ตกล้องและสถานะให้ตัวละครเริ่มต้น
            currentBody.SetupCamera(playerCamera.transform);
            currentBody.isPossessed = true;
            currentBody.enabled = true; // มั่นใจว่าเปิดใช้งาน
        }
    }

    private void Update()
    {
        if (playerCamera != null)
        {
            UpdateTargetDetection();
        }

        HandlePossessionInput();
    }

    private void UpdateTargetDetection()
    {
        Ray ray = playerCamera.ViewportPointToRay(screenCenter);
        
        // วาดเส้นสีแดงในหน้า Scene View ตลอดเวลาเพื่อให้เห็นทิศทางของ Ray
        Debug.DrawRay(ray.origin, ray.direction * maxDetectionDistance, Color.red);
        
        int hitCount = Physics.SphereCastNonAlloc(ray, aimRadius, hitResults, maxDetectionDistance, possessableLayer);
        
        PossessableEntity closestEntity = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitResults[i].collider;
            
            // ใช้ GetComponentInParent เพื่อดักจับกรณีที่ Collider อยู่ที่ลูก แต่สคริปต์อยู่แม่
            PossessableEntity entity = hitCollider.GetComponentInParent<PossessableEntity>();
            
            if (entity != null)
            {
                // ตรวจสอบว่าไม่ใช่ตัวเอง
                if (currentBody != null && entity.gameObject == currentBody.gameObject)
                    continue;

                float distance = hitResults[i].distance;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEntity = entity;
                }
            }
        }

        currentTarget = closestEntity;

        // ควบคุม UI
        if (uiController != null)
        {
            if (currentTarget != null)
            {
                uiController.ShowUI(currentTarget.transform);
            }
            else
            {
                uiController.HideUI();
            }
        }
    }

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

    private void ExecutePossession(PossessableEntity targetEntity)
    {
        Debug.Log($"กำลังสิงร่าง: {targetEntity.EntityName}");
        
        // 1. ดึงสคริปต์ Playermovement ของร่างเป้าหมาย
        if (targetEntity.TryGetComponent(out Playermovement newBody))
        {
            // 2. ถอดสิทธิ์ตัวเดิม (ให้เหลือแต่ระบบ Gravity)
            if (currentBody != null)
            {
                currentBody.isPossessed = false;
                // สังเกตว่าเราไม่ได้ตั้ง enabled = false แล้ว เพื่อให้ตัวละครเก่ายังคงตกลงพื้นได้!
                
                // หากมี Animator สั่งให้มันหยุดเดิน
                Animator oldAnim = currentBody.GetComponentInChildren<Animator>();
                if (oldAnim != null)
                {
                    oldAnim.SetFloat("Speed", 0f);
                }
            }

            // 3. เซ็ตค่าให้ตัวใหม่ (ส่งมอบกล้องไปให้)
            newBody.SetupCamera(playerCamera.transform);

            // 4. มอบสิทธิ์ใช้งานตัวใหม่
            newBody.isPossessed = true;
            newBody.enabled = true;

            // 5. อัปเดตตัวแปร currentBody เป็นตัวใหม่
            currentBody = newBody;

            // ซ่อน UI หลังสิงร่างสำเร็จ
            currentTarget = null;
            if (uiController != null) uiController.HideUI();
        }
        else
        {
            Debug.LogError($"[PossessionManager] เป้าหมาย {targetEntity.name} ไม่มีสคริปต์ Playermovement!", targetEntity);
        }
    }

    // เปลี่ยนมาใช้ OnDrawGizmos (ไม่ต้องกดคลิกวัตถุก็เห็นเส้นเหลืองได้)
    private void OnDrawGizmos()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            Gizmos.DrawRay(ray.origin, ray.direction * maxDetectionDistance);
            Gizmos.DrawWireSphere(ray.origin + (ray.direction * maxDetectionDistance), aimRadius);
        }
    }
}
