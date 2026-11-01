using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    /// <summary>卸船九步骤以原图图标重排；其他流程仍展示原图，不改动实验步骤。</summary>
    public sealed class DischargeFlowDiagram : MonoBehaviour
    {
        public GameObject stepLayout;
        public Image originalDiagram;
        public void Show(string optionId)
        {
            bool discharge = optionId == "DISCHARGE";
            if (stepLayout != null) stepLayout.SetActive(discharge);
            if (originalDiagram != null) originalDiagram.gameObject.SetActive(!discharge);
        }
    }
}
