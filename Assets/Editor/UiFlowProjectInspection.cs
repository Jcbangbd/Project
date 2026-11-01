using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UiFlowProjectInspection
{
    public static void Run()
    {
        var report = new StringBuilder();
        foreach (var path in new[] { "Assets/Scenes/SceneEntrance.unity", "Assets/Scenes/2.unity", "Assets/Scenes/SceneStage1.unity" })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            report.AppendLine(path);
            foreach (var root in scene.GetRootGameObjects()) Walk(root.transform, "", report);
        }
        Directory.CreateDirectory(".codex-tmp");
        File.WriteAllText(".codex-tmp/ui-flow-scene-inventory.txt", report.ToString());
    }
    private static void Walk(Transform t, string parent, StringBuilder report)
    {
        string path = parent + "/" + t.name;
        if (t is RectTransform)
        {
            string detail = "";
            var text = t.GetComponent<TMP_Text>();
            var legacy = t.GetComponent<Text>();
            var button = t.GetComponent<Button>();
            var image = t.GetComponent<Image>();
            if (text != null) detail += " TMP=" + text.text.Replace("\n", " ");
            if (legacy != null) detail += " Text=" + legacy.text.Replace("\n", " ");
            if (image != null && image.sprite != null) detail += " Sprite=" + image.sprite.name;
            if (button != null) for (int i=0;i<button.onClick.GetPersistentEventCount();i++)
                detail += " Click=" + button.onClick.GetPersistentTarget(i) + "." + button.onClick.GetPersistentMethodName(i);
            report.AppendLine(path + detail);
        }
        foreach (Transform child in t) Walk(child,path,report);
    }
}
