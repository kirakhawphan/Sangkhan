public interface IEnemyState
{
    /// <summary>
    /// ทำงานครั้งแรกเมื่อเข้าสู่ State นี้
    /// </summary>
    void Enter();

    /// <summary>
    /// ทำงานทุกเฟรมตราบใดที่ยังอยู่ใน State นี้
    /// </summary>
    void Update();

    /// <summary>
    /// ทำงานก่อนที่จะออกจาก State นี้ (เพื่อเคลียร์ค่าต่างๆ)
    /// </summary>
    void Exit();
}
