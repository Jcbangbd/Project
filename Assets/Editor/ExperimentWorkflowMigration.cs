using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using Vtp.VR.Integration;

public static class ExperimentWorkflowMigration
{
    public static void Apply()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("仅在隔离工程显式执行。");
        Patch("Assets/Resources/PreExperimentQuestionnaire.prefab", root =>
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text == "实验准备答题") text.text = "实验结束答题";
                if (text.name == "CompleteInfo") text.text = "第1至8题已作答完成。点击提交，统一保存本次成绩并返回实验介绍。";
                if (text.text == "开始仿真实验" || text.text == "继续结果核对") text.text = "提交答题并返回实验介绍";
            }
        });
        Patch("Assets/Resources/ExperimentAssessmentCanvas.prefab", root =>
        {
            var view = root.GetComponentInChildren<ExperimentExampleSelectionCanvasView>(true);
            view.EnterButton.GetComponentInChildren<TMP_Text>(true).text = "设置参数";
        });
        var assessment = PrefabUtility.LoadPrefabContents("Assets/Resources/ExperimentAssessmentCanvas.prefab");
        try
        {
            var notice = assessment.GetComponentInChildren<ExperimentLocalFallbackNoticeView>(true);
            var dialog = UnityEngine.Object.Instantiate(notice.gameObject);
            dialog.name = "ExperimentCompletionView";
            var old = dialog.GetComponent<ExperimentLocalFallbackNoticeView>();
            var message = old.MessageText;
            var button = old.ConfirmButton;
            UnityEngine.Object.DestroyImmediate(old);
            var completion = dialog.AddComponent<ExperimentCompletionView>();
            completion.title = dialog.transform.Find("NoticeBox/NoticeTitle")?.GetComponent<TMP_Text>();
            if (completion.title == null)
                foreach (var text in dialog.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name == "NoticeTitle") completion.title = text;
            if (completion.title == null) throw new InvalidOperationException("结束页面缺少标题组件。");
            completion.title.text = "实验已结束";
            completion.message = message;
            completion.message.text = "本次作业任务已全部完成，请开始实验后答题。";
            completion.message.color = ExperimentUiTheme.TextColor;
            completion.startQuestionnaire = button;
            button.GetComponentInChildren<TMP_Text>(true).text = "开始答题";
            dialog.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(dialog, "Assets/Resources/ExperimentCompletionView.prefab");
            UnityEngine.Object.DestroyImmediate(dialog);
        }
        finally { PrefabUtility.UnloadPrefabContents(assessment); }
        AssetDatabase.SaveAssets();
        Debug.Log("EXPERIMENT_WORKFLOW_MIGRATION_PASSED");
    }
    private static void Patch(string path, Action<GameObject> update)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { update(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
