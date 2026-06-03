using UnityEngine;
using Skills.Core;

namespace Skills.Effects
{
    /// <summary>
    /// สกิลยิงกระสุน: Instantiate Prefab กระสุนแล้วปล่อยบินไป
    /// </summary>
    [CreateAssetMenu(fileName = "New Projectile Effect", menuName = "Skills/Effects/Projectile")]
    public class ProjectileEffect : SkillEffect
    {
        [Header("Projectile Settings")]
        [Tooltip("Prefab ของกระสุน (ต้องมี ProjectileBullet.cs + Rigidbody + Collider [isTrigger])")]
        public GameObject projectilePrefab;

        public float speed = 20f;
        public float lifetime = 5f;

        [Header("Spawn Position")]
        public float spawnHeight = 1.2f;
        public float spawnForwardOffset = 1f;

        [Header("Damage")]
        public float damage = 60f;
        public float poiseDamage = 15f;
        public float knockbackPower = 6f;
        public DamageType damageType = DamageType.Combat;
        public LayerMask targetLayer;

        [Header("Impact")]
        public ImpactProfile impactProfile;

        [Header("Explosion on Hit")]
        public bool explodeOnHit = false;
        public float explosionRadius = 2f;
        public float explosionDamage = 30f;

        public override void Execute(GameObject caster, SkillData skillData)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning("[ProjectileEffect] ไม่มี Prefab กระสุน!", caster);
                return;
            }

            Transform casterTransform = caster.transform;
            Vector3 spawnPos = casterTransform.position
                             + casterTransform.forward * spawnForwardOffset
                             + Vector3.up * spawnHeight;
            Quaternion spawnRot = casterTransform.rotation;

            GameObject bullet = Instantiate(projectilePrefab, spawnPos, spawnRot);
            ProjectileBullet bulletScript = bullet.GetComponent<ProjectileBullet>();

            if (bulletScript != null)
            {
                DamageInfo info = new DamageInfo
                {
                    damageAmount = damage,
                    poiseDamage = poiseDamage,
                    damageType = damageType,
                    hitPoint = Vector3.zero, // Update on hit
                    knockbackForce = casterTransform.forward * knockbackPower,
                    attacker = caster,
                    impactProfile = impactProfile
                };

                bulletScript.Initialize(
                    casterTransform.forward,
                    speed,
                    lifetime,
                    info,
                    targetLayer,
                    explodeOnHit,
                    explosionRadius,
                    explosionDamage
                );
            }
            else
            {
                Debug.LogWarning("[ProjectileEffect] Prefab กระสุนไม่มี ProjectileBullet.cs!", bullet);
                Destroy(bullet, lifetime);
            }

            if (skillData != null && skillData.vfxPrefab != null)
            {
                Instantiate(skillData.vfxPrefab, spawnPos, spawnRot);
            }
        }

        public override void DrawGizmos(Transform caster)
        {
            if (caster == null) return;

            Vector3 spawnPos = caster.position + caster.forward * spawnForwardOffset + Vector3.up * spawnHeight;
            
            // จุดกำเนิดกระสุน
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(spawnPos, 0.2f);
            
            // เส้นทางการยิง
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.5f);
            Gizmos.DrawRay(spawnPos, caster.forward * 5f); // ยิงออกไปข้างหน้า
        }
    }
}
