using UnityEngine;

// นำสคริปต์นี้ไปแปะไว้ที่ GameManager หรือ GameObject เปล่าๆ ในฉาก (ไม่ต้องแปะไว้ที่ตัวละครแล้ว)
public class PossessionManager : MonoBehaviour
{
    public static PossessionManager Instance { get; private set; }

    [Header("System References")]
    [SerializeField, Tooltip("ตัวละครเริ่มต้นที่ผู้เล่นควบคุม (ลากตัวละครที่มี Playermovement มาใส่)")]
    private Playermovement currentBody;

    [SerializeField, Tooltip("กล้องของผู้เล่น (ลาก Main Camera มาใส่ช่องนี้)")]
    private Camera playerCamera;

    [SerializeField, Tooltip("สคริปต์ควบคุม UI ที่สร้างไว้ (ลากจาก Hierarchy มาใส่)")]
    private PossessionUIController uiController;

    [SerializeField, Tooltip("หลอดเลือด HUD บนหน้าจอ (ลาก HealthBarUI มาใส่)")]
    private HealthBarUI healthBarUI;

    [SerializeField, Tooltip("ระบบสั่นกล้อง (ลาก CameraShake มาใส่)")]
    private CameraShake cameraShake;

    [SerializeField, Tooltip("ระบบเอฟเฟกต์ FOV (ลาก CameraFOV มาใส่)")]
    private CameraFOV cameraFOV;

    [Header("Smooth Camera Transition")]
    [SerializeField, Tooltip("ระยะเวลาที่กล้องใช้เคลื่อนที่ไปยังร่างใหม่ (วินาที)\nใส่ 0 เพื่อปิดระบบนี้และให้กล้องวาร์ปไปทันที")]
    private float cameraTransitionDuration = 0.6f;

    [Header("Cooldown")]
    [SerializeField, Tooltip("ระบบคูลดาวน์หลังสิงร่าง (ตั้งค่า duration ได้ใน Inspector)")]
    private Cooldown possessionCooldown;

    [Header("Target Detection")]
    [SerializeField, Tooltip("ระบบตรวจจับเป้าหมาย (ตั้งค่าระยะ, รัศมี, Layer ได้ใน Inspector)")]
    private TargetDetector targetDetector;

    // --- Events ---
    [Tooltip("อีเวนต์เมื่อร่างสิงของผู้เล่นเสียชีวิตลง (สำหรับเกาะไปเขียน Restart/GameOver)")]
    public event System.Action OnPossessedBodyDeath;

    // --- Smooth Camera Transition State ---
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;
    private Transform cameraTransformCache;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

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

    private void Start()
    {
        // สมัครรับข้อมูลความตายสำหรับร่างแรกเริ่ม
        if (currentBody != null && currentBody.TryGetComponent(out HealthSystem initialHealth))
        {
            initialHealth.OnDeath += HandlePossessedBodyDeath;
        }
    }

    private void OnDisable()
    {
        // ยกเลิกรับข้อมูลความตายเพื่อป้องกัน Memory Leak
        if (currentBody != null && currentBody.TryGetComponent(out HealthSystem currentHealth))
        {
            currentHealth.OnDeath -= HandlePossessedBodyDeath;
        }
    }

    private void Update()
    {
        // ปิดระบบ Target Detection ขณะคูลดาวน์
        if (!possessionCooldown.IsReady())
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
            if (!possessionCooldown.IsReady())
            {
                Debug.Log("ยังคูลดาวน์อยู่! รอสักครู่...");
                return;
            }

            if (targetDetector.CurrentTarget != null)
            {
                ExecutePossession(targetDetector.CurrentTarget);

                // เริ่มคูลดาวน์หลังสิงร่างสำเร็จ
                possessionCooldown.StartCooldown();
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
    public bool IsOnCooldown => !possessionCooldown.IsReady();

    /// <summary>
    /// คืนค่าเปอร์เซ็นต์คูลดาวน์ที่เหลือ (1 = เพิ่งเริ่ม, 0 = หมดแล้ว) สำหรับ UI แสดง Cooldown Indicator
    /// </summary>
    public float CooldownProgress => possessionCooldown.Progress;

    private void ExecutePossession(PossessableEntity targetEntity)
    {
        Debug.Log($"กำลังสิงร่าง: {targetEntity.EntityName}");
        
        // 1. ดึงสคริปต์ Playermovement ของร่างเป้าหมาย
        if (targetEntity.TryGetComponent(out Playermovement newBody))
        {
            // 2. ถอดสิทธิ์ตัวเดิม
            if (currentBody != null)
            {
                currentBody.isPossessed = false;
                currentBody.cameraLocked = false; // ปลดล็อกกล้องตัวเก่า

                // ยกเลิกการดักจับความตายของร่างเก่า
                if (currentBody.TryGetComponent(out HealthSystem oldHealth))
                {
                    oldHealth.OnDeath -= HandlePossessedBodyDeath;
                }
                
                // [เพิ่ม] บังคับปิด PlayerCombat ของร่างเก่าทันที (Safety Net)
                // ป้องกันกรณี PossessableEntity ไม่ได้ตั้งค่าช่อง PlayerCombat ไว้
                PlayerCombat oldCombat = currentBody.GetComponent<PlayerCombat>();
                if (oldCombat != null)
                {
                    oldCombat.enabled = false;
                }

                // หากตัวเก่ามี PossessableEntity ให้เรียก OnUnpossessed เพื่อคืนชีพ AI
                if (currentBody.TryGetComponent(out PossessableEntity oldEntity))
                {
                    oldEntity.OnUnpossessed();
                }
                
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
            
            // เรียกฟังก์ชัน OnPossessed เพื่อให้ AI หลับ และสลับโหมด (รวมถึงเปิด PlayerCombat บนร่างใหม่ด้วย)
            targetEntity.OnPossessed();

            // 5. เริ่มระบบ Smooth Camera Transition (ถ้าเปิดใช้งาน)
            if (cameraTransitionDuration > 0f)
            {
                StartCameraTransition(newBody);
            }

            // 6. สลับหลอดเลือด HUD + ระบบสั่นกล้อง ไปยังร่างใหม่
            HealthSystem newHealthSystem = newBody.GetComponent<HealthSystem>();

            if (newHealthSystem != null)
            {
                // ดักจับการตายของร่างใหม่
                newHealthSystem.OnDeath += HandlePossessedBodyDeath;
            }

            if (healthBarUI != null && newHealthSystem != null)
            {
                healthBarUI.SetTargetHealthSystem(newHealthSystem);
            }

            if (cameraShake != null)
            {
                cameraShake.SetTarget(newHealthSystem, newBody);
            }

            if (cameraFOV != null)
            {
                cameraFOV.SetTarget(newHealthSystem);
            }

            // 7. อัปเดตตัวแปร currentBody เป็นตัวใหม่
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

    /// <summary>
    /// ฟังก์ชันทำงานอัตโนมัติเมื่อร่างที่ผู้เล่นกำลังสิงร่างอยู่เสียชีวิตลง
    /// </summary>
    private void HandlePossessedBodyDeath()
    {
        Debug.LogWarning($"[PossessionManager] ร่างที่ผู้เล่นสิงอยู่ ('{(currentBody != null ? currentBody.gameObject.name : "Unknown")}') ได้เสียชีวิตลงแล้ว!");

        // 1. บังคับปิดระบบเคลื่อนที่และการต่อสู้ของผู้เล่น เพื่อหยุดการขยับและปล่อยให้เล่นแอนิเมชันตาย
        if (currentBody != null)
        {
            currentBody.enabled = false;

            PlayerCombat combat = currentBody.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.enabled = false;
            }
        }

        // 2. เคลียร์เป้าหมายการตรวจเล็ง
        if (targetDetector != null)
        {
            targetDetector.ClearTarget();
        }

        if (uiController != null)
        {
            uiController.HideUI();
        }

        // 3. ยิง Event สัญญาณความตายให้ระบบอื่นเข้ามารับช่วงต่อ (เช่น เปิด UI Game Over / Restart)
        OnPossessedBodyDeath?.Invoke();
    }
}
