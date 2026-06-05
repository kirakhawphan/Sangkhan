using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Playermovement : MonoBehaviour
{
    [Header("Possession State")]
    [Tooltip("ติ๊กถูกเฉพาะตัวที่ผู้เล่นควบคุมตั้งแต่เริ่มเกม")]
    public bool isPossessed = false;

    [HideInInspector]
    [Tooltip("ล็อกกล้องไม่ให้ HandleCamera ทำงาน (ใช้ตอน PossessionManager กำลังเคลื่อนกล้องแบบ Smooth)")]
    public bool cameraLocked = false;

    [Header("Impact Settings (When Possessed)")]
    [Tooltip("โปรไฟล์การสั่นเมื่อตัวละครนี้โจมตีโดนเป้าหมาย (เว้นว่างไว้หากไม่ต้องการให้สั่น)")]
    public ImpactProfile playerGlobalImpactProfile;

    [Tooltip("โปรไฟล์การสั่นรุนแรงเมื่อตัวละครนี้โจมตีจนเกราะ (Poise) ของศัตรูแตก")]
    public ImpactProfile poiseBreakImpactProfile;

    // ค่า offset จาก CameraShake (สคริปต์อื่นเขียนค่านี้ได้ Playermovement จะบวกเพิ่มให้ตอนเซ็ตกล้อง)
    [HideInInspector]
    public Vector3 cameraShakeOffset;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float runSpeed = 10f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float turnSmoothTime = 0.1f;
    
    [Header("Camera Control")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform cameraTarget; // จุดอ้างอิงให้กล้องหมุนรอบ (ใส่เป็นกระดูกไหล่/หน้าอกได้)
    [SerializeField] private float mouseSensitivityX = 3f;
    [SerializeField] private float mouseSensitivityY = 3f;
    [SerializeField] private float distance = 5f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0, 1.5f, 0); 
    [SerializeField] private float minYAngle = -20f;
    [SerializeField] private float maxYAngle = 80f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask;

    [Header("Dash Settings")]
    [SerializeField] private KeyCode dashKey = KeyCode.LeftAlt;
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private AnimationCurve dashCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    [SerializeField] private Cooldown dashCooldown = new Cooldown { duration = 1.0f };

    // --- Dash State ---
    public bool IsDashing => isDashing;
    private bool isDashing = false;
    private float currentDashTime = 0f;
    private Vector3 dashDirection;
    private float dashAnimationClipLength = 0.3f;
    private float originalAnimatorSpeed = 1f;
    private float dashCurveArea = 0.5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    // --- Caching Components & Variables ---
    private CharacterController controller;
    private PlayerCombat playerCombat; // แคช PlayerCombat เพื่อเช็ค IsAttacking
    private HealthSystem playerHealthSystem; // [เพิ่ม] แคชไว้เปิด I-Frame
    private float turnSmoothVelocity;
    private float currentX = 0f;
    private float currentY = 0f;
    private Vector3 velocity;
    private bool isGrounded;
    private bool hasGroundCheck;
    
    // --- Zero Allocation Caches ---
    // สร้างตัวแปร Vector ไว้ล่วงหน้า ไม่ใช้คำสั่ง new ใน Update
    private Vector3 inputDirection;
    private Vector3 moveDirection;
    
    // แคชค่า Jump Velocity เพื่อหลีกเลี่ยง Mathf.Sqrt() ในขณะเล่น
    private float calculatedJumpVelocity;
    
    // แคช Animator Parameters เป็น Hash (int) เพื่อหลีกเลี่ยงการจอง String (Zero String Allocation)
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int dashHash = Animator.StringToHash("Dash");

    private bool wasSprinting = false; // ตัวแปรสำหรับเช็คสถานะการวิ่ง เพื่อลดการประมวลผลซ้ำ

    private void Awake()
    {
        // 1. แคช Components ไว้ใน Awake() ให้จบ
        controller = GetComponent<CharacterController>();
        playerCombat = GetComponent<PlayerCombat>(); // แคชไว้เพื่อเช็ค IsAttacking ทุกเฟรม
        playerHealthSystem = GetComponent<HealthSystem>(); // แคช HealthSystem ไว้เรียก ActivateIFrame
        hasGroundCheck = groundCheck != null;
        
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // (ย้ายการเช็ค Camera ออกไปเพื่อให้สามารถรับค่า Camera กลางเกมตอนสลับร่างได้)

        // 3. แคชการคำนวณคณิตศาสตร์ที่ตายตัวไว้ล่วงหน้า
        CalculateJumpVelocity();
        CalculateDashCurveArea();
    }

    private void Start()
    {
        if (cameraTransform != null)
        {
            Vector3 euler = cameraTransform.eulerAngles;
            currentX = euler.y;
            currentY = euler.x;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SyncDashDurationWithAnimation();
    }

    private void SyncDashDurationWithAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            string clipName = clip.name.ToLower();
            // หาความยาวของแอนิเมชันที่น่าจะเป็น Dash
            if (clipName.Contains("dash") || clipName.Contains("roll") || clipName.Contains("dodge"))
            {
                dashAnimationClipLength = clip.length; // เก็บความยาวต้นฉบับไว้ ไม่ทับค่าใน Inspector
                break;
            }
        }
    }

    // หากมีการปรับค่าความสูงกระโดดใน Inspector ขณะเล่น ให้คำนวณใหม่
    private void OnValidate()
    {
        CalculateJumpVelocity();
        CalculateDashCurveArea();
    }

    private void CalculateJumpVelocity()
    {
        calculatedJumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void CalculateDashCurveArea()
    {
        dashCurveArea = 0f;
        if (dashCurve == null)
        {
            dashCurveArea = 1f;
            return;
        }
        
        int steps = 20;
        for (int i = 0; i < steps; i++)
        {
            float t1 = (float)i / steps;
            float t2 = (float)(i + 1) / steps;
            float v1 = dashCurve.Evaluate(t1);
            float v2 = dashCurve.Evaluate(t2);
            dashCurveArea += (v1 + v2) / 2f * (1f / steps);
        }
        
        if (dashCurveArea <= 0.01f) dashCurveArea = 1f;
    }

    // ฟังก์ชันสำหรับรับสืบทอดกล้องตอนสลับร่าง
    public void SetupCamera(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
        
        // รีเซ็ตค่าการหมุนให้เข้ากับมุมกล้องปัจจุบัน
        if (cameraTransform != null)
        {
            Vector3 euler = cameraTransform.eulerAngles;
            currentX = euler.y;
            currentY = euler.x;
        }
    }

    /// <summary>
    /// คำนวณตำแหน่งและทิศทางกล้องปลายทาง (ไม่ได้ย้ายกล้องจริง)
    /// ใช้สำหรับ PossessionManager ในการ Lerp กล้องไปยังจุดหมาย
    /// </summary>
    public void GetCameraDestination(out Vector3 destPosition, out Quaternion destRotation)
    {
        Vector3 targetPosition = cameraTarget != null ? cameraTarget.position : (transform.position + targetOffset);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 direction = rotation * Vector3.forward;
        Vector3 desiredCameraPos = targetPosition - (direction * distance);

        // ระบบชนกำแพง: ยิง SphereCast เพื่อเช็คสิ่งกีดขวางในเลเยอร์ groundMask
        float checkRadius = 0.2f;
        Vector3 castDirection = -direction;

        if (Physics.SphereCast(targetPosition, checkRadius, castDirection, out RaycastHit hit, distance, groundMask))
        {
            destPosition = targetPosition + castDirection * Mathf.Max(0.2f, hit.distance - 0.05f);
        }
        else
        {
            destPosition = desiredCameraPos;
        }

        destRotation = rotation;
    }

    private void LateUpdate()
    {
        if (isPossessed)
        {
            HandleCamera();
        }
    }

    private void Update()
    {
        if (isPossessed)
        {
            HandleMouseLock();
            HandleDashInput(); // [เพิ่ม] รับคำสั่ง Dash
        }
        
        HandleMovement();
    }

    private void HandleDashInput()
    {
        if (isDashing)
        {
            currentDashTime += Time.deltaTime;

            if (currentDashTime >= dashDuration)
            {
                isDashing = false;
                if (animator != null)
                {
                    animator.speed = originalAnimatorSpeed; // รีเซ็ตความเร็วกลับเป็นปกติ
                }
            }
            return;
        }

        // เช็ค Input กด Dash, เช็คคูลดาวน์ และต้องเหยียบพื้นอยู่
        if (Input.GetKeyDown(dashKey) && dashCooldown.IsReady() && isGrounded)
        {
            bool isAttacking = playerCombat != null && playerCombat.IsAttacking;
            if (isAttacking) return; // ห้ามพุ่งตอนโจมตี (หรือจะให้พุ่งเพื่อ Cancel ตีก็ได้ ขึ้นอยู่กับ Design)

            StartDash();
        }
    }

    private void StartDash()
    {
        isDashing = true;
        currentDashTime = 0f;
        dashCooldown.StartCooldown();

        // ปรับความเร็วของ Animator ให้แอนิเมชันเล่นจบพร้อมกับ dashDuration พอดี
        if (animator != null)
        {
            originalAnimatorSpeed = animator.speed;
            if (dashDuration > 0f && dashAnimationClipLength > 0f)
            {
                animator.speed = dashAnimationClipLength / dashDuration;
            }
        }

        // คำนวณทิศทางพุ่งจาก Input WASD
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

        if (inputDir.sqrMagnitude >= 0.01f)
        {
            // พุ่งไปในทิศทางที่กด โดยอิงตามมุมกล้อง (ถ้าไม่มีกล้องให้ใช้อิงจากมุมตัวละครแทน)
            float cameraAngle = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraAngle;
            dashDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            
            // หันหน้าตัวละครไปทางที่พุ่งทันที
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
        }
        else
        {
            // ถ้าไม่กดอะไรเลย ให้พุ่งไปข้างหน้าตามทิศที่ตัวละครหันหน้าอยู่
            dashDirection = transform.forward;
        }

        // เปิด I-Frame ถ้ามี HealthSystem
        if (playerHealthSystem != null)
        {
            playerHealthSystem.ActivateIFrame();
        }

        // ทริกเกอร์แอนิเมชัน
        if (animator != null)
        {
            animator.SetTrigger(dashHash);
        }
    }

    private void HandleMouseLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void HandleCamera()
    {
        if (cameraTransform == null || cameraLocked) return; // ป้องกัน Error หรือล็อกตอน Smooth Transition

        if (Input.GetMouseButton(1) || Cursor.lockState == CursorLockMode.Locked)
        {
            currentX += Input.GetAxis("Mouse X") * mouseSensitivityX;
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivityY;
            currentY = Mathf.Clamp(currentY, minYAngle, maxYAngle);
        }
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            distance -= scroll * 5f;
            distance = Mathf.Clamp(distance, 2f, 15f);
        }

        Vector3 targetPosition = cameraTarget != null ? cameraTarget.position : (transform.position + targetOffset);
        
        // ใช้ Quaternion.Euler สร้าง Rotation ชั่วคราว (Struct ไม่มีผลต่อ GC)
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 direction = rotation * Vector3.forward;
        Vector3 desiredCameraPos = targetPosition - (direction * distance);

        // ระบบชนกำแพง: ป้องกันกล้องคลิปทะลุกำแพงโดยอิงจาก groundMask
        float checkRadius = 0.2f;
        Vector3 castDirection = -direction;
        Vector3 finalCameraPos;

        if (Physics.SphereCast(targetPosition, checkRadius, castDirection, out RaycastHit hit, distance, groundMask))
        {
            finalCameraPos = targetPosition + castDirection * Mathf.Max(0.2f, hit.distance - 0.05f);
        }
        else
        {
            finalCameraPos = desiredCameraPos;
        }

        cameraTransform.position = finalCameraPos + cameraShakeOffset;
        cameraTransform.rotation = rotation;
    }

    private void HandleMovement()
    {
        // 1. เช็คพื้น
        if (hasGroundCheck)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }
        else
        {
            isGrounded = controller.isGrounded;
        }

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 2. คำนวณทิศทางการเดิน (รับค่าเฉพาะตอนที่ถูกสิงร่าง)
        bool isAttacking = playerCombat != null && playerCombat.IsAttacking;
        bool isSprintingNow = false;

        if (isDashing)
        {
            // ใช้ความเร็วจาก Curve เพื่อให้การพุ่งมีความไหลลื่น
            float normalizedTime = currentDashTime / dashDuration;
            float force = dashCurve.Evaluate(normalizedTime);
            // ชดเชยกับพื้นที่ใต้กราฟ เพื่อให้ระยะทางพุ่งรวมเท่ากับ dashDistance เป๊ะๆ
            Vector3 dashMovement = dashDirection * ((dashDistance / dashDuration) * (force / dashCurveArea) * Time.deltaTime);
            controller.Move(dashMovement);

            // แจ้ง Animator ให้รู้ว่าไม่ได้เดินปกติ
            if (animator != null)
            {
                animator.SetFloat(speedHash, 0f, 0.1f, Time.deltaTime);
            }
        }
        else
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (isPossessed && !isAttacking)
            {
                horizontal = Input.GetAxisRaw("Horizontal");
                vertical = Input.GetAxisRaw("Vertical");
            }
            
            inputDirection.x = horizontal;
            inputDirection.y = 0f;
            inputDirection.z = vertical;
            inputDirection.Normalize();

            if (inputDirection.sqrMagnitude >= 0.01f)
            {
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + (cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y);
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                
                isSprintingNow = Input.GetKey(KeyCode.LeftShift);
                float currentSpeed = isSprintingNow ? runSpeed : moveSpeed;
                controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);

                if (animator != null)
                {
                    float speedParam = isSprintingNow ? 1f : 0.5f;
                    animator.SetFloat(speedHash, speedParam, 0.1f, Time.deltaTime);
                }
            }
            else
            {
                if (animator != null)
                {
                    animator.SetFloat(speedHash, 0f, 0.1f, Time.deltaTime);
                }
            }
        }

        // แจ้ง CameraFOV ให้รู้ว่ากำลังวิ่งอยู่หรือไม่ (เฉพาะร่างที่ถูกสิงและเมื่อมีการเปลี่ยนสถานะเท่านั้น ลดการเรียกใช้ทุกเฟรม)
        if (isPossessed && isSprintingNow != wasSprinting)
        {
            if (CameraFOV.Instance != null)
            {
                CameraFOV.Instance.SetSprinting(isSprintingNow);
            }
            wasSprinting = isSprintingNow;
        }

        if (animator != null)
        {
            animator.SetBool(isGroundedHash, isGrounded);
        }

        // 3. ระบบกระโดด (รับค่าเฉพาะตอนถูกสิง + ห้ามกระโดดขณะโจมตีและแดช)
        if (isPossessed && !isAttacking && !isDashing && Input.GetButtonDown("Jump") && isGrounded)
        {
            // ดึงค่าที่คำนวณไว้ล่วงหน้ามาใช้ทันที ไม่ต้องรันสูตร Mathf.Sqrt ซ้ำ
            velocity.y = calculatedJumpVelocity;
            
            if (animator != null)
            {
                animator.SetTrigger(jumpHash);
            }
        }

        // 4. แร็งโน้มถ่วง
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
