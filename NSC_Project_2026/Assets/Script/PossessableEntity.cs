using UnityEngine;

// นำสคริปต์นี้ไปแปะไว้ที่ GameObject ตัวละครอื่นๆ ที่ต้องการให้ผู้เล่นสามารถสิงร่างได้
public class PossessableEntity : MonoBehaviour
{
    [Header("Entity Info")]
    [SerializeField, Tooltip("ชื่อของร่างนี้ (เอาไว้แสดงผล UI หรือใช้แยกแยะ)")]
    private string entityName = "Unknown Entity";
    
    // Property สำหรับให้ภายนอกอ่านค่าได้อย่างเดียว (Encapsulation)
    public string EntityName => entityName;
}
