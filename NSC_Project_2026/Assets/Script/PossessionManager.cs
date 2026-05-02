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

    [Header("Smooth Camera Transition")]
    [SerializeField, Tooltip("เปิด/ปิด ระบบเคลื่อนกล้องแบบ Smooth ตอนสิงร่าง (ปิดแล้วกล้องจะวาร์ปไปทันที)")]
    private bool useSmoothCameraTransition = true;

    [SerializeField, Tooltip("ระยะเวลาที่กล้องใช้เคลื่อนที่ไปยังร่างใหม่ (วินาที)")]
    private float cameraTransitionDuration = 0.6f;

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

    // --- Smooth Camera Transition State ---
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;
    private Transform cameraTransformCache;

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

    private void LateUpdate()
    {
        if (isTransitioning)
        {
            HandleCameraTransition();
        }
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
                // ตรวจสอบว่าไม่ใช่ตัวเอง (ตรวจสอบทั้ง GameObject และ Hierarchy แม่ลูก)
                if (currentBody != null)
                {
                    if (entity.gameObject == currentBody.gameObject || 
                        entity.transform.IsChildOf(currentBody.transform) || 
                        currentBody.transform.IsChildOf(entity.transform))
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
                currentBody.cameraLocked = false; // ปลดล็อกกล้องตัวเก่า
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

            // 5. เริ่มระบบ Smooth Camera Transition (ถ้าเปิดใช้งาน)
            if (useSmoothCameraTransition)
            {
                StartCameraTransition(newBody);
            }

            // 6. อัปเดตตัวแปร currentBody เป็นตัวใหม่
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

    // ==================== Smooth Camera Transition ====================

    /// <summary>
    /// เริ่มระบบเคลื่อนกล้องแบบ Smooth ไปยังร่างใหม่
    /// จะล็อกกล้องของ Playermovement ไว้ชั่วคราวแล้วให้ PossessionManager ควบคุมแทน
    /// </summary>
    private void StartCameraTransition(Playermovement newBody)
    {
        if (cameraTransformCache == null)
        {
            cameraTransformCache = playerCamera.transform;
        }

        // จำตำแหน่งปัจจุบันของกล้องก่อนย้าย
        transitionStartPos = cameraTransformCache.position;
        transitionStartRot = cameraTransformCache.rotation;

        // ล็อกกล้องของตัวใหม่ไว้ไม่ให้มัน snap ใน HandleCamera()
        newBody.cameraLocked = true;

        // รีเซ็ตตัวจับเวลา
        transitionTimer = 0f;
        isTransitioning = true;
    }

    /// <summary>
    /// จัดการ Lerp กล้องทุกเฟรม (เรียกใน LateUpdate เพื่อให้ทำงานหลัง HandleCamera)
    /// </summary>
    private void HandleCameraTransition()
    {
        if (currentBody == null || cameraTransformCache == null)
        {
            FinishCameraTransition();
            return;
        }

        transitionTimer += Time.deltaTime;
        float t = Mathf.Clamp01(transitionTimer / cameraTransitionDuration);

        // ใช้ SmoothStep เพื่อให้การเคลื่อนที่รู้สึกเป็นธรรมชาติ (ช้า -> เร็ว -> ช้า)
        float smoothT = t * t * (3f - 2f * t);

        // คำนวณจุดหมายปลายทาง (อัปเดตทุกเฟรมเพราะตัวละครอาจเคลื่อนที่ระหว่าง transition)
        currentBody.GetCameraDestination(out Vector3 destPos, out Quaternion destRot);

        // Lerp ตำแหน่งและการหมุน
        cameraTransformCache.position = Vector3.Lerp(transitionStartPos, destPos, smoothT);
        cameraTransformCache.rotation = Quaternion.Slerp(transitionStartRot, destRot, smoothT);

        // จบ transition เมื่อถึงเป้าหมาย
        if (t >= 1f)
        {
            FinishCameraTransition();
        }
    }

    /// <summary>
    /// จบระบบ Smooth Transition และคืนสิทธิ์กล้องให้ Playermovement
    /// </summary>
    private void FinishCameraTransition()
    {
        isTransitioning = false;

        // ปลดล็อกกล้องให้ Playermovement กลับมาควบคุมเอง
        if (currentBody != null)
        {
            currentBody.cameraLocked = false;
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
