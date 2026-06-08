using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบจัดการคิวการโจมตีของศัตรู (Token / Combat Slot System)
/// ทำหน้าที่แจก "บัตรคิว" ให้ศัตรูที่อยากโจมตีผู้เล่น
/// ถ้าคิวเต็ม ศัตรูจะต้องไปเดินวน (CircleState) รอแทน
///
/// วิธีใช้: นำสคริปต์นี้ไปแปะไว้ที่ GameManager หรือ GameObject เปล่าในฉาก
/// </summary>
public class CombatSlotManager : MonoBehaviour
{
    public static CombatSlotManager Instance { get; private set; }

    [Header("Slot Settings")]
    [Tooltip("จำนวนศัตรูที่สามารถเข้าโจมตีประชิดผู้เล่นได้พร้อมกัน")]
    [SerializeField] private int maxMeleeSlots = 2;

    [Header("Off-Screen Delay")]
    [Tooltip("ระยะเวลา (วินาที) ที่ศัตรูนอกมุมกล้องต้องรอก่อนได้รับบัตรคิว (0 = ปิดระบบ)")]
    [SerializeField] public float offScreenSlotDelay = 1.5f;

    // รายชื่อศัตรูที่กำลังถือบัตรคิวอยู่ (กำลังโจมตีผู้เล่น)
    private readonly List<EnemyBrain> _currentAttackers = new List<EnemyBrain>();

    // [Zero GC] Cache กล้องหลักไว้ครั้งเดียว หลีกเลี่ยง Camera.main lookup ทุก frame
    private Camera _mainCamera;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // [Zero GC] Cache กล้องหลักตั้งแต่เริ่มเกม
            _mainCamera = Camera.main;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ขอรับบัตรคิวเพื่อเข้าโจมตีผู้เล่น
    /// คืนค่า true ถ้าได้รับอนุญาต, false ถ้าคิวเต็ม
    /// </summary>
    public bool RequestSlot(EnemyBrain requester)
    {
        // กัน Null และป้องกันการลงทะเบียนซ้ำ
        if (requester == null) return false;
        if (_currentAttackers.Contains(requester)) return true; // มีคิวอยู่แล้ว

        if (_currentAttackers.Count < maxMeleeSlots)
        {
            _currentAttackers.Add(requester);
            return true; // ✅ ได้รับอนุญาตให้โจมตี
        }

        return false; // ❌ คิวเต็ม ให้ไปเดินวนแทน
    }

    /// <summary>
    /// [เพิ่ม] บังคับแทรกคิว (ใช้สำหรับ Counter Attack)
    /// ถ้าคิวเต็มอยู่ จะเตะตัวแรกสุดที่กำลังตีให้ออกไป เพื่อไม่ให้รุมผู้เล่นเยอะเกิน
    /// </summary>
    public void ForceRequestSlot(EnemyBrain requester)
    {
        if (requester == null) return;
        if (_currentAttackers.Contains(requester)) return;

        // ถ้าคิวเต็ม ให้เตะตัวแรกออกไป (ตัวที่รุมอยู่ก่อน)
        if (_currentAttackers.Count >= maxMeleeSlots && _currentAttackers.Count > 0)
        {
            EnemyBrain victim = _currentAttackers[0];
            _currentAttackers.RemoveAt(0);

            // บังคับให้ตัวที่โดนแย่งคิว ถอยกลับไปดูเชิง
            if (victim != null && victim.circleState != null)
            {
                victim.ChangeState(victim.circleState);
            }
        }
        
        _currentAttackers.Add(requester);
    }

    /// <summary>
    /// คืนบัตรคิวกลับมาให้ระบบ (เรียกเมื่อโจมตีเสร็จ, โดน Stun, หรือตาย)
    /// </summary>
    public void ReleaseSlot(EnemyBrain requester)
    {
        if (requester == null) return;
        _currentAttackers.Remove(requester);
    }

    /// <summary>
    /// ตรวจสอบว่าศัตรูตัวนี้กำลังถือบัตรคิวอยู่หรือไม่
    /// </summary>
    public bool HasSlot(EnemyBrain requester)
    {
        return _currentAttackers.Contains(requester);
    }

    /// <summary>
    /// จำนวนช่องว่างที่เหลืออยู่ (สำหรับ Debug)
    /// </summary>
    public int AvailableSlots => maxMeleeSlots - _currentAttackers.Count;

    /// <summary>
    /// ตรวจสอบว่าศัตรูอยู่ในมุมมองของกล้องหลักหรือไม่
    /// ใช้ Camera Viewport เพื่อเช็คว่า position อยู่ใน [0,1] x [0,1] และอยู่หน้ากล้อง (z > 0)
    /// </summary>
    /// <param name="worldPosition">ตำแหน่งโลกของศัตรู</param>
    /// <returns>true ถ้าอยู่ในหน้าจอ</returns>
    public static bool IsEnemyOnScreen(Vector3 worldPosition)
    {
        // [Zero GC] ใช้ _mainCamera ที่ cache ไว้แทน Camera.main (ไม่ allocate ทุก frame)
        Camera cam = Instance != null ? Instance._mainCamera : Camera.main;
        if (cam == null) return true; // ถ้าไม่มีกล้อง ถือว่าอยู่บนหน้าจอเสมอ (ปลอดภัย)

        Vector3 vp = cam.WorldToViewportPoint(worldPosition);
        // vp.z > 0 = อยู่หน้ากล้อง, [0,1] x [0,1] = อยู่ใน Viewport
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }
}
