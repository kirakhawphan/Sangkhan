using System.Collections;
using UnityEngine;

// ระบบจัดการเรื่องแรงกระแทกของเกม (เน้นจัดการเรื่องเวลา) แบบ Zero GC และ Flat Code
public class ImpactManager : MonoBehaviour
{
    public static ImpactManager Instance { get; private set; }

    private float hitStopDurationTimer; // ตัวจับเวลา Hit Stop ที่ใช้แทนการจอง Memory ใหม่
    private bool isHitStopping; // ตัวเช็คสถานะว่ากำลังหยุดเวลาอยู่หรือไม่

    private void Awake()
    {
        // จัดการ Singleton (กันบัคมีหลายตัวในฉาก)
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// สั่งหยุดเวลาชั่วขณะเพื่อสร้าง Game Feel (Hit Stop)
    /// </summary>
    /// <param name="duration">ระยะเวลาที่ต้องการหยุด (วินาที ในโลกความจริง)</param>
    public void HitStop(float duration)
    {
        // Guard Clause: กันบัคค่าเวลาติดลบ
        if (duration <= 0f) return;

        // ไม่ว่าของเก่าจะทำงานอยู่หรือไม่ เราจะตั้งเวลาใหม่ด้วยค่าที่นานกว่าเสมอ
        // ป้องกันบัคตีโดนศัตรู 3 ตัวพร้อมกันในเฟรมเดียว แล้วเวลาหยุดค้างนานเกินไป
        if (duration > hitStopDurationTimer)
        {
            hitStopDurationTimer = duration;
        }

        // ถ้ายังไม่ได้เริ่ม Hit Stop ให้เริ่มการทำงาน
        if (!isHitStopping)
        {
            isHitStopping = true;
            
            // ปรับเวลาลงให้ใกล้ศูนย์ (แนะนำ 0.05 ดีกว่า 0 เป๊ะ เพื่อป้องกัน Physics หรือ Animator ชะงักบัค)
            Time.timeScale = 0.05f; 
            
            StartCoroutine(HitStopRoutine());
        }
    }

    private IEnumerator HitStopRoutine()
    {
        // ใช้ while loop วนเช็คจาก Timer ของเราเอง 
        // (Zero GC: หลีกเลี่ยงการใช้ new WaitForSecondsRealtime(...) เพราะจะสร้างขยะ (Garbage) ใน Memory ทุกครั้งที่ตีโดน)
        while (hitStopDurationTimer > 0f)
        {
            // ลบเวลาด้วย unscaledDeltaTime (เวลาจริงของโลกนอกเกม ที่ไม่ถูกกระทบจาก Time.timeScale)
            hitStopDurationTimer -= Time.unscaledDeltaTime;
            
            yield return null; // รอเฟรมถัดไป
        }

        // เมื่อเวลาหมด คืนค่าสถานะให้เกมเดินต่อตามปกติ
        isHitStopping = false;
        Time.timeScale = 1.0f;
    }
}
