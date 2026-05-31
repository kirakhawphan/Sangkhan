using UnityEngine;
using UnityEngine.UI;

public class UIShineSweep : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("ภาพแสงที่จะให้วิ่งผ่าน (ควรเป็นลูกของ Mask)")]
    public RectTransform shineImage;

    [Header("Settings")]
    [Tooltip("ให้แสงแวบผ่านทุกๆ กี่วินาที")]
    public float intervalSeconds = 3f;
    [Tooltip("ความเร็วตอนที่แสงกำลังวิ่งผ่าน (วินาที)")]
    public float sweepDuration = 0.5f;
    
    [Header("Positions")]
    [Tooltip("ตำแหน่งเริ่มต้น (X) ตอนแสงอยู่ฝั่งซ้าย")]
    public float startX = -300f;
    [Tooltip("ตำแหน่งสิ้นสุด (X) ตอนแสงวิ่งทะลุไปฝั่งขวา")]
    public float endX = 300f;

    // --- State Variables (Zero GC) ---
    private float currentTimer = 0f;
    private bool isSweeping = false;
    private float sweepPercent = 0f;
    private Vector2 cachedPos;
    private Graphic shineGraphic;

    void Start()
    {
        if (shineImage != null)
        {
            shineGraphic = shineImage.GetComponent<Graphic>();

            // เริ่มต้นด้วยการซ่อนภาพแสงไว้ก่อน และตั้งค่าตำแหน่ง X ให้ไปจุดเริ่ม
            cachedPos = shineImage.anchoredPosition;
            cachedPos.x = startX;
            shineImage.anchoredPosition = cachedPos;
            
            // ปิดแค่ตัววาดภาพ (Graphic) เพื่อไม่ให้กระทบกับตัว Script เผื่อว่าแปะไว้ที่เดียวกัน
            if (shineGraphic != null) shineGraphic.enabled = false;
            else shineImage.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (shineImage == null) return;

        // ถ้ารอเวลาอยู่
        if (!isSweeping)
        {
            currentTimer += Time.deltaTime;
            if (currentTimer >= intervalSeconds)
            {
                // หมดเวลารอ -> เริ่มแวบแสง
                currentTimer = 0f;
                isSweeping = true;
                sweepPercent = 0f;
                
                if (shineGraphic != null) shineGraphic.enabled = true;
                else shineImage.gameObject.SetActive(true);
            }
        }
        // ถ้าแสงกำลังวิ่งอยู่
        else
        {
            sweepPercent += Time.deltaTime / sweepDuration;
            
            // อัปเดตตำแหน่งจากซ้ายไปขวา
            cachedPos = shineImage.anchoredPosition;
            cachedPos.x = Mathf.Lerp(startX, endX, sweepPercent);
            shineImage.anchoredPosition = cachedPos;

            // วิ่งจนจบแล้ว
            if (sweepPercent >= 1f)
            {
                isSweeping = false;
                
                if (shineGraphic != null) shineGraphic.enabled = false;
                else shineImage.gameObject.SetActive(false);
            }
        }
    }
}
