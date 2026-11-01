using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    public sealed class FlowImageZoom : MonoBehaviour
    {
        public TMP_Text title;
        public Image image;
        public Button close;
        private void Awake() { close.onClick.AddListener(() => Destroy(gameObject)); }
        private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Destroy(gameObject); }
    }
}
