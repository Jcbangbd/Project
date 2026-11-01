using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vtp.VR.Integration;
using Vtp.VR.LocalAlgorithmExe;
using Vtp.VR.Rest;

// 只验证提示文字，不运行其他算法、不访问服务器、不提交成绩。
public static class AlgorithmProgressTextVerification
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("请在隔离批处理工程验证。");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var client = typeof(RmgExeCoordinator).Assembly.GetType("Vtp.VR.LocalAlgorithmExe.RmgExeProcessClient", true);
        var format = client.GetMethod("BuildExecutionProgress", BindingFlags.NonPublic | BindingFlags.Static);
        if (format == null) throw new InvalidOperationException("进度格式方法不存在。");
        var overlay = UnityEngine.Object.Instantiate(Resources.Load<AlgorithmRunOverlay>("AlgorithmRunOverlay"));
        var checks = new List<string>();
        string[] algorithms = { "ga", "pso", "alns" };
        string[] names = { "遗传算法", "粒子群优化算法", "自适应大邻域搜索算法" };
        try
        {
            foreach (int count in new[] { 1, 17, 43 })
            foreach (string taskType in new[] { "卸船", "装船", "卸车", "装车", "翻箱" })
            for (int index = 0; index < algorithms.Length; index++)
            {
                var actual = (string)format.Invoke(null, new object[] { count, taskType, algorithms[index] });
                var expected = "已传入" + count + "条“" + taskType + "”算例，正在执行" + names[index] + "……";
                if (actual != expected || actual.Contains("EXE") || actual.Contains("启动"))
                    throw new InvalidOperationException("执行提示不匹配：" + actual);
                checks.Add("PASS: " + actual);
                SceneNumericalExampleRuntime.Set("PROGRESS_TEXT_QA", new SceneNumericalExampleDto[0], false, null, taskType);
                overlay.Begin(names[index], count);
                var subtitle = names[index] + " · " + count + " 条" + taskType + "任务";
                if (overlay.algorithmText.text != subtitle) throw new InvalidOperationException("副标题未使用当前作业类型。");
                checks.Add("PASS: " + subtitle);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(overlay.gameObject);
            SceneNumericalExampleRuntime.Clear();
        }
        Directory.CreateDirectory(".codex-tmp/AlgorithmProgressText");
        File.WriteAllLines(".codex-tmp/AlgorithmProgressText/checks.txt", checks);
        Debug.Log("ALGORITHM_PROGRESS_TEXT_VERIFICATION_PASSED " + checks.Count);
    }
}
