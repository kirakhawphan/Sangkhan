using System.Collections.Generic;
using UnityEngine;

// ระบบ Object Pooling แบบ Zero GC สำหรับ VFX โดยเฉพาะ
public class VFXPool : MonoBehaviour
{
    private static VFXPool instance;
    public static VFXPool Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("[VFX_Pool]");
                instance = go.AddComponent<VFXPool>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // เก็บ Pool แยกตาม Prefab แต่ละแบบ
    private Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!pools.ContainsKey(prefab))
        {
            pools[prefab] = new Queue<GameObject>();
        }

        Queue<GameObject> queue = pools[prefab];
        GameObject vfxInstance;

        if (queue.Count > 0)
        {
            vfxInstance = queue.Dequeue();
            vfxInstance.transform.position = position;
            vfxInstance.transform.rotation = rotation;
            vfxInstance.SetActive(true);
        }
        else
        {
            vfxInstance = Instantiate(prefab, position, rotation);
            
            // แปะสคริปต์ให้มันคืนตัวเองกลับเข้า Pool เมื่อเล่นเสร็จ
            VFXAutoReturn autoReturn = vfxInstance.AddComponent<VFXAutoReturn>();
            autoReturn.prefabRef = prefab;
        }

        return vfxInstance;
    }

    public void ReturnToPool(GameObject prefab, GameObject instance)
    {
        instance.SetActive(false);

        if (!pools.ContainsKey(prefab))
        {
            pools[prefab] = new Queue<GameObject>();
        }

        pools[prefab].Enqueue(instance);
    }
}
