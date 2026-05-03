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

    [Header("Cooldown")]
    [SerializeField, Tooltip("เปิด/ปิด ระบบคูลดาวน์หลังสิงร่าง")]
    private bool useCooldown = true;

    [SerializeField, Tooltip("ระยะเวลาคูลดาวน์หลังสิงร่างสำเร็จ (วินาที)")]
    private float cooldownDuration = 3f;

    [Header("Target Detection")]
    [SerializeField, Tooltip("ระบบตรวจจับเป้าหมาย (ตั้งค่าระยะ, รัศมี, Layer ได้ใน Inspector)")]
    private TargetDetector targetDetector;

    // --- Smooth Camera Transition State ---
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;
    private Transform cameraTransformCache;

    // --- Cooldown State ---
    private float cooldownTimer = 0f;

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
        // นับถอยหลังคูลดาวน์
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // ปิดระบบ Target Detection ขณะคูลดาวน์
        if (IsOnCooldown)
        {
            if (targetDetector.CurrentTarget != null)
            {
                targetDetector.ClearTarget();
                if (uiController != null) uiController.HideUI();
            }
        }
        else if (playerCamera != null)
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
        // สร้าง Ray จากกึ่งกลางหน้าจอของกล้องผู้เล่น (ใช้ direction ของ Camera)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // มอบหมายให้ TargetDetector ทำงาน (ส่ง Transform แทน Vector3 ตาม API ใหม่)
        targetDetector.UpdateDetection(
            playerCamera.transform,
            ray.direction,
            currentBody != null ? currentBody.transform : null
        );

        // ควบคุม UI ตามผลลัพธ์จาก TargetDetector
        if (uiController != null)
        {
            if (targetDetector.CurrentTarget != null)
            {
                uiController.ShowUI(targetDetector.CurrentTarget.transform);
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
            // ตรวจสอบคูลดาวน์
            if (useCooldown && cooldownTimer > 0f)
            {
                Debug.Log($"ยังคูลดาวน์อยู่! เหลือเวลา {cooldownTimer:F1} วินาที");
                return;
            }

            if (targetDetector.CurrentTarget != null)
            {
                ExecutePossession(targetDetector.CurrentTarget);

                // เริ่มคูลดาวน์หลังสิงร่างสำเร็จ
                if (useCooldown)
                {
                    cooldownTimer = cooldownDuration;
                }
            }
            else
            {
                Debug.Log("ไม่มีเป้าหมายให้สิงร่างในเป้าเล็ง!");
            }
        }
    }

    /// <summary>
    /// ตรวจสอบว่ายังอยู่ในช่วงคูลดาวน์หรือไม่ (สำหรับ UI อื่นๆ ดึงไปใช้ได้)
    /// </summary>
    public bool IsOnCooldown => useCooldown && cooldownTimer > 0f;

    /// <summary>
    /// คืนค่าเปอร์เซ็นต์คูลดาวน์ที่เหลือ (1 = เพิ่งเริ่ม, 0 = หมดแล้ว) สำหรับ UI แสดง Cooldown Indicator
    /// </summary>
    public float CooldownProgress => useCooldown && cooldownDuration > 0f ? Mathf.Clamp01(cooldownTimer / cooldownDuration) : 0f;

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
            targetDetector.ClearTarget();
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
        if (playerCamera != null && targetDetector != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            targetDetector.DrawGizmos(ray.origin, ray.direction);
        }
    }
}
