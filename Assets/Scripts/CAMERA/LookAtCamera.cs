using UnityEngine;

/// <summary>
/// 让物体朝向相机（或指定目标），并防止 NullReference。
/// 支持：全朝向 / 仅绕Y轴朝向（广告牌）

public class LookAtCamera : MonoBehaviour
{
    public enum BillboardMode { Full, YAxisOnly }

    [Header("目标（可空，留空则自动取主相机）")]
    [SerializeField] private Transform target;

    [Header("朝向模式")]
    public BillboardMode mode = BillboardMode.Full;

    [Header("丢失时自动重试（秒）")]
    [SerializeField] private float reacquireInterval = 1.0f;

    private float _nextReacquireTime;

    private void Awake()
    {
        TryAcquireTarget(force: true);
    }

    private void OnEnable()
    {
        TryAcquireTarget(force: false);
    }

    private void LateUpdate() // 用 LateUpdate，确保相机已更新完
    {
        // 目标丢了就尝试重取（例如切场景、相机销毁重建）
        if (target == null && Time.unscaledTime >= _nextReacquireTime)
        {
            TryAcquireTarget(force: false);
        }

        if (target == null) return; // 仍为空则直接返回，避免 NRE

        if (mode == BillboardMode.Full)
        {
            transform.LookAt(target);
        }
        else // YAxisOnly：只在水平面内朝向相机
        {
            Vector3 toCam = target.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
        }
    }

    private void TryAcquireTarget(bool force)
    {
        if (target != null && !force) return;

        // 1) 优先使用主相机
        Camera cam = Camera.main; // 可能为 null（没有 MainCamera 标签）
        if (cam != null)
        {
            target = cam.transform;
        }
        else
        {
            // 2) 回退：找场景中任意相机
            var anyCam = FindObjectOfType<Camera>();
            if (anyCam != null) target = anyCam.transform;
        }

        // 下次重试时间
        _nextReacquireTime = Time.unscaledTime + reacquireInterval;


    }
}
