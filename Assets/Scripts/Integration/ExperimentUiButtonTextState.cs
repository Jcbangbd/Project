using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Vtp.VR.Integration
{
    public sealed class ExperimentUiButtonTextState : MonoBehaviour
    {
        public Button Button;
        public bool Primary;
        private TMP_Text _label;
        private bool _lastInteractable,_initialized;
        private void LateUpdate()
        {
            if(Button==null)return;
            if(_label==null)_label=Button.GetComponentInChildren<TMP_Text>();
            if(_initialized && _lastInteractable==Button.interactable)return;
            _initialized=true;_lastInteractable=Button.interactable;
            if(_label!=null)_label.color=Button.interactable
                ?Primary?(Color)ExperimentUiTheme.PrimaryText:ExperimentUiTheme.TextColor:ExperimentUiTheme.Muted;
        }
    }
}
