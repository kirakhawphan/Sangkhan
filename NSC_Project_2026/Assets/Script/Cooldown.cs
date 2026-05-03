using UnityEngine;

/// <summary>
/// ระบบคูลดาวน์แบบ Reusable — นำไปฝังใน MonoBehaviour ใดก็ได้
/// ใช้ [System.Serializable] เพื่อให้ตั้งค่า duration ได้จาก Inspector
/// ไม่สืบทอดจาก MonoBehaviour — เป็นคลาสตัวช่วยล้วนๆ
/// </summary>
[System.Serializable]
public class Cooldown
{
    [Tooltip("ระยะเวลาคูลดาวน์ (วินาที)")]
    public float duration = 2f;

    // เวลาที่ระบบจะพร้อมใช้งานครั้งถัดไป (เทียบกับ Time.time)
    private float nextReadyTime = 0f;

    /// <summary>
    /// ตรวจสอบว่าคูลดาวน์หมดแล้วหรือยัง (พร้อมใช้งาน = true)
    /// </summary>
    public bool IsReady()
    {
        return Time.time >= nextReadyTime;
    }

    /// <summary>
    /// เริ่มนับคูลดาวน์ใหม่ (เรียกหลังใช้สกิล/ความสามารถสำเร็จ)
    /// </summary>
    public void StartCooldown()
    {
        nextReadyTime = Time.time + duration;
    }

    /// <summary>
    /// คืนค่าเปอร์เซ็นต์คูลดาวน์ที่เหลือ (1 = เพิ่งเริ่ม, 0 = หมดแล้ว)
    /// เหมาะสำหรับ UI แสดง Cooldown Indicator
    /// </summary>
    public float Progress
    {
        get
        {
            if (duration <= 0f) return 0f;
            float remaining = nextReadyTime - Time.time;
            return Mathf.Clamp01(remaining / duration);
        }
    }
}
