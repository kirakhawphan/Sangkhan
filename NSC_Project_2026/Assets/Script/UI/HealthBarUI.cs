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

    // เป้าหมายของหลอดเลือดที่ต้องการจะ Lerp ไปหา
    private float targetPercentage = 1f;

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

        // --- ขั้นตอนที่ 2: ยกเลิกระบบ Smooth Drain เดิม ---
        // ไม่จำเป็นต้องปิด Coroutine แล้ว แค่เซ็ตเป้าหมายใหม่แทน

        // --- ขั้นตอนที่ 3: เปลี่ยนเป้าหมายเป็นตัวใหม่ ---
        targetHealthSystem = newHealthSystem;

        // --- ขั้นตอนที่ 4: Subscribe Event ของเป้าหมายใหม่ ---
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnHealthChanged += HandleHealthChanged;

            // --- ขั้นตอนที่ 5: อัปเดตหลอดเลือดทันที (Snap) ไม่ต้องรอ Smooth ---
            // ใช้ Property เพื่อดึงค่า % เลือดปัจจุบันของร่างใหม่
            // [แก้บั๊ก] ต้องอัปเดต targetPercentage ด้วย ไม่งั้น Update() จะ Lerp กลับไปค่าเลือดร่างเก่า!
            float newPercentage = targetHealthSystem.CurrentHealthPercentage;
            targetPercentage = newPercentage;

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = newPercentage;
            }
        }
    }

    /// <summary>
    /// ฟังก์ชันที่ทำงานทันทีเมื่อ Event OnHealthChanged ถูก Invoke จาก HealthSystem
    /// </summary>
    /// <param name="healthPercentage">ค่าเลือดที่รับมาอยู่ในช่วง 0.0 - 1.0</param>
    private void HandleHealthChanged(float healthPercentage)
    {
        // เซ็ตเป้าหมายใหม่ให้ Update() จัดการต่อ (Zero GC)
        targetPercentage = healthPercentage;
    }

    private void Update()
    {
        if (healthFillImage == null) return;

        // วนทำงานใน Update แทน Coroutine เพื่อประหยัด Garbage
        if (Mathf.Abs(healthFillImage.fillAmount - targetPercentage) > 0.001f)
        {
            healthFillImage.fillAmount = Mathf.Lerp(healthFillImage.fillAmount, targetPercentage, Time.deltaTime * smoothDrainSpeed);
        }
        else
        {
            // ป้องกันการ Lerp ค้างไม่จบ
            healthFillImage.fillAmount = targetPercentage;
        }
    }
}
