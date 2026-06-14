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
    [Tooltip("ตัวคูณความเร็วตอนเดินขณะตั้งการ์ด (เช่น 0.5 คือเดินช้าลงครึ่งนึง)")]
    [SerializeField] private float blockSpeedMultiplier = 0.5f;
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
    private readonly int inputXHash = Animator.StringToHash("InputX");
    private readonly int inputYHash = Animator.StringToHash("InputY");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
    private readonly int jumpHash = Animator.StringToHash("Jump");

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
    }

    // หากมีการปรับค่าความสูงกระโดดใน Inspector ขณะเล่น ให้คำนวณใหม่
    private void OnValidate()
    {
        CalculateJumpVelocity();
    }

    private void CalculateJumpVelocity()
    {
        calculatedJumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    public float GetCameraAngle()
    {
        return cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
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
        }
        
        HandleMovement();
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
        bool isDodging = playerCombat != null && playerCombat.IsDodging;
        bool isSprintingNow = false;

        if (isDodging)
        {
            // แจ้ง Animator ให้รู้ว่าไม่ได้เดินปกติ
            if (animator != null)
            {
                animator.SetFloat(inputXHash, 0f, 0.1f, Time.deltaTime);
                animator.SetFloat(inputYHash, 0f, 0.1f, Time.deltaTime);
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

                // ให้ตัวละครหันตามกล้องตลอดเวลาแบบค่อยๆ หมุน (Smooth)
                if (cameraTransform != null)
                {
                    float targetAngle = cameraTransform.eulerAngles.y;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }
            }
            
            inputDirection.x = horizontal;
            inputDirection.y = 0f;
            inputDirection.z = vertical;
            inputDirection.Normalize();

            if (inputDirection.sqrMagnitude >= 0.01f)
            {
                // คำนวณทิศทางการเดินอิงจากกล้อง
                if (cameraTransform != null)
                {
                    moveDirection = cameraTransform.right * inputDirection.x + cameraTransform.forward * inputDirection.z;
                }
                else
                {
                    moveDirection = transform.right * inputDirection.x + transform.forward * inputDirection.z;
                }
                
                moveDirection.y = 0f;
                moveDirection.Normalize();
                
                // ปรับความเร็วถ้ากำลังตั้งการ์ด
                bool isBlocking = playerHealthSystem != null && playerHealthSystem.IsBlocking;
                
                isSprintingNow = Input.GetKey(KeyCode.LeftShift) && !isBlocking && vertical > 0;
                
                float currentSpeed = isSprintingNow ? runSpeed : moveSpeed;
                if (isBlocking)
                {
                    currentSpeed *= blockSpeedMultiplier;
                }

                controller.Move(moveDirection * currentSpeed * Time.deltaTime);

                if (animator != null)
                {
                    float multiplier = isSprintingNow ? 1f : 0.5f;
                    
                    float animX = inputDirection.x;
                    float animZ = inputDirection.z;

                    // ถ้ากำลังเดินถอยหลังเฉียง ให้เฉลี่ยน้ำหนักไปทางถอยหลัง (Z) ประมาณ 70% ตามที่ต้องการ
                    if (animZ < -0.1f && Mathf.Abs(animX) > 0.1f)
                    {
                        // บีบค่า X ลง เพื่อให้องศาการ Blend เอียงไปหาท่าเดินถอยหลัง (แกน Z) มากกว่าท่าเดินซ้าย/ขวา
                        animX *= 0.45f; 
                        
                        // ปรับ Normalize กลับเพื่อให้ความยาวเวกเตอร์เท่าเดิม (กันท่า Idle ผสม)
                        Vector2 skewed = new Vector2(animX, animZ).normalized;
                        animX = skewed.x;
                        animZ = skewed.y;
                    }

                    animator.SetFloat(inputXHash, animX * multiplier, 0.1f, Time.deltaTime);
                    animator.SetFloat(inputYHash, animZ * multiplier, 0.1f, Time.deltaTime);
                }
            }
            else
            {
                if (animator != null)
                {
                    animator.SetFloat(inputXHash, 0f, 0.1f, Time.deltaTime);
                    animator.SetFloat(inputYHash, 0f, 0.1f, Time.deltaTime);
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

        // 3. ระบบกระโดด (รับค่าเฉพาะตอนถูกสิง + ห้ามกระโดดขณะโจมตีและหลบ)
        if (isPossessed && !isAttacking && !isDodging && Input.GetButtonDown("Jump") && isGrounded)
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
