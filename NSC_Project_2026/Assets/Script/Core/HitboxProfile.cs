using UnityEngine;

[CreateAssetMenu(fileName = "New Hitbox Profile", menuName = "Combat/Hitbox Profile")]
public class HitboxProfile : ScriptableObject
{
    [Header("Damage Settings")]
    [Tooltip("ดาเมจพื้นฐาน")]
    public float attackDamage = 20f;
    
    [Tooltip("ดาเมจทำลายเกราะ (ความถึก)")]
    public float poiseDamage = 10f;
    
    [Tooltip("ประเภทของดาเมจ (บอกว่าใครตีหรือธาตุอะไร)")]
    public DamageType damageType = DamageType.Combat;
    
    [Tooltip("ความแรงของการกระเด็น")]
    public float knockbackPower = 5f;

    [Header("Game Feel Settings (Hit Impact)")]
    [Tooltip("โปรไฟล์ความแรงของการกระทบ (สั่นกล้อง, Hit Stop) เฉพาะของท่าโจมตีนี้")]
    public ImpactProfile impactProfile;

    [Header("VFX Settings")]
    [Tooltip("พาร์ทิเคิลเอฟเฟกต์ตอนตีโดน (จะเกิดตรงจุดที่ปะทะพอดี)")]
    public GameObject hitEffectPrefab;
    
    [Tooltip("ตัวคูณขนาดของเอฟเฟกต์ (1 = ขนาดปกติ, 0.5 = เล็กลงครึ่งนึง, 2 = ใหญ่เป็นสองเท่า)")]
    public float hitEffectScale = 1f;

    [Tooltip("ความเร็วในการเล่นเอฟเฟกต์ (1 = ปกติ, 2 = เร็วขึ้นสองเท่า, 0.5 = สโลว์โมชั่น)")]
    public float hitEffectSpeed = 1f;
}
