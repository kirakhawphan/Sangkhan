// อินเตอร์เฟสสำหรับเป็นผู้รับดาเมจ (Decoupled Architecture 100%)
// สคริปต์ใดก็ตามที่สามารถรับดาเมจได้ (ผู้เล่น, ศัตรู, กล่อง, สิ่งของ) ให้ Implement อินเตอร์เฟสนี้
public interface IDamageable
{
    // ฟังก์ชันรับดาเมจ บังคับให้รับค่าเป็น DamageInfo 
    void TakeDamage(DamageInfo info);
}
