using System;
using System.Collections.Generic;
using UnityEngine;

public class Loom : MonoBehaviour
{
    public static Loom Current;

    private struct DelayedQueueItem
    {
        public float time;
        public Action action;
    }

    private List<Action> _actions = new List<Action>();
    private List<DelayedQueueItem> _delayed = new List<DelayedQueueItem>();

    void Awake()
    {
        if (Current != null)
        {
            Destroy(gameObject);
            return;
        }

        Current = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // 处理即时操作
        lock (_actions)
        {
            foreach (var action in _actions)
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogError("Loom action error: " + e.Message);
                }
            }
            _actions.Clear();
        }

        // 处理延迟操作
        lock (_delayed)
        {
            for (int i = _delayed.Count - 1; i >= 0; i--)
            {
                if (_delayed[i].time <= Time.time)
                {
                    try
                    {
                        _delayed[i].action();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Loom delayed action error: " + e.Message);
                    }
                    _delayed.RemoveAt(i);
                }
            }
        }
    }

    public static void QueueOnMainThread(Action action)
    {
        QueueOnMainThread(action, 0f);
    }

    public static void QueueOnMainThread(Action action, float time)
    {
        if (time != 0)
        {
            lock (Current._delayed)
            {
                Current._delayed.Add(new DelayedQueueItem
                {
                    time = Time.time + time,
                    action = action
                });
            }
        }
        else
        {
            lock (Current._actions)
            {
                Current._actions.Add(action);
            }
        }
    }
}