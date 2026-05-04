using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("อ้างอิงไปยังระบบเลือดของตัวละคร")]
    [SerializeField] private HealthSystem targetHealthSystem;
    
    [Tooltip("อ้างอิงไปยัง UI Image ที่ทำหน้าที่เป็นหลอดเลือด")]
    [SerializeField] private Image healthFillImage;

    [Header("Settings")]
    [Tooltip("ความเร็วในการไหลของหลอดเลือด (Smooth Drain)")]
    [SerializeField] private float smoothDrainSpeed = 5f;

    // เก็บตัวแปร Coroutine เพื่อใช้ในการหยุดการทำงานเมื่อมีค่าเลือดอัปเดตซ้อนกัน
    private Coroutine smoothDrainCoroutine;

    private void OnEnable()
    {
        // ตรวจสอบเพื่อป้องกัน NullReferenceException
        if (targetHealthSystem != null)
        {
            // Subscribe Event: สมัครรับข้อมูลเมื่อสคริปต์นี้ถูกเปิดใช้งาน
            targetHealthSystem.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (targetHealthSystem != null)
        {
            // Unsubscribe Event: ยกเลิกการรับข้อมูลเมื่อปิดสคริปต์ เพื่อป้องกัน Memory Leak
            targetHealthSystem.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void Start()
    {
        // Initialization: ตั้งค่าหลอดเลือดให้เต็มก่อน (100%) 
        // เมื่อ HealthSystem.Start() ทำงาน มันจะทำการยิง Event อัปเดตค่าเลือดปัจจุบันมาให้สคริปต์นี้โดยอัตโนมัติ
        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = 1f; 
        }
    }

    // ==================== Public API สำหรับระบบสิงร่าง ====================

    /// <summary>
    /// สลับเป้าหมายหลอดเลือดไปยัง HealthSystem ตัวใหม่
    /// ใช้เรียกจาก PossessionManager เมื่อสิงร่างสำเร็จ
    /// </summary>
    /// <param name="newHealthSystem">HealthSystem ของร่างใหม่ที่เพิ่งสิง (ส่ง null ได้ถ้าต้องการยกเลิกการติดตาม)</param>
    public void SetTargetHealthSystem(HealthSystem newHealthSystem)
    {
        // --- ขั้นตอนที่ 1: ตัดการเชื่อมต่อจากเป้าหมายเดิม ---
        // Unsubscribe Event จากตัวเก่าก่อน เพื่อป้องกัน Memory Leak
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnHealthChanged -= HandleHealthChanged;
        }

        // --- ขั้นตอนที่ 2: หยุด Coroutine Smooth Drain ที่ค้างอยู่ ---
        // ป้องกันไม่ให้ Animation หลอดเลือดของร่างเก่ายังไหลอยู่
        if (smoothDrainCoroutine != null)
        {
            StopCoroutine(smoothDrainCoroutine);
            smoothDrainCoroutine = null;
        }

        // --- ขั้นตอนที่ 3: เปลี่ยนเป้าหมายเป็นตัวใหม่ ---
        targetHealthSystem = newHealthSystem;

        // --- ขั้นตอนที่ 4: Subscribe Event ของเป้าหมายใหม่ ---
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnHealthChanged += HandleHealthChanged;

            // --- ขั้นตอนที่ 5: อัปเดตหลอดเลือดทันที (Snap) ไม่ต้องรอ Smooth ---
            // ใช้ Property เพื่อดึงค่า % เลือดปัจจุบันของร่างใหม่
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = targetHealthSystem.CurrentHealthPercentage;
            }
        }
    }

    /// <summary>
    /// ฟังก์ชันที่ทำงานทันทีเมื่อ Event OnHealthChanged ถูก Invoke จาก HealthSystem
    /// </summary>
    /// <param name="healthPercentage">ค่าเลือดที่รับมาอยู่ในช่วง 0.0 - 1.0</param>
    private void HandleHealthChanged(float healthPercentage)
    {
        // หากมี Animation หลอดเลือดอันเก่าที่กำลังไหลอยู่ ให้หยุดก่อน
        if (smoothDrainCoroutine != null)
        {
            StopCoroutine(smoothDrainCoroutine);
        }

        // เริ่ม Animation การไหลของหลอดเลือดไปหาค่าเป้าหมายใหม่
        smoothDrainCoroutine = StartCoroutine(SmoothDrainRoutine(healthPercentage));
    }

    /// <summary>
    /// Coroutine สำหรับการปรับเปลี่ยนหลอดเลือดอย่างนุ่มนวล (Smooth Drain Effect)
    /// </summary>
    /// <param name="targetPercentage">ค่าเปอร์เซ็นต์เป้าหมายที่หลอดเลือดต้องไปถึง</param>
    private IEnumerator SmoothDrainRoutine(float targetPercentage)
    {
        // ตรวจสอบว่ายังมี healthFillImage อยู่หรือไม่
        if (healthFillImage == null) yield break;

        // วนลูปทำงานตราบใดที่ค่า fillAmount ยังไม่เข้าใกล้เป้าหมาย (ใช้ค่าคลาดเคลื่อน 0.001f ป้องกันลูปไม่จบ)
        while (Mathf.Abs(healthFillImage.fillAmount - targetPercentage) > 0.001f)
        {
            // ใช้ Mathf.Lerp เพื่อค่อยๆ เลื่อนค่า fillAmount ปัจจุบันไปหาเป้าหมายอย่างนุ่มนวล
            healthFillImage.fillAmount = Mathf.Lerp(healthFillImage.fillAmount, targetPercentage, Time.deltaTime * smoothDrainSpeed);
            
            // รอให้ถึงเฟรมถัดไปแล้วค่อยทำลูปต่อ
            yield return null;
        }

        // เมื่อค่าเข้าใกล้มากๆ แล้ว ให้เซ็ตเป็นค่าเป้าหมายพอดีเพื่อความแม่นยำ
        healthFillImage.fillAmount = targetPercentage;
    }
}
