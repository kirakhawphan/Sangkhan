using UnityEngine;

// ข้อมูลการโจมตี สร้างเป็น Struct เพื่อหลีกเลี่ยงการจองหน่วยความจำใหม่ (Zero GC Allocation)
[System.Serializable]
public struct DamageInfo
{
    public float damageAmount;     // จำนวนดาเมจ
    public Vector3 hitPoint;       // จุดที่ถูกโจมตี (สำหรับสร้างเอฟเฟกต์หรือเลือด)
    public Vector3 knockbackForce; // แรงกระเด็นที่เป้าหมายจะได้รับ
    public GameObject attacker;    // ผู้โจมตี (เพื่อใช้ตรวจสอบหาต้นตอของการโจมตี)
}
