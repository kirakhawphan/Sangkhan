using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;

public class ListGameObjectsTool
{
    [MenuItem("Tools/List GameObjects")]
    [InitializeOnLoadMethod]
    public static void ListObjects()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded) return;
        
        GameObject[] roots = activeScene.GetRootGameObjects();
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- GameObjects in {activeScene.name} ---");
        
        foreach (GameObject root in roots)
        {
            sb.AppendLine(root.name);
            foreach (Transform child in root.transform)
            {
                sb.AppendLine("  |- " + child.name);
            }
        }
        
        Debug.Log(sb.ToString()); // Changed from LogWarning to Log to reduce noise
    }
}
