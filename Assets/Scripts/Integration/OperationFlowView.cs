using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    public sealed class OperationFlowView : MonoBehaviour
    {
        public OperationFlowCatalog catalog;
        public TMP_Text heading;
        public TMP_Text description;
        public Image diagram;
        public Button zoomButton;
        public bool compact;
        private OperationFlowCatalog.Entry _entry;
        private void Awake()
        {
            if (zoomButton != null) zoomButton.onClick.AddListener(OpenZoom);
        }
        public void Show(string optionId)
        {
            if (catalog == null) catalog = Resources.Load<OperationFlowCatalog>("OperationFlowCatalog");
            _entry = catalog == null ? null : catalog.Find(optionId);
            gameObject.SetActive(_entry != null);
            if (_entry == null) return;
            heading.text = _entry.title;
            description.text = compact && !string.IsNullOrEmpty(_entry.shortDescription)
                ? _entry.shortDescription : _entry.description;
            diagram.sprite = _entry.diagram;
            diagram.preserveAspect = true;
            var stepDiagram = GetComponentInChildren<DischargeFlowDiagram>(true);
            if (stepDiagram != null) stepDiagram.Show(optionId);
            var stepsHeading = transform.Find("DiagramCard/StepsHeading");
            if (stepsHeading != null) stepsHeading.GetComponent<TMP_Text>().text = optionId == "DISCHARGE"
                ? "岸桥卸船作业九个步骤" : _entry.title + " · 作业步骤";
            var scroll=description.GetComponentInParent<ScrollRect>();
            if(scroll!=null)scroll.verticalNormalizedPosition=1;
        }
        public void UseQuestionLayout()
        {
            diagram.rectTransform.anchorMax=new Vector2(.46f,diagram.rectTransform.anchorMax.y);
            var scroll=description.GetComponentInParent<ScrollRect>();
            if(scroll!=null)
            {
                var rect=scroll.GetComponent<RectTransform>();
                rect.anchorMin=new Vector2(.49f,rect.anchorMin.y);
            }
        }
        private void OpenZoom()
        {
            if (_entry == null) return;
            var prefab = Resources.Load<FlowImageZoom>("FlowImageZoom");
            if (prefab == null) return;
            var zoom = Instantiate(prefab);
            zoom.GetComponent<Canvas>().sortingOrder = 31000;
            zoom.title.text = _entry.title;
            zoom.image.sprite = _entry.diagram;
            zoom.image.preserveAspect = true;
        }
    }
}
