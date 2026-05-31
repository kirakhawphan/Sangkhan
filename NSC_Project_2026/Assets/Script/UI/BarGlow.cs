using UnityEngine;
using UnityEngine.UI;

public class HealthBarGlow : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("ใส่รูปหลอดเลือดของคุณ (ตัวที่ตั้งเป็น Filled)")]
    public Image healthBarFill;
    
    [Tooltip("ใส่รูปลายเส้นเรืองแสง (ลาก Object ลงมาใส่ช่องนี้)")]
    public RectTransform glowEdge;

    [Header("Settings")]
    [Tooltip("ถ้าเลือดเต็ม 100% หรือเหลือ 0% ให้ซ่อนเส้นเรืองแสงไหม?")]
    public bool hideAtExtremes = true;

    [Header("Smooth Movement (ขยับแบบสมูท)")]
    [Tooltip("เปิดใช้การเลื่อนแบบสมูทไหม?")]
    public bool useSmoothMovement = true;
    [Tooltip("ความเร็วในการเลื่อนตามหลอดเลือด (ค่ายิ่งเยอะยิ่งตามเร็ว)")]
    public float lerpSpeed = 10f;

    [Header("Pulsing Effect (เอฟเฟกต์กระพริบ/เต้น)")]
    [Tooltip("เปิดเอฟเฟกต์ให้เส้นเรืองแสงเต้นขยายเข้าออกไหม?")]
    public bool usePulseEffect = true;
    public float pulseSpeed = 5f;
    public float pulseSize = 1.2f;

    [Header("Alignment Tweaks (ปรับตำแหน่งให้ตรงเป๊ะ)")]
    [Tooltip("ถ้าเส้นมันเบี้ยวไปซ้ายขวา สามารถเลื่อนตัวเลขนี้เพื่อชดเชยได้ครับ")]
    public float offsetX = 0f;
    [Tooltip("ถ้าเส้นมันเบี้ยวขึ้นลง สามารถเลื่อนตัวเลขนี้เพื่อชดเชยได้ครับ")]
    public float offsetY = 0f;

    // --- Caching Components (Zero GC & Reduced C++ Interop Overhead) ---
    private RectTransform fillRectTransform;
    private Transform glowParent;
    private GameObject glowGameObject;

    private float initialY;
    private float currentLocalX;
    private float targetLocalX;
    
    private float lastFillAmount = -1f;
    private float lastRectWidth = -1f;
    private bool isGlowActive;

    private Vector3 originalScale;

    void Start()
    {
        if (glowEdge != null)
        {
            initialY = glowEdge.anchoredPosition.y;
            originalScale = glowEdge.localScale;
            glowParent = glowEdge.parent;
            glowGameObject = glowEdge.gameObject;
            isGlowActive = glowGameObject.activeSelf;
            
            if (healthBarFill != null)
            {
                fillRectTransform = healthBarFill.rectTransform;
                lastFillAmount = healthBarFill.fillAmount;
                lastRectWidth = fillRectTransform.rect.width;

                targetLocalX = CalculateTargetLocalX(lastFillAmount);
                currentLocalX = targetLocalX;
                glowEdge.anchoredPosition = new Vector2(currentLocalX + offsetX, initialY + offsetY);
            }
        }
    }

    void LateUpdate()
    {
        if (healthBarFill == null || glowEdge == null) return;

        float fillPercentage = healthBarFill.fillAmount;
        float currentWidth = fillRectTransform.rect.width;

        // 1. คำนวณเป้าหมายใหม่เฉพาะตอนที่เลือดลด/เพิ่ม หรือขนาดจอ (Width) เปลี่ยน
        // (ลดภาระ CPU ไม่ให้รัน TransformPoint ทุกเฟรมโดยไม่จำเป็น)
        if (fillPercentage != lastFillAmount || currentWidth != lastRectWidth)
        {
            lastFillAmount = fillPercentage;
            lastRectWidth = currentWidth;
            targetLocalX = CalculateTargetLocalX(fillPercentage);
        }

        // 2. จัดการขยับตำแหน่ง
        bool needsPositionUpdate = false;

        if (useSmoothMovement)
        {
            if (Mathf.Abs(currentLocalX - targetLocalX) > 0.001f)
            {
                currentLocalX = Mathf.Lerp(currentLocalX, targetLocalX, Time.deltaTime * lerpSpeed);
                needsPositionUpdate = true;
            }
            else if (currentLocalX != targetLocalX)
            {
                // Snap เข้าเป้าหมายให้เป๊ะถ้าระยะห่างเหลือน้อยมากๆ
                currentLocalX = targetLocalX;
                needsPositionUpdate = true;
            }
        }
        else
        {
            if (currentLocalX != targetLocalX)
            {
                currentLocalX = targetLocalX;
                needsPositionUpdate = true;
            }
        }

        // อัปเดตตำแหน่งเฉพาะตอนที่มีการเปลี่ยนแปลงจริงๆ 
        // (สำคัญมาก: การเซ็ต anchoredPosition พร่ำเพรื่อจะสั่งให้ Canvas Rebuild ซึ่งกิน CPU สูงมาก)
        if (needsPositionUpdate)
        {
            glowEdge.anchoredPosition = new Vector2(currentLocalX + offsetX, initialY + offsetY);
        }

        // 3. เอฟเฟกต์กระพริบ/เต้น (Pulsing)
        if (usePulseEffect)
        {
            // Vector3 และ Mathf ทำงานบน Stack ทั้งหมด จึงเป็น Zero GC 100%
            float scalePingPong = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseSize - 1f);
            glowEdge.localScale = originalScale * scalePingPong;
        }

        // 4. จัดการเปิด/ปิด GameObject แบบ Caching State 
        // ลด Overhead จากการเรียก .activeSelf (C++ Interop) ทุกเฟรม
        if (hideAtExtremes)
        {
            bool shouldShow = fillPercentage > 0.01f && fillPercentage < 0.99f;
            if (isGlowActive != shouldShow)
            {
                isGlowActive = shouldShow;
                glowGameObject.SetActive(shouldShow);
            }
        }
    }

    // ฟังก์ชันคำนวณตำแหน่งผ่าน World Space (Zero GC Allocation)
    private float CalculateTargetLocalX(float fillPercentage)
    {
        Rect rect = fillRectTransform.rect;
        float fillEdgeLocalX = rect.xMin + (rect.width * fillPercentage);

        // new Vector3 เป็น Struct allocation บน Stack ไม่มี GC 발생
        Vector3 edgeLocalPos = new Vector3(fillEdgeLocalX, 0f, 0f);
        Vector3 worldPos = fillRectTransform.TransformPoint(edgeLocalPos);

        if (glowParent != null)
        {
            return glowParent.InverseTransformPoint(worldPos).x;
        }
        return worldPos.x;
    }
}

