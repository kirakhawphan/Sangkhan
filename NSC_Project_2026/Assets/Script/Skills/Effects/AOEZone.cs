using UnityEngine;
using System.Collections.Generic;

namespace Skills.Effects
{
    /// <summary>
    /// ติดไว้ที่ Prefab โซนพื้นที่ — ทำดาเมจต่อเนื่องให้เป้าหมายที่อยู่ในวง
    /// </summary>
    public class AOEZone : MonoBehaviour
    {
        private float damagePerTick;
        private float tickInterval;
        private float poiseDamagePerTick;
        private float knockbackPower;
        private DamageType damageType;
        private LayerMask targetLayer;
        private GameObject owner;
        private bool isInitialized;

        private struct TargetEntry
        {
            public IDamageable Target;
            public float Timer;
            public int ColliderCount;
            public Collider RepresentativeCollider;
        }

        private readonly List<TargetEntry> targets = new List<TargetEntry>();

        public void Initialize(
            float duration,
            float dmgPerTick,
            float interval,
            float poiseDmg,
            DamageType type,
            LayerMask layer,
            GameObject caster,
            float knockback = 0f)
        {
            damagePerTick = dmgPerTick;
            tickInterval = interval;
            poiseDamagePerTick = poiseDmg;
            damageType = type;
            targetLayer = layer;
            owner = caster;
            knockbackPower = knockback;
            isInitialized = true;

            Destroy(gameObject, duration);
        }

        private void Update()
        {
            if (!isInitialized) return;

            float dt = Time.deltaTime;

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                TargetEntry entry = targets[i];

                if (entry.Target == null || (entry.Target as Component) == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                entry.Timer -= dt;

                if (entry.Timer <= 0f)
                {
                    DealDamage(entry.Target, entry.RepresentativeCollider);
                    entry.Timer = tickInterval;
                }
                
                targets[i] = entry;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isInitialized) return;
#if UNITY_EDITOR
            Debug.Log($"[AOEZone] ตรวจพบการชนกับ: {other.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
#endif

            if ((targetLayer.value & (1 << other.gameObject.layer)) == 0)
            {
#if UNITY_EDITOR
                Debug.Log($"[AOEZone] ปฏิเสธ {other.name} เพราะ Layer ไม่อยู่ใน TargetLayer ที่ตั้งไว้ (TargetLayer: {targetLayer.value})");
#endif
                return;
            }

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null)
            {
#if UNITY_EDITOR
                Debug.Log($"[AOEZone] ปฏิเสธ {other.name} เพราะไม่มีสคริปต์ IDamageable");
#endif
                return;
            }

            if (owner != null)
            {
                IDamageable ownerDamageable = owner.GetComponentInParent<IDamageable>();
                if (target == ownerDamageable)
                {
#if UNITY_EDITOR
                    Debug.Log($"[AOEZone] ปฏิเสธ {other.name} เพราะเป็นผู้ร่ายสกิล (ตีตัวเองไม่ได้)");
#endif
                    return;
                }
            }

            int existingIndex = -1;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Target == target)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                TargetEntry entry = targets[existingIndex];
                entry.ColliderCount++;
                targets[existingIndex] = entry;
            }
            else
            {
                DealDamage(target, other);
                targets.Add(new TargetEntry
                {
                    Target = target,
                    Timer = tickInterval,
                    ColliderCount = 1,
                    RepresentativeCollider = other
                });
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!isInitialized) return;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Target == target)
                {
                    TargetEntry entry = targets[i];
                    entry.ColliderCount--;
                    if (entry.ColliderCount <= 0)
                    {
                        targets.RemoveAt(i);
                    }
                    else
                    {
                        targets[i] = entry;
                    }
                    break;
                }
            }
        }

        private void DealDamage(IDamageable target, Collider col)
        {
            // คำนวณทิศทางกระเด็น (ผลักออกจากศูนย์กลางโซน)
            Vector3 knockDir = Vector3.zero;
            if (knockbackPower > 0f && col != null)
            {
                knockDir = (col.transform.position - transform.position).normalized;
                knockDir.y = 0f;
                if (knockDir.sqrMagnitude < 0.001f) knockDir = Vector3.forward;
            }

            DamageInfo info = new DamageInfo
            {
                damageAmount = damagePerTick,
                poiseDamage = poiseDamagePerTick,
                damageType = damageType,
                hitPoint = col != null ? col.ClosestPoint(transform.position) : transform.position,
                knockbackForce = knockDir * knockbackPower,
                attacker = owner,
                impactProfile = null
            };

#if UNITY_EDITOR
            Debug.Log($"[AOEZone] กำลังทำดาเมจใส่ {(target as MonoBehaviour)?.gameObject.name} จำนวน {damagePerTick} ดาเมจ! (Knockback: {knockbackPower})");
#endif
            target.TakeDamage(info);
        }
    }
}

