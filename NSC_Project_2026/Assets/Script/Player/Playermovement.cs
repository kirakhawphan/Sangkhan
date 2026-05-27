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

    [Header("Animation")]
    [SerializeField] private Animator animator;

    // --- Caching Components & Variables ---
    private CharacterController controller;
    private PlayerCombat playerCombat; // แคช PlayerCombat เพื่อเช็ค IsAttacking
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

    private void Awake()
    {
        // 1. แคช Components ไว้ใน Awake() ให้จบ
        controller = GetComponent<CharacterController>();
        playerCombat = GetComponent<PlayerCombat>(); // แคชไว้เพื่อเช็ค IsAttacking ทุกเฟรม
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
        //    ถ้ากำลังโจมตี → บังคับ input เป็น 0 เพื่อล็อกการเดิน (ยังรับแรงโน้มถ่วงปกติ)
        float horizontal = 0f;
        float vertical = 0f;

        bool isAttacking = playerCombat != null && playerCombat.IsAttacking;

        if (isPossessed && !isAttacking)
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
        }
        
        inputDirection.x = horizontal;
        inputDirection.y = 0f;
        inputDirection.z = vertical;
        inputDirection.Normalize();

        bool isSprintingNow = false;

        if (inputDirection.sqrMagnitude >= 0.01f)
        {
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + (cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y);
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // นำค่าไปใส่ใน Struct แทนการสร้าง new Vector3 ใหม่
            moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            
            isSprintingNow = Input.GetKey(KeyCode.LeftShift);
            float currentSpeed = isSprintingNow ? runSpeed : moveSpeed;
            controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);

            if (animator != null)
            {
                float speedParam = isSprintingNow ? 1f : 0.5f;
                // ใช้ Hash แทน String ประหยัด Memory ฝั่งแอนิเมชันได้ 100%
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

        // แจ้ง CameraFOV ให้รู้ว่ากำลังวิ่งอยู่หรือไม่ (เฉพาะร่างที่ถูกสิง)
        if (isPossessed && CameraFOV.Instance != null)
        {
            CameraFOV.Instance.SetSprinting(isSprintingNow);
        }

        if (animator != null)
        {
            animator.SetBool(isGroundedHash, isGrounded);
        }

        // 3. ระบบกระโดด (รับค่าเฉพาะตอนถูกสิง + ห้ามกระโดดขณะโจมตี)
        if (isPossessed && !isAttacking && Input.GetButtonDown("Jump") && isGrounded)
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
