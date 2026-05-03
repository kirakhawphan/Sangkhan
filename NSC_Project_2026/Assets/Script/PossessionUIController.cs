using UnityEngine;

public class PossessionUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField, Tooltip("RectTransform ของไอคอน UI ที่จะให้ลอยอยู่บนหัวเป้าหมาย (เช่น รูปปุ่ม E หรือลูกศร)")]
    private RectTransform uiIconRect;
    
    [SerializeField, Tooltip("กล้องหลักที่ใช้แปลงพิกัดจาก 3D มาเป็นพิกัดหน้าจอ (ลาก Main Camera มาใส่)")]
    private Camera mainCamera;

    [Header("UI Position Settings")]
    [SerializeField, Tooltip("ชดเชยความสูงให้ UI ลอยอยู่เหนือหัวเป้าหมาย (แกน Y ในโลก 3D)")]
    private Vector3 worldOffset = new Vector3(0, 2f, 0);

    [Header("Animation Settings")]
    [SerializeField, Tooltip("ความเร็วในการหมุน (องศาต่อวินาที)")]
    private float rotationSpeed = 180f;
    
    [SerializeField, Tooltip("เวลาที่ใช้เฟดเข้า/ออก และซูม (วินาที)")]
    private float transitionDuration = 0.2f;
    
    [SerializeField, Tooltip("ขนาดใหญ่สุดตอนเพิ่งปรากฏ (จะค่อยๆ หดลงมาที่ 1)")]
    private float maxScale = 1.5f;

    [SerializeField, Tooltip("ขนาดพื้นฐานของ UI (ใช้ปรับจูนก่อนคูณกับสเกลเป้าหมาย)")]
    private float baseUIScale = 1f;

    // --- Caching Variables ---
    private Transform currentTarget;
    private Canvas parentCanvas;
    private RectTransform parentRect;
    private Transform mainCameraTransform;
    private bool isOverlayCanvas;
    private bool isWorldSpaceCanvas;
    private CanvasGroup canvasGroup;

    // --- Animation State ---
    private float transitionProgress = 0f; // 0 = หายไปสนิท, 1 = โชว์เต็มที่
    private int targetState = 0; // 0 = กำลังซ่อน, 1 = กำลังโชว์
    private float currentZRotation = 0f; // เก็บค่าองศาการหมุนสะสม
    private float cachedTargetScaleY = 1f; // แคชสเกล Y ของเป้าหมาย เพื่อปรับขนาด UI ตาม

    // --- Optimization (Zero Allocation) ---
    private static readonly Vector2 OutOfBoundsPos = new Vector2(-9999f, -9999f);
    private static readonly Vector2 BottomLeftAnchor = Vector2.zero;
    private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);

    private void Awake()
    {
        // 1. ตรวจสอบอ้างอิงอัตโนมัติหากลืมใส่ใน Inspector
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) Debug.LogError("[PossessionUIController] ไม่พบ Main Camera ในฉาก!", this);
        }
        
        // แคช Transform ของกล้องเพื่อลดภาระ Native Call
        if (mainCamera != null)
        {
            mainCameraTransform = mainCamera.transform;
        }

        if (uiIconRect == null)
        {
            Debug.LogError("[PossessionUIController] กรุณาลาก UI Icon ใส่ใน Inspector!", this);
            return;
        }

        // 2. แอบเพิ่ม CanvasGroup ให้อัตโนมัติ (จำเป็นสำหรับการทำ Fade จางเข้า/ออก)
        canvasGroup = uiIconRect.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = uiIconRect.gameObject.AddComponent<CanvasGroup>();
        }

        // 3. แคช Component ทั้งหมดตั้งแต่เริ่มเกม
        parentCanvas = uiIconRect.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            parentRect = parentCanvas.transform as RectTransform; // แคชเพื่อไม่ต้องแปลง type ทุกเฟรม
            isOverlayCanvas = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay);
            isWorldSpaceCanvas = (parentCanvas.renderMode == RenderMode.WorldSpace);
            
            // ตั้งค่า Anchor ล่วงหน้าตามชนิดของ Canvas
            if (isOverlayCanvas)
            {
                uiIconRect.anchorMin = BottomLeftAnchor;
                uiIconRect.anchorMax = BottomLeftAnchor;
            }
            else
            {
                uiIconRect.anchorMin = CenterAnchor;
                uiIconRect.anchorMax = CenterAnchor;
            }
        }

        // 4. รีเซ็ตสถานะ Animation ตอนเริ่มเกม
        transitionProgress = 0f;
        targetState = 0;
        currentZRotation = 0f;
        ApplyTransitionEffects();
        uiIconRect.gameObject.SetActive(false);
    }

    private void Update()
    {
        // ตรวจสอบเช็คตัวแปรที่จำเป็น (ไม่มีการจอง Memory ใหม่)
        if (mainCameraTransform == null || uiIconRect == null) return;

        // คอยจัดการเรื่องเฟดและซูม
        HandleTransition();

        // รันเฉพาะตอนที่กำลังแสดงอยู่ หรือ กำลังค่อยๆ เฟดหายไป
        if (transitionProgress > 0f)
        {
            // ถ้าเป้าหมายยังไม่ถูกทำลาย ให้ตามติดต่อไป
            if (currentTarget != null)
            {
                UpdateUIPosition();
            }
            
            // หมุน UI ตลอดเวลา
            HandleRotation();
        }
    }

    public void ShowUI(Transform targetTransform)
    {
        if (targetTransform == null) return;

        currentTarget = targetTransform;
        cachedTargetScaleY = targetTransform.lossyScale.y; // อัพเดทสเกลทันทีตอนเปลี่ยนเป้า
        
        if (targetState != 1)
        {
            targetState = 1;
            uiIconRect.gameObject.SetActive(true);
        }
    }

    public void HideUI()
    {
        if (targetState != 0)
        {
            // เปลี่ยนแค่ State เพื่อให้ HandleTransition() ค่อยๆ ลดความสว่างลงจนเหลือ 0
            targetState = 0;
        }
    }

    // --- ฟังก์ชันจัดการ Animation ย่อย ---
    private void HandleTransition()
    {
        if (targetState == 1 && transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            if (transitionProgress >= 1f) transitionProgress = 1f;
            ApplyTransitionEffects();
        }
        else if (targetState == 0 && transitionProgress > 0f)
        {
            transitionProgress -= Time.deltaTime / transitionDuration;
            if (transitionProgress <= 0f)
            {
                transitionProgress = 0f;
                currentTarget = null; // คืนค่าเป้าหมายเมื่อจางหายสนิท
                uiIconRect.gameObject.SetActive(false); // ปิดเกมออบเจกต์เพื่อประหยัดเฟรมเรต
            }
            ApplyTransitionEffects();
        }
    }

    private void ApplyTransitionEffects()
    {
        // 1. ทำเฟดเข้า/ออก (Alpha)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = transitionProgress;
        }
        
        // 2. ทำสเกล (ใช้ Struct ไม่เกิด Garbage)
        float transitionScale = Mathf.Lerp(maxScale, 1f, transitionProgress);
        float finalScale = transitionScale * baseUIScale * cachedTargetScaleY;
        uiIconRect.localScale = new Vector3(finalScale, finalScale, finalScale);
    }

    private void HandleRotation()
    {
        // สะสมค่าองศาการหมุน
        currentZRotation += rotationSpeed * Time.deltaTime;
        
        // ถ้าไม่ใช่ World Space ให้ประยุกต์ใช้การหมุนแกน Z เข้าไปตรงๆ
        if (!isWorldSpaceCanvas)
        {
            uiIconRect.localRotation = Quaternion.Euler(0f, 0f, currentZRotation);
        }
    }

    // --- ฟังก์ชันคำนวณตำแหน่งพิกัดระดับ Extreme Optimization ---
    private void UpdateUIPosition()
    {
        // ใช้ lossyScale ของเป้าหมายมาคูณ worldOffset
        // ตัวใหญ่ (Scale สูง) = UI ลอยสูงขึ้น, ตัวเล็ก (Scale ต่ำ) = UI ลอยต่ำลง
        cachedTargetScaleY = currentTarget.lossyScale.y; // อัพเดทสเกลทุกเฟรม (รองรับกรณีตัวละครเปลี่ยนขนาด)
        Vector3 scaledOffset = worldOffset * cachedTargetScaleY;
        
        // อัพเดทขนาด UI ตามสเกลเป้าหมายแบบ Real-time
        ApplyTransitionEffects();
        
        Vector3 worldPos = currentTarget.position + scaledOffset;
        
        // 1. ถ้าใช้ Canvas แบบ World Space
        if (isWorldSpaceCanvas)
        {
            // เอา UI ไปวางในโลก 3D จริงๆ
            uiIconRect.position = worldPos;
            
            // หันหน้าเข้าหากล้องเสมอ (Billboard) + หมุนแกน Z ไปด้วย
            uiIconRect.rotation = mainCameraTransform.rotation * Quaternion.Euler(0f, 0f, currentZRotation);
            
            return;
        }

        // 2. ถ้าใช้ Canvas แบบ Overlay หรือ Screen Space Camera
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // ตรวจสอบว่าเป้าหมายอยู่หน้ากล้อง (Z > 0)
        if (screenPos.z > 0f)
        {
            if (parentCanvas != null)
            {
                if (isOverlayCanvas)
                {
                    uiIconRect.anchoredPosition = new Vector2(screenPos.x, screenPos.y) / parentCanvas.scaleFactor;
                }
                else if (parentRect != null)
                {
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, parentCanvas.worldCamera, out Vector2 localPoint))
                    {
                        uiIconRect.anchoredPosition = localPoint;
                    }
                }
            }
        }
        else
        {
            uiIconRect.anchoredPosition = OutOfBoundsPos;
        }
    }
}
