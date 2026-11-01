using System;
using UnityEngine;
using UnityEngine.UI; // ÐÂ UI ÃüÃû¿Õ¼ä

namespace UnityStandardAssets.Utility
{
    public class SimpleActivatorMenu : MonoBehaviour
    {
        // An incredibly simple menu which, when given references
        // to gameobjects in the scene
        public Text camSwitchButton; // UnityEngine.UI.Text
        public GameObject[] objects;

        private int m_CurrentActiveObject;

        private void OnEnable()
        {
            // active object starts from first in array
            m_CurrentActiveObject = 0;
            if (camSwitchButton != null && objects.Length > 0)
                camSwitchButton.text = objects[m_CurrentActiveObject].name;
        }

        public void NextCamera()
        {
            if (objects == null || objects.Length == 0) return;

            int nextactiveobject = m_CurrentActiveObject + 1 >= objects.Length ? 0 : m_CurrentActiveObject + 1;

            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].SetActive(i == nextactiveobject);
            }

            m_CurrentActiveObject = nextactiveobject;
            if (camSwitchButton != null)
                camSwitchButton.text = objects[m_CurrentActiveObject].name;
        }
    }
}
