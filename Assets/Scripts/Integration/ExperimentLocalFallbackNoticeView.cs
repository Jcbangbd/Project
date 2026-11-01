using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    /// <summary>算例来源提示弹窗。结构与样式保存在 Canvas Prefab 中。</summary>
    [DisallowMultipleComponent]
    public sealed class ExperimentLocalFallbackNoticeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button confirmButton;

        public TMP_Text MessageText => messageText;
        public Button ConfirmButton => confirmButton;

        public void Configure(TMP_Text message, Button confirm)
        {
            messageText = message;
            confirmButton = confirm;
        }

        private void OnEnable()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(Hide);
        }

        public void Show(string message)
        {
            if (messageText != null)
                messageText.text = message ?? string.Empty;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
