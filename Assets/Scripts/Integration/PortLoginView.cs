using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vtp.VR.Integration
{
    public sealed class PortLoginView : MonoBehaviour
    {
        public GameObject form;
        public TMP_InputField userName;
        public TMP_InputField password;
        public Button login;
        public Button changePassword;
        public Image backgroundImage;
        public TMP_Text userNameLabel;
        public TMP_Text passwordLabel;

        public static void Install(AccountManager account)
        {
            if (account == null) return;
            var canvas = account.GetComponentInParent<Canvas>(true);
            var view = canvas == null ? null : canvas.GetComponentInChildren<PortLoginView>(true);
            if (view == null)
            {
                Debug.LogWarning("[PortLoginView] 登录场景尚未保存新视图组件，请使用已迁移的 Assets/Scenes/2.unity；运行时不会创建或覆盖登录页面。");
                return;
            }
            view.Bind(account);
        }

        public void Bind(AccountManager account)
        {
            // 只绑定场景内已保存的组件，不再在Awake中生成、隐藏或重新挂载页面。
            var view = this;
            account.loginPanel = view.form;
            account.userNameLoginInput = view.userName;
            account.passwordLoginInput = view.password;
            account.loginButton = view.login;
            account.openAlterButton = view.changePassword;
        }
    }
}
