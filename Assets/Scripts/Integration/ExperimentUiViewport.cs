using UnityEngine;
namespace Vtp.VR.Integration
{
    public sealed class ExperimentUiViewport : MonoBehaviour
    {
        public void Refresh()
        {
            var parent=transform.parent as RectTransform;
            if(parent==null || parent.rect.width<=0 || parent.rect.height<=0)return;
            float scale=Mathf.Min(parent.rect.width/1920f,parent.rect.height/1080f);
            transform.localScale=new Vector3(scale,scale,1);
        }
        private void LateUpdate(){Refresh();}
    }
}
