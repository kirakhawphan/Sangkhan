using UnityEngine;

// ประเภทของดาเมจ ใช้แยกแยะบริบทการเสียเลือด
// ค่าเริ่มต้นของ enum คือ 0 (Combat) ดังนั้นโค้ดเดิมที่ไม่ได้ระบุจะถือว่าเป็น Combat อัตโนมัติ
public enum DamageType
{
    Combat,     // โดนศัตรูโจมตี (กล้องสั่น, เอฟเฟกต์โดนตี)
    System,     // ดาเมจจากระบบ เช่น DamageZone, Trap (ไม่สั่นกล้อง)
    FallDamage, // ตกจากที่สูง
    Poison      // พิษ, DOT
}

// ข้อมูลการโจมตี สร้างเป็น Struct เพื่อหลีกเลี่ยงการจองหน่วยความจำใหม่ (Zero GC Allocation)
[System.Serializable]
public struct DamageInfo
{
    public float damageAmount;     // จำนวนดาเมจ
    public DamageType damageType;  // ประเภทดาเมจ (ค่าเริ่มต้น = Combat)
    public Vector3 hitPoint;       // จุดที่ถูกโจมตี (สำหรับสร้างเอฟเฟกต์หรือเลือด)
    public Vector3 knockbackForce; // แรงกระเด็นที่เป้าหมายจะได้รับ
    public GameObject attacker;    // ผู้โจมตี (เพื่อใช้ตรวจสอบหาต้นตอของการโจมตี)
}
