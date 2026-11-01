using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Vtp.VR.Rest;

namespace Vtp.VR.Integration
{
    public sealed class ExperimentIntroductionView : MonoBehaviour
    {
        public OperationFlowView flow;
        public Button[] tabs;
        public Button learnButton;
        public Button experimentButton;
        public TMP_Text taskContext;
        private Button[] _legacyLearn;
        private Button[] _legacyExperiment;
        private Action _openDischarge;
        private GameObject _legacyWindow;
        private int _selected;
        private static readonly string[] Ids = { "DISCHARGE", "LOAD_SHIP", "LOAD_TRUCK", "UNLOAD_TRUCK", "REHANDLE" };

        public void Initialize(Transform oldCanvas, Action openDischarge)
        {
            _openDischarge = openDischarge;
            var legacyWindow = oldCanvas.Find("Play");
            _legacyWindow = legacyWindow == null ? null : legacyWindow.gameObject;
            _legacyLearn = new Button[5];
            _legacyExperiment = new Button[5];
            string[] paths = { "DischargeShip", "LoadShip", "Dispatch", "Collect", "ReOrder" };
            for (int i=0;i<paths.Length;i++)
            {
                var panel = oldCanvas.Find("Play/Scenario/Panel/" + paths[i] + "/Panel/Image");
                if (panel != null) foreach (var button in panel.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null && label.text.Contains("学习")) _legacyLearn[i]=button;
                    else if (label != null && label.text.Contains("自定义")) _legacyExperiment[i]=button;
                }
                int index=i;
                tabs[i].onClick.AddListener(() => Select(index));
            }
            foreach (string path in new[]{"Play/Top","Play/Down","Play/Scenario"})
            {
                var old = oldCanvas.Find(path);
                if (old == null) continue;
                var group=old.GetComponent<CanvasGroup>();
                if(group==null)group=old.gameObject.AddComponent<CanvasGroup>();
                group.alpha=0; group.interactable=false; group.blocksRaycasts=false;
            }
            learnButton.onClick.AddListener(() =>
            {
                var target=_legacyLearn[_selected];
                if (target != null) { Hide(); target.onClick.Invoke(); }
            });
            experimentButton.onClick.AddListener(() =>
            {
                if (_selected == 0) { Hide(); _openDischarge?.Invoke(); }
                else if (_legacyExperiment[_selected] != null)
                { Hide(); _legacyExperiment[_selected].onClick.Invoke(); }
            });
            Select(0);
        }
        public void Select(int index)
        {
            _selected=Mathf.Clamp(index,0,4);
            flow.Show(Ids[_selected]);
            RefreshTaskContext();
            for (int i=0;i<tabs.Length;i++)
            {
                var referenceStyle = tabs[i].GetComponent<SciFiReferenceButton>();
                if (referenceStyle != null) referenceStyle.SetSelected(i == _selected);
                else ExperimentUiTheme.StyleButton(tabs[i],i==_selected);
            }
        }
        public void Show() { gameObject.SetActive(true); RefreshTaskContext(); }
        private void OnEnable() { RefreshTaskContext(); }
        private void RefreshTaskContext()
        {
            if (taskContext == null) return;
            string id = StudentAssessmentRuntime.CurrentTaskId;
            if (string.IsNullOrEmpty(id)) id = SceneNumericalExampleRuntime.CourseTaskId;
            string type = flow != null && flow.catalog != null
                ? flow.catalog.entries[_selected].taskType : string.Empty;
            taskContext.text = string.IsNullOrEmpty(id) ? "作业流程介绍 · " + type
                : "当前任务 " + id + " · " + type +
                  (SceneNumericalExampleRuntime.HasExamples && SceneNumericalExampleRuntime.CourseTaskId == id && SceneNumericalExampleRuntime.TaskType == type
                      ? " · " + SceneNumericalExampleRuntime.Examples.Length + "条" : string.Empty);
        }
        public void Hide() { gameObject.SetActive(false); }
        public static void HideAll()
        {
            foreach(var view in Resources.FindObjectsOfTypeAll<ExperimentIntroductionView>())
                if(view.gameObject.scene.IsValid())view.Hide();
        }
        public static void RestoreForMenu(GameObject window)
        {
            if (window == null || !window.activeInHierarchy) return;
            foreach (var view in Resources.FindObjectsOfTypeAll<ExperimentIntroductionView>())
                if (view.gameObject.scene.IsValid() && view._legacyWindow == window) view.Show();
        }
    }
}
