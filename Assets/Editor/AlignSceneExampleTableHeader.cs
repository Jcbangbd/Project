#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
internal static class AlignSceneExampleTableHeader
{
    private const string RowPrefabPath = "Assets/PREFABS/UI/SceneExampleRow.prefab";

    static AlignSceneExampleTableHeader()
    {
        EditorApplication.delayCall += Align;
    }

    private static void Align()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "SceneStage1")
        {
            return;
        }

        Transform tableHeader = Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(item =>
                item != null &&
                item.gameObject.scene == scene &&
                item.name == "TableHeader" &&
                item.parent != null &&
                item.parent.name == "ExampleViewerPanel");

        if (tableHeader == null)
        {
            Debug.LogWarning("[ExampleTableHeader] TableHeader was not found.");
            return;
        }

        GameObject rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (rowPrefab == null)
        {
            Debug.LogWarning("[ExampleTableHeader] SceneExampleRow prefab was not found.");
            return;
        }

        RectTransform headerRect = tableHeader as RectTransform;
        if (headerRect == null)
        {
            return;
        }

        Undo.RecordObject(headerRect, "Align example table header");
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(25f, -145f);
        headerRect.offsetMax = new Vector2(-25f, -105f);
        headerRect.localScale = Vector3.one;
        headerRect.localRotation = Quaternion.identity;

        HorizontalLayoutGroup sourceLayout = rowPrefab.GetComponent<HorizontalLayoutGroup>();
        HorizontalLayoutGroup headerLayout = tableHeader.GetComponent<HorizontalLayoutGroup>();
        if (headerLayout == null)
        {
            headerLayout = Undo.AddComponent<HorizontalLayoutGroup>(tableHeader.gameObject);
        }

        Undo.RecordObject(headerLayout, "Configure example table header layout");
        if (sourceLayout != null)
        {
            headerLayout.padding = new RectOffset(
                sourceLayout.padding.left,
                sourceLayout.padding.right,
                sourceLayout.padding.top,
                sourceLayout.padding.bottom);
            headerLayout.spacing = sourceLayout.spacing;
            headerLayout.childAlignment = sourceLayout.childAlignment;
            headerLayout.childControlWidth = sourceLayout.childControlWidth;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = sourceLayout.childForceExpandWidth;
            headerLayout.childForceExpandHeight = true;
            headerLayout.childScaleWidth = sourceLayout.childScaleWidth;
            headerLayout.childScaleHeight = sourceLayout.childScaleHeight;
            headerLayout.reverseArrangement = sourceLayout.reverseArrangement;
        }
        else
        {
            headerLayout.padding = new RectOffset(6, 6, 0, 0);
            headerLayout.spacing = 4f;
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = true;
        }

        string[] headerNames =
        {
            "NoHeader", "CraneHeader", "TypeHeader",
            "StartHeader", "EndHeader", "DurationHeader"
        };
        string[] rowNames =
        {
            "NoText", "CraneText", "TypeText",
            "StartText", "EndText", "DurationText"
        };
        string[] labels =
        {
            "\u5E8F\u53F7", "\u5CB8\u6865", "\u4EFB\u52A1\u7C7B\u578B",
            "\u8D77\u59CB\u4F4D\u7F6E", "\u76EE\u6807\u4F4D\u7F6E", "\u65F6\u957F"
        };
        float[] fallbackFlexibleWidths = { 1f, 1f, 2.2f, 1.2f, 1.2f, 1f };

        for (int i = 0; i < headerNames.Length; i++)
        {
            Transform headerChild = tableHeader.Find(headerNames[i]);
            Transform rowChild = rowPrefab.transform.Find(rowNames[i]);
            if (headerChild == null)
            {
                Debug.LogWarning("[ExampleTableHeader] Missing child: " + headerNames[i]);
                continue;
            }

            LayoutElement targetElement = headerChild.GetComponent<LayoutElement>();
            if (targetElement == null)
            {
                targetElement = Undo.AddComponent<LayoutElement>(headerChild.gameObject);
            }
            Undo.RecordObject(targetElement, "Match example table column width");

            LayoutElement sourceElement = rowChild != null
                ? rowChild.GetComponent<LayoutElement>()
                : null;
            if (sourceElement != null)
            {
                targetElement.ignoreLayout = sourceElement.ignoreLayout;
                targetElement.minWidth = sourceElement.minWidth;
                targetElement.preferredWidth = sourceElement.preferredWidth;
                targetElement.flexibleWidth = sourceElement.flexibleWidth;
            }
            else
            {
                targetElement.ignoreLayout = false;
                targetElement.minWidth = -1f;
                targetElement.preferredWidth = -1f;
                targetElement.flexibleWidth = fallbackFlexibleWidths[i];
            }
            targetElement.minHeight = 40f;
            targetElement.preferredHeight = 40f;
            targetElement.flexibleHeight = 0f;

            RectTransform childRect = headerChild as RectTransform;
            if (childRect != null)
            {
                Undo.RecordObject(childRect, "Reset example table header transform");
                childRect.localScale = Vector3.one;
                childRect.localRotation = Quaternion.identity;
            }

            TMP_Text text = headerChild.GetComponent<TMP_Text>();
            if (text != null)
            {
                Undo.RecordObject(text, "Format example table header text");
                text.text = labels[i];
                text.color = new Color32(32, 32, 32, 255);
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);
        EditorUtility.SetDirty(tableHeader.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ExampleTableHeader] Six headers aligned with SceneExampleRow columns.");
    }
}
#endif
