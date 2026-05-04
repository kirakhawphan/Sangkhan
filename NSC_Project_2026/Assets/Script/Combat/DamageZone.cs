using UnityEngine;

// พื้นที่อันตราย: เมื่อตัวละครเหยียบจะได้รับดาเมจต่อเนื่อง (Damage Over Time)
// ใช้ร่วมกับระบบ IDamageable / HealthSystem ที่มีอยู่แล้ว
public class DamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("ดาเมจต่อครั้ง")]
    [SerializeField] private float damagePerTick = 10f;

    [Tooltip("ระยะเวลาระหว่างแต่ละครั้งที่โดนดาเมจ (วินาที)")]
    [SerializeField] private float tickInterval = 0.5f;

    [Tooltip("ประเภทดาเมจ (Combat = สั่นกล้อง, System = ไม่สั่น, Poison = พิษ)")]
    [SerializeField] private DamageType damageType = DamageType.System;

    // ตัวจับเวลาภายใน เพื่อควบคุมว่าจะโดนดาเมจทุกกี่วินาที
    private float tickTimer;

    private void OnTriggerEnter(Collider other)
    {
        // โดนทันทีเมื่อเหยียบเข้ามา ไม่ต้องรอ Tick แรก
        TryDealDamage(other);
        tickTimer = tickInterval; // รีเซ็ตตัวจับเวลา
    }

    private void OnTriggerStay(Collider other)
    {
        // นับเวลาลง
        tickTimer -= Time.deltaTime;

        if (tickTimer <= 0f)
        {
            TryDealDamage(other);
            tickTimer = tickInterval; // รีเซ็ตหลังจากยิงดาเมจ
        }
    }

    /// <summary>
    /// พยายามส่งดาเมจไปยังเป้าหมายที่อยู่ในโซน
    /// ใช้ GetComponentInParent เพื่อรองรับกรณี Collider อยู่บน Child Object
    /// </summary>
    private void TryDealDamage(Collider other)
    {
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        DamageInfo info = new DamageInfo
        {
            damageAmount = damagePerTick,
            damageType = this.damageType, // ใช้ค่าจาก Inspector
            hitPoint = other.ClosestPoint(transform.position),
            knockbackForce = Vector3.zero, // พื้นไม่มีแรงกระเด็น
            attacker = gameObject
        };

        target.TakeDamage(info);
    }
}
