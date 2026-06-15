using UnityEngine;
using System.Collections;

// สคริปต์นี้จะติดไปกับ VFX ทุกตัวที่ถูก Spawn จาก Pool 
// ทำหน้าที่เช็คว่า Particle เล่นจบหรือยัง แล้วส่งตัวเองกลับเข้า Pool
public class VFXAutoReturn : MonoBehaviour
{
    [HideInInspector] public GameObject prefabRef;
    
    private ParticleSystem[] particles;
    private float maxDuration = 0f;

    private void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>();
        
        // คำนวณเวลาเล่นที่นานที่สุด
        foreach (var ps in particles)
        {
            float duration = ps.main.duration + ps.main.startDelay.constantMax;
            if (duration > maxDuration)
            {
                maxDuration = duration;
            }
        }
        
        // เผื่อเวลาให้เผื่อ Particle กระจายให้หมด
        maxDuration += 0.5f; 
    }

    private void OnEnable()
    {
        StartCoroutine(ReturnRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator ReturnRoutine()
    {
        // ถ้ามี ParticleSystem ให้เช็คสถานะ IsAlive()
        if (particles.Length > 0)
        {
            bool isAlive = true;
            while (isAlive)
            {
                yield return new WaitForSeconds(0.1f); // เช็คทุกๆ 0.1 วินาที เพื่อประหยัด CPU
                
                isAlive = false;
                for (int i = 0; i < particles.Length; i++)
                {
                    if (particles[i] != null && particles[i].IsAlive(true))
                    {
                        isAlive = true;
                        break;
                    }
                }
            }
        }
        else
        {
            // ถ้าไม่ใช่ Particle System ให้ใช้เวลาแบบคงที่แทน
            yield return new WaitForSeconds(maxDuration > 0 ? maxDuration : 1f);
        }

        // เมื่อเล่นจบแล้ว คืนเข้า Pool แทนการลบ
        VFXPool.Instance.ReturnToPool(prefabRef, gameObject);
    }
}
