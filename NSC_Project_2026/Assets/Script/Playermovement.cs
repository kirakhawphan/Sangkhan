using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Playermovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float runSpeed = 10f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;

    [Header("Camera Control")]
    public Transform cameraTransform;
    public Transform cameraTarget; // จุดอ้างอิงให้กล้องหมุนรอบ (ใส่เป็นกระดูกไหล่/หน้าอกได้) ถ้าว่างไว้จะใช้จุดกึ่งกลางโมเดล + targetOffset
    public float mouseSensitivityX = 3f;
    public float mouseSensitivityY = 3f;
    public float distance = 5f;
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0); // จุดกึ่งกลางที่กล้องจะมอง 
    
    private float currentX = 0f;
    private float currentY = 0f;
    public float minYAngle = -20f;
    public float maxYAngle = 80f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool hasGroundCheck; // เก็บค่าบูลีนไว้ใช้ครั้งเดียวเพื่อความเร็ว

    void Start()
    {
        controller = GetComponent<CharacterController>();
        hasGroundCheck = groundCheck != null;
        
        // ❌ เอา Camera.main ออกไปเลย เพื่อลด Overhead ตอนเริ่มเกม (GameObject.FindObjectWithTag)
        // ✅ บังคับให้ตั้งค่าอ้างอิงผ่าน Inspector โดยตรง 
        if (cameraTransform == null)
        {
            Debug.LogError("Playermovement: กรุณาลากใส่ Camera ในช่อง Camera Transform ผ่าน Inspector!");
            enabled = false; // ปิดการทำงานสคริปต์นี้ถ้าไม่ได้ลากใส่
            return;
        }

        Vector3 euler = cameraTransform.eulerAngles;
        currentX = euler.y;
        currentY = euler.x;

        // ล็อกเมาส์ทันทีเมื่อเริ่มเกม
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        HandleCamera();
    }

    void Update()
    {
        // กดปุ่ม ESC เพื่อสลับการล็อก/ปลดล็อกเมาส์
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

        HandleMovement();
    }

    void HandleCamera()
    {
        // ตัดบรรทัด if (cameraTransform == null) return; ทิ้ง เพราะเช็คใน Start และปิด Script ไปแล้ว

        // หมุนกล้องเมื่อกดคลิกขวาค้าง หรือในขณะที่เมาส์ถูกล็อกอยู่ (CursorLockMode.Locked)
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

        // ถ้ามีการกำหนดจุด cameraTarget พิเศษ ให้ใช้จุดนั้น ถ้าไม่มีให้ใช้ตำแหน่งตัวละครบวกด้วย Offset
        Vector3 targetPosition = cameraTarget != null ? cameraTarget.position : (transform.position + targetOffset);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        
        cameraTransform.position = targetPosition - (rotation * Vector3.forward * distance);
        cameraTransform.rotation = rotation;
    }

    void HandleMovement()
    {
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

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // ✅ ประหยัด Performance: เช็คความยาวด้วย sqrMagnitude แทน magnitude เพราะไม่ต้องถอด Root (Math.Sqrt)
        if (direction.sqrMagnitude >= 0.01f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : moveSpeed;
            controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // ทำการหาค่าแรงกระโดด สามารถปรับไปทำใน Inspector ได้ถ้าต้องการให้เร็วขึ้นไปอีก
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
