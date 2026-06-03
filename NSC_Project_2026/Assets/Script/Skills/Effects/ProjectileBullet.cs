using UnityEngine;

namespace Skills.Effects
{
    /// <summary>
    /// ติดไว้ที่ Prefab กระสุน — ควบคุมการบิน + ตรวจจับการชน + ส่ง DamageInfo
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ProjectileBullet : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private DamageInfo damageInfo;
        private LayerMask targetLayer;
        private bool hasExplode;
        private float explosionRadius;
        private float explosionDamage;
        private bool isInitialized;

        private Rigidbody rb;
        private readonly Collider[] explosionResults = new Collider[20];

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Initialize(
            Vector3 dir,
            float spd,
            float lifetime,
            DamageInfo info,
            LayerMask layer,
            bool explode,
            float explRadius,
            float explDamage)
        {
            direction = dir.normalized;
            speed = spd;
            damageInfo = info;
            targetLayer = layer;
            hasExplode = explode;
            explosionRadius = explRadius;
            explosionDamage = explDamage;
            isInitialized = true;

            // ให้กระสุนบินด้วย Rigidbody
            rb.linearVelocity = direction * speed;

            // ทำลายตัวเองหลังหมดเวลา
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isInitialized) return;

#if UNITY_EDITOR
            Debug.Log($"[ProjectileBullet] ชนกับ: {other.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
#endif

            // ถ้ายิงโดนตัวเอง (หรือสิ่งแวดล้อมที่ตั้งใจให้ทะลุ) ให้ข้ามไป
            if (damageInfo.attacker != null && other.transform.IsChildOf(damageInfo.attacker.transform))
            {
#if UNITY_EDITOR
                Debug.Log($"[ProjectileBullet] ทะลุ {other.name} เพราะเป็นของคนยิง");
#endif
                return;
            }

            // ถ้า Layer ไม่ตรงกับที่กำหนดไว้ให้ชน (Target Layer)
            if ((targetLayer.value & (1 << other.gameObject.layer)) == 0)
            {
#if UNITY_EDITOR
                Debug.Log($"[ProjectileBullet] ทะลุ {other.name} เพราะ Layer ไม่อยู่ใน TargetLayer (ตั้งไว้: {targetLayer.value})");
#endif
                return;
            }

            // Direct Hit
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                damageInfo.hitPoint = other.ClosestPoint(transform.position);
                target.TakeDamage(damageInfo);
            }

            // Explosion
            if (hasExplode)
            {
                DoExplosion();
            }

            Destroy(gameObject);
        }

        private void DoExplosion()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, explosionRadius, explosionResults, targetLayer);

            IDamageable selfDamageable = null;
            if (damageInfo.attacker != null)
                selfDamageable = damageInfo.attacker.GetComponentInParent<IDamageable>();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = explosionResults[i];
                if (col == null) continue;
                
                IDamageable splashTarget = col.GetComponentInParent<IDamageable>();
                if (splashTarget == null || splashTarget == selfDamageable) continue;

                Vector3 knockbackDir = (col.transform.position - transform.position).normalized;
                if (knockbackDir.sqrMagnitude < 0.001f) knockbackDir = Vector3.up;

                DamageInfo splashInfo = new DamageInfo
                {
                    damageAmount = explosionDamage,
                    poiseDamage = damageInfo.poiseDamage * 0.5f,
                    damageType = damageInfo.damageType,
                    hitPoint = col.ClosestPoint(transform.position),
                    knockbackForce = knockbackDir * (damageInfo.knockbackForce.magnitude * 0.7f),
                    attacker = damageInfo.attacker,
                    impactProfile = damageInfo.impactProfile
                };

                splashTarget.TakeDamage(splashInfo);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (hasExplode)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, explosionRadius);
            }
        }
    }
}
