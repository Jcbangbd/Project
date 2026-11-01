using System;
using UnityEngine;

namespace Vtp.VR.Integration
{
    [CreateAssetMenu(menuName = "Experiment UI/Operation flow catalog")]
    public sealed class OperationFlowCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string optionId;
            public string taskType;
            public string title;
            [TextArea(4, 12)] public string description;
            [TextArea(2, 5)] public string shortDescription;
            public Sprite diagram;
        }
        public Entry[] entries = new Entry[0];
        public Entry Find(string key)
        {
            foreach (Entry entry in entries)
                if (entry != null && (entry.optionId == key || entry.taskType == key)) return entry;
            return null;
        }
    }
}
