using UnityEngine;

// ระบบเลือดที่เป็นอิสระ (Decoupled) ไม่ผูกกับคลาส Player หรือ Enemy โดยตรง
public class HealthSystem : MonoBehaviour, IDamageable
{
    [Header("Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float maxPoise = 20f; // [เพิ่ม] ความถึกสูงสุดก่อนจะชะงัก

    [Header("Debug (ดูค่าได้ตอน Play Mode)")]
    [SerializeField] private float currentHealth;
    [SerializeField] private float healthPercentage;
    [SerializeField] private float currentPoise; // [เพิ่ม] ค่าความถึกปัจจุบัน

    // อีเวนต์สำหรับส่งค่า % เลือด (0.0 ถึง 1.0) ไปให้ UI หรือระบบอื่นที่ติดตามอยู่
    public event System.Action<float> OnHealthChanged;
    // อีเวนต์สำหรับแจ้งเตือนเมื่อเป้าหมายตาย
    public event System.Action OnDeath;

    // อีเวนต์เฉพาะตอนโดนดาเมจ — ส่งทั้งจำนวนดาเมจและประเภท
    // ใช้สำหรับระบบที่ต้องการแยกประเภทดาเมจ เช่น CameraShake, HitFlash, Sound
    public event System.Action<float, DamageType> OnDamageTaken;

    // [เพิ่ม] สำหรับแจ้ง AI/Player ให้เล่นท่าชะงัก (ส่ง knockbackForce ไปด้วย)
    public event System.Action<Vector3> OnHurt;
    
    // [เพิ่ม] สำหรับเล่นเอฟเฟกต์เกราะแตก
    public event System.Action OnPoiseBroken;

    private bool isDead = false;
    private Playermovement playerMovementCache; // แคชไว้ตรวจสอบสถานะการสิงร่าง

    // Property สาธารณะ เพื่อให้สคริปต์ภายนอก (เช่น HealthBarUI) ดึงค่า % เลือดปัจจุบันได้ทันที
    // ใช้ตอนสลับเป้าหมาย (Possession) เพื่ออัปเดตหลอดเลือดแบบไม่ต้องรอ Event
    public float CurrentHealthPercentage => GetHealthPercentage();

    private void Awake()
    {
        // กำหนดเลือดให้เต็มเมื่อเริ่มต้น
        currentHealth = maxHealth;
        healthPercentage = GetHealthPercentage();

        // [เพิ่ม] กำหนดค่า Poise ให้เต็มเมื่อเริ่มต้น
        currentPoise = maxPoise;

        // แคช Playermovement ไว้ตรวจสอบว่าเป็นผู้เล่นหรือไม่ (ใช้ทำ Super Armor)
        playerMovementCache = GetComponent<Playermovement>();
    }

    private void Start()
    {
        // ส่งอีเวนต์ค่าเลือดเริ่มต้น เพื่อให้ UI อัปเดตเมื่อเริ่มเกม
        OnHealthChanged?.Invoke(GetHealthPercentage());
    }

    // ฟังก์ชันรับดาเมจตามข้อบังคับของ IDamageable
    public void TakeDamage(DamageInfo info)
    {
        if (isDead) return;

        // ลดเลือดตามจำนวนดาเมจ แล้ว Clamp ไว้ในช่วง [0, maxHealth]
        currentHealth = Mathf.Clamp(currentHealth - info.damageAmount, 0f, maxHealth);

        // อัปเดตค่า Debug ใน Inspector
        healthPercentage = GetHealthPercentage();

        // ยิงอีเวนต์แจ้งเตือนว่าเลือดเปลี่ยนไปเท่าไหร่แล้ว (สำหรับ UI หลอดเลือด)
        OnHealthChanged?.Invoke(healthPercentage);

        // ยิงอีเวนต์แจ้งรายละเอียดดาเมจ (สำหรับ CameraShake, HitFlash และอื่นๆ)
        OnDamageTaken?.Invoke(info.damageAmount, info.damageType);

        // ตรวจสอบการตาย
        if (currentHealth <= 0f)
        {
            Die();
            return; // Guard Clause ป้องกันการเกิด Poise Broken พร้อมกับตาย
        }

        // --- [เพิ่ม] ลอจิกระบบ Poise (ชะงัก) ---
        // เช็คว่าถ้าเป็นผู้เล่นกำลังควบคุมร่างนี้อยู่ (isPossessed) จะได้รับ Super Armor ทันที (ไม่ชะงัก)
        if (playerMovementCache != null && playerMovementCache.isPossessed) return;

        // หักค่า Poise ปัจจุบันด้วยดาเมจ Poise ที่ได้รับ
        currentPoise -= info.poiseDamage;

        // Guard Clause: ถ้าเกราะยังไม่แตก (Poise > 0) ให้ออกจากฟังก์ชันไปเลย ไม่ต้องชะงัก
        if (currentPoise > 0) return;

        // เมื่อเกราะแตก (Poise <= 0): ให้รีเซ็ตค่ากลับให้เต็ม
        currentPoise = maxPoise;

        // ยิง Event แจ้งเอฟเฟกต์เกราะแตก และสั่งให้ตัวละครชะงัก
        OnPoiseBroken?.Invoke();
        OnHurt?.Invoke(info.knockbackForce);
    }

    /// <summary>
    /// ฟังก์ชันฮีลเลือด — Clamp ไว้ไม่ให้เกิน maxHealth
    /// เรียกใช้จากสคริปต์ภายนอกได้เลย เช่น HealZone, Potion, Skill
    /// </summary>
    /// <param name="amount">จำนวนเลือดที่จะฮีล (ค่าบวก)</param>
    public void Heal(float amount)
    {
        if (isDead) return;

        // เพิ่มเลือด แล้ว Clamp ไว้ไม่ให้เกิน maxHealth
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);

        // อัปเดตค่า Debug ใน Inspector
        healthPercentage = GetHealthPercentage();

        // ยิงอีเวนต์ให้ UI อัปเดตหลอดเลือด
        OnHealthChanged?.Invoke(healthPercentage);
    }

    private void Die()
    {
        isDead = true;
        
        // ยิงอีเวนต์เมื่อตาย เพื่อให้สคริปต์อื่นมาเกาะ (เช่น เล่นแอนิเมชันตาย, ดรอปของ)
        OnDeath?.Invoke();
    }

    // ฟังก์ชันช่วยเหลือสำหรับคำนวณ % เลือด เพื่อความสะดวก
    private float GetHealthPercentage()
    {
        if (maxHealth <= 0) return 0f;
        return currentHealth / maxHealth;
    }
}
