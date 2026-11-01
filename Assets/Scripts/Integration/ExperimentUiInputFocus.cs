using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Vtp.VR.Integration
{
    public sealed class ExperimentUiInputFocus : MonoBehaviour
    {
        public TMP_InputField Input;
        public Outline Border;
        public Image Accent;
        private bool _lastFocused, _initialized;
        private void LateUpdate()
        {
            if(Input==null)return;
            bool focused=Input.isFocused;
            if(_initialized && focused==_lastFocused)return;
            _initialized=true;_lastFocused=focused;
            if(Border!=null)Border.effectColor=focused?(Color)ExperimentUiTheme.Focus:ExperimentUiTheme.Border;
            if(Accent!=null)Accent.color=focused?(Color)ExperimentUiTheme.Focus:ExperimentUiTheme.Border;
        }
    }
}
