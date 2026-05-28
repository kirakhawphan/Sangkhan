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
}
