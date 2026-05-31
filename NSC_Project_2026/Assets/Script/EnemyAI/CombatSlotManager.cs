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

    // รายชื่อศัตรูที่กำลังถือบัตรคิวอยู่ (กำลังโจมตีผู้เล่น)
    private readonly List<EnemyBrain> _currentAttackers = new List<EnemyBrain>();

    private void Awake()
    {
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
}
