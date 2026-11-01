using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    // 页面结构保存在独立预制体中，运行时只填入数据并绑定“开始答题”。
    public sealed class ExperimentCompletionView : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text message;
        public Button startQuestionnaire;
        private Action _onStart;

        public void Show(int taskCount, Action onStart)
        {
            _onStart = onStart;
            title.text = "实验已结束";
            message.text = "本次 " + taskCount + " 条作业任务已全部完成。\n请开始实验后答题，答题提交后返回实验介绍。";
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = ExperimentUiTheme.Font;
            startQuestionnaire.onClick.RemoveListener(Continue);
            startQuestionnaire.onClick.AddListener(Continue);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
        private void Continue()
        {
            var callback = _onStart;
            _onStart = null;
            Hide();
            callback?.Invoke();
        }
        public void Hide() { gameObject.SetActive(false); }
    }
}
