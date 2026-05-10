using System.Collections;
using UnityEngine;

// ระบบกล้องสั่นเมื่อโดนโจมตี — Subscribe เฉพาะ OnDamageTaken แล้วเช็ค DamageType
// แปะสคริปต์นี้ไว้ที่ Main Camera หรือ GameObject ใดก็ได้
//
// หลักการ: ไม่แตะตำแหน่งกล้องเอง แต่เขียนค่า shakeOffset ลงใน Playermovement.cameraShakeOffset
// แล้วให้ Playermovement บวกเพิ่มตอนเซ็ตกล้องใน LateUpdate → ไม่มีปัญหา Execution Order
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("Shake Settings")]
    [Tooltip("ความแรงของการสั่น (ยิ่งมากยิ่งสั่นแรง)")]
    [SerializeField] private float shakeIntensity = 0.15f;

    [Tooltip("ระยะเวลาที่กล้องสั่น (วินาที)")]
    [SerializeField] private float shakeDuration = 0.2f;

    [Header("References")]
    [Tooltip("HealthSystem ของตัวละครที่ต้องการติดตาม (ลากมาใส่)")]
    [SerializeField] private HealthSystem targetHealthSystem;

    [Tooltip("Playermovement ของตัวละครปัจจุบัน (ลากมาใส่)")]
    [SerializeField] private Playermovement targetPlayermovement;

    // เก็บ Coroutine เพื่อหยุดการสั่นซ้อนกัน
    private Coroutine shakeCoroutine;

    private void OnEnable()
    {
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken -= HandleDamageTaken;
        }
    }

    // ==================== Public API สำหรับระบบสิงร่าง ====================

    /// <summary>
    /// สลับเป้าหมายไปยังร่างใหม่ (เรียกจาก PossessionManager เมื่อสิงร่าง)
    /// </summary>
    public void SetTarget(HealthSystem newHealthSystem, Playermovement newPlayermovement)
    {
        // --- รีเซ็ต offset ของร่างเก่า ---
        if (targetPlayermovement != null)
        {
            targetPlayermovement.cameraShakeOffset = Vector3.zero;
        }

        // หยุด Coroutine เก่าที่ค้างอยู่
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        // --- Unsubscribe Event จากตัวเก่า ---
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken -= HandleDamageTaken;
        }

        // --- เปลี่ยนเป้าหมาย ---
        targetHealthSystem = newHealthSystem;
        targetPlayermovement = newPlayermovement;

        // --- Subscribe Event ตัวใหม่ ---
        if (targetHealthSystem != null)
        {
            targetHealthSystem.OnDamageTaken += HandleDamageTaken;
        }
    }

    // ==================== Event Handler ====================

    private void HandleDamageTaken(float damageAmount, DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Combat:
                // โดนศัตรูตี → สั่นกล้อง!
                TriggerShake();
                break;

            case DamageType.System:
            case DamageType.Poison:
                // ดาเมจจากระบบ/พิษ → ไม่สั่นกล้อง
                break;

            case DamageType.FallDamage:
                // ตกจากที่สูง → สั่นกล้อง
                TriggerShake();
                break;
        }
    }

    // ==================== Shake Logic ====================

    public void TriggerShake(float customIntensity = 0f, float customDuration = 0f)
    {
        // ต้องมี Playermovement เป้าหมายก่อนถึงจะสั่นได้
        if (targetPlayermovement == null) return;

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        float finalIntensity = customIntensity > 0f ? customIntensity : shakeIntensity;
        float finalDuration = customDuration > 0f ? customDuration : shakeDuration;

        shakeCoroutine = StartCoroutine(ShakeRoutine(finalIntensity, finalDuration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // คำนวณ offset สั่น — ความแรงค่อยๆ ลดลงตามเวลา
            float remainingRatio = 1f - (elapsed / duration);
            float currentIntensity = intensity * remainingRatio;

            // เขียน offset ลงใน Playermovement → มันจะบวกเพิ่มตอนเซ็ตกล้องเอง
            if (targetPlayermovement != null)
            {
                targetPlayermovement.cameraShakeOffset = new Vector3(
                    Random.Range(-currentIntensity, currentIntensity),
                    Random.Range(-currentIntensity, currentIntensity),
                    0f
                );
            }

            // [Game Feel] ใช้ unscaledDeltaTime เพื่อให้กล้องยังสั่นเร็วปกติแม้เวลาในเกมหยุด (Hit Stop)
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // รีเซ็ต offset เมื่อสั่นเสร็จ
        if (targetPlayermovement != null)
        {
            targetPlayermovement.cameraShakeOffset = Vector3.zero;
        }

        shakeCoroutine = null;
    }
}
