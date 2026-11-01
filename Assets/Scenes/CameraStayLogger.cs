using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 摄像头 / 流程停留时间记录器
/// </summary>
public static class CameraStayLogger
{
    // ===== 视角级别 =====
    private static string currentViewId;
    private static float viewEnterTime;
    private static bool hasView;
    private static DateTime viewStartTime;
    private static readonly Dictionary<string, float> viewTotalTimes = new Dictionary<string, float>();

    // ===== 流程级别 =====
    private static string currentFlowId;
    private static float flowEnterTime;
    private static bool hasFlow;
    private static readonly Dictionary<string, float> flowTotalTimes = new Dictionary<string, float>();
    private static DateTime currentFlowStartTime;

    // ================== 视角 ==================

    public static void Enter(string viewId)
    {
        if (string.IsNullOrEmpty(viewId))
            return;

        if (hasView && viewId == currentViewId)
            return;

        End(); // 先结束上一个视角

        currentViewId = viewId;
        viewEnterTime = Time.unscaledTime;
        viewStartTime = DateTime.UtcNow;
        hasView = true;
    }

    public static void End()
    {
        if (!hasView)
            return;

        float duration = Time.unscaledTime - viewEnterTime;

        if (!viewTotalTimes.ContainsKey(currentViewId))
            viewTotalTimes[currentViewId] = 0f;

        viewTotalTimes[currentViewId] += duration;

        Debug.Log($"[CameraStay] 视角 {currentViewId} 停留 {duration:F2} 秒，累计 {viewTotalTimes[currentViewId]:F2} 秒");

        // ★★★ 在这里把这次视角停留发送到服务器
        try
        {
            if (NetworkManager.Instance != null &&
                NetworkManager.Instance.IsConnected &&
                NetworkManager.Instance.CurrentUserId > 0)
            {
                // 如果当前正在某个流程里，就把流程名当作 location
                string location = hasFlow && !string.IsNullOrEmpty(currentFlowId)
                    ? currentFlowId
                    : "None";

                NetworkManager.Instance.SendActivityLog(
                    flowName: currentViewId,         // 这里用视角名当 ActivityType
                    detail: "CameraViewStay",        // 用 detail 区分是“视角停留”
                    startTime: viewStartTime,        // 进入这个视角时的真实时间
                    durationSeconds: duration,
                    location: location               // 当前流程名，例如 DischargeShip_Learn
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CameraStay] 上报视角记录出错: {ex.Message}");
        }

        hasView = false;
        currentViewId = null;
    }


    // ================== 流程 ==================

    public static void EnterFlow(string flowId)
    {
        if (string.IsNullOrEmpty(flowId))
            return;

        if (hasFlow && flowId == currentFlowId)
            return;

        EndFlow(); // 先结束上一个流程

        currentFlowId = flowId;
        flowEnterTime = Time.unscaledTime;
        currentFlowStartTime = DateTime.UtcNow;
        hasFlow = true;

        Debug.Log($"[Flow] 进入流程 {currentFlowId}");
    }

    public static void EndFlow()
    {
        if (!hasFlow)
            return;

        float duration = Time.unscaledTime - flowEnterTime;

        if (!flowTotalTimes.ContainsKey(currentFlowId))
            flowTotalTimes[currentFlowId] = 0f;

        flowTotalTimes[currentFlowId] += duration;

        Debug.Log($"[Flow] 退出流程 {currentFlowId}，本次 {duration:F2} 秒，累计 {flowTotalTimes[currentFlowId]:F2} 秒");

        // ★★★ 这里保留一个【流程总时长】记录（可选）★★★
        try
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.CurrentUserId > 0)
            {
                NetworkManager.Instance.SendActivityLog(
                    flowName: currentFlowId,              // ActivityType = 流程名
                    detail: "FlowStay",
                    startTime: currentFlowStartTime,
                    durationSeconds: duration,
                    location: "SceneStage1"               // 场景名（你也可以改成 SceneManager.GetActiveScene().name）
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Flow] 上报学习记录出错: {ex.Message}");
        }

        hasFlow = false;
        currentFlowId = null;
    }

    public static void LogAllTotals()
    {
        Debug.Log("===== Flow 总时间统计 =====");
        foreach (var kv in flowTotalTimes)
            Debug.Log($"流程 {kv.Key} 总时长：{kv.Value:F2} 秒");

        Debug.Log("===== Camera View 总时间统计 =====");
        foreach (var kv in viewTotalTimes)
            Debug.Log($"视角 {kv.Key} 总时长：{kv.Value:F2} 秒");
    }
}