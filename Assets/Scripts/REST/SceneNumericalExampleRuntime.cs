using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vtp.VR.Rest
{
    /// <summary>
    /// 临时保存从远程服务器取得的当前任务算例。
    /// 后续算法输入转换器可以从这里读取。
    /// </summary>
    public static class SceneNumericalExampleRuntime
    {
        private static SceneNumericalExampleDto[] _examples =
            new SceneNumericalExampleDto[0];

        private static SceneNumericalExampleDto[] _originalExamples =
            new SceneNumericalExampleDto[0];

        public static string CourseTaskId
        {
            get;
            private set;
        }

        public static SceneNumericalExampleDto[] Examples
        {
            get
            {
                return _examples;
            }
        }

        /// <summary>
        /// 本次服务器或ZIP返回的原始集合。查看和动画只使用Examples；
        /// 原始集合仅为兼容EXE自身对翻箱初始化数据的要求。
        /// </summary>
        public static SceneNumericalExampleDto[] OriginalExamples
        {
            get
            {
                return _originalExamples;
            }
        }

        public static bool HasExamples
        {
            get
            {
                return _examples != null &&
                       _examples.Length > 0;
            }
        }

        /// <summary>
        /// 当前缓存是否来自服务器不可用时生成的本地测试算例。
        /// </summary>
        public static bool IsLocalFallback
        {
            get;
            private set;
        }

        /// <summary>
        /// 当前实验已经筛选出的唯一作业类型，例如“卸船”。
        /// </summary>
        public static string TaskType
        {
            get;
            private set;
        }

        /// <summary>
        /// 用于算例查看页面显示的数据来源说明。
        /// </summary>
        public static string SourceMessage
        {
            get;
            private set;
        }

        public static void Set(
            string courseTaskId,
            SceneNumericalExampleDto[] examples,
            bool isLocalFallback = false,
            string sourceMessage = null,
            string taskType = null,
            SceneNumericalExampleDto[] originalExamples = null)
        {
            CourseTaskId =
                (courseTaskId ?? string.Empty).Trim();

            _examples =
                examples ??
                new SceneNumericalExampleDto[0];

            _originalExamples =
                originalExamples ??
                _examples;

            IsLocalFallback = isLocalFallback;
            SourceMessage = sourceMessage ?? string.Empty;
            TaskType = taskType ?? string.Empty;
        }

        public static void Clear()
        {
            CourseTaskId = null;

            IsLocalFallback = false;
            SourceMessage = string.Empty;
            TaskType = string.Empty;

            _examples =
                new SceneNumericalExampleDto[0];

            _originalExamples =
                new SceneNumericalExampleDto[0];
        }
    }
}
