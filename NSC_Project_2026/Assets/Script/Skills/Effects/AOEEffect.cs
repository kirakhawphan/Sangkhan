using UnityEngine;
using Skills.Core;

namespace Skills.Effects
{
    /// <summary>
    /// สกิล AOE: วางโซนพื้นที่ดาเมจต่อเนื่อง (Duration-based)
    /// </summary>
    [CreateAssetMenu(fileName = "New AOE Effect", menuName = "Skills/Effects/AOE")]
    public class AOEEffect : SkillEffect
    {
        [Header("Zone Settings")]
        [Tooltip("Prefab ของโซนพื้นที่ (ต้องมี AOEZone.cs + Collider [isTrigger])")]
        public GameObject zonePrefab;
        public float duration = 5f;
        public float radius = 3f;

        [Header("Damage Over Time")]
        public float damagePerTick = 15f;
        public float tickInterval = 0.5f;
        public float poiseDamagePerTick = 5f;

        [Header("Knockback")]
        [Tooltip("แรงกระเด็น (0 = ไม่กระเด็น)")]
        public float knockbackPower = 0f;

        [Header("Damage Type")]
        public DamageType damageType = DamageType.System;
        public LayerMask targetLayer;

        [Header("Spawn Position")]
        public float forwardOffset = 3f;

        public override void Execute(GameObject caster, SkillData skillData)
        {
            if (zonePrefab == null)
            {
                Debug.LogWarning("[AOEEffect] ไม่มี Prefab โซน!", caster);
                return;
            }

            Transform casterTransform = caster.transform;
            Vector3 spawnPos = casterTransform.position + casterTransform.forward * forwardOffset;
            spawnPos.y = casterTransform.position.y; 

            GameObject zone = Instantiate(zonePrefab, spawnPos, Quaternion.identity);
            
            // Scale ตามที่ตั้ง
            zone.transform.localScale = Vector3.one * (radius * 2f);

            AOEZone zoneScript = zone.GetComponent<AOEZone>();
            if (zoneScript != null)
            {
                zoneScript.Initialize(
                    duration,
                    damagePerTick,
                    tickInterval,
                    poiseDamagePerTick,
                    damageType,
                    targetLayer,
                    caster,
                    knockbackPower
                );
            }
            else
            {
                Debug.LogWarning("[AOEEffect] Prefab โซนไม่มี AOEZone.cs!", zone);
                Destroy(zone, duration);
            }

            if (skillData != null && skillData.vfxPrefab != null)
            {
                GameObject vfx = Instantiate(skillData.vfxPrefab, spawnPos, Quaternion.identity);
                Destroy(vfx, duration);
            }
        }

        public override void DrawGizmos(Transform caster)
        {
            if (caster == null) return;

            Vector3 spawnPos = caster.position + caster.forward * forwardOffset;
            spawnPos.y = caster.position.y;
            
            // วาดพื้นที่ที่จะวางโซน
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(spawnPos, radius);
        }
    }
}
