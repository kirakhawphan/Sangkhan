using System.Collections;
using UnityEngine;

// ระบบกล้องสั่นเมื่อโดนโจมตี — ปรับตาม ImpactProfile (ScriptableObject) ของอาวุธ
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

    // ==================== Inspector Settings ====================

    [Header("Default Shake Settings")]
    [Tooltip("ความแรงเริ่มต้นของการสั่น (ใช้เมื่อไม่มี ImpactProfile)")]
    [SerializeField] private float defaultIntensity = 0.15f;

    [Tooltip("ระยะเวลาเริ่มต้นที่กล้องสั่น (วินาที)")]
    [SerializeField] private float defaultDuration = 0.2f;

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

    private void HandleDamageTaken(DamageInfo info)
    {
        if (info.impactProfile != null)
        {
            if (info.impactProfile.enableReceiverShake)
            {
#if UNITY_EDITOR
                Debug.Log($"[CameraShake] Receiver Shake triggered via profile. Intensity: {info.impactProfile.receiverShakeIntensity}, Duration: {info.impactProfile.receiverShakeDuration}");
#endif
                TriggerShake(info.impactProfile.receiverShakeIntensity, info.impactProfile.receiverShakeDuration);
            }
        }
        else
        {
            // Fallback เฉพาะดาเมจพิเศษที่ไม่ใช่มอนสเตอร์ตีปกติ (เช่น ตกจากที่สูง)
            if (info.damageType == DamageType.FallDamage)
            {
                TriggerShake(defaultIntensity * 1.5f, defaultDuration * 1.5f);
            }
        }
    }

    // ==================== Public API สำหรับผู้โจมตี (เมื่อฟันโดนศัตรู) ====================

    /// <summary>
    /// สั่งสั่นกล้องสำหรับฝ่ายผู้โจมตี (เรียกเมื่อผู้เล่นฟันโดนเป้าหมาย)
    /// </summary>
    public void TriggerAttackerShake(ImpactProfile profile)
    {
        if (profile == null) return;

        if (profile.enableAttackerShake)
        {
#if UNITY_EDITOR
            Debug.Log($"[CameraShake] Attacker Shake triggered via profile. Intensity: {profile.attackerShakeIntensity}, Duration: {profile.attackerShakeDuration}");
#endif
            TriggerShake(profile.attackerShakeIntensity, profile.attackerShakeDuration);
        }
    }

    // ==================== Shake Logic ====================

    /// <summary>
    /// สั่งสั่นกล้องด้วยค่า Default จาก Inspector
    /// </summary>
    public void TriggerShake()
    {
        TriggerShake(defaultIntensity, defaultDuration);
    }

    /// <summary>
    /// สั่งสั่นกล้องพร้อมกำหนดค่าเอง (เรียกจากระบบอื่นได้อิสระ เช่น Explosion, Possession, Skill)
    /// </summary>
    public void TriggerShake(float customIntensity, float customDuration)
    {
        // ต้องมี Playermovement เป้าหมายก่อนถึงจะสั่นได้
        if (targetPlayermovement == null) return;

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = StartCoroutine(ShakeRoutine(customIntensity, customDuration));
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
