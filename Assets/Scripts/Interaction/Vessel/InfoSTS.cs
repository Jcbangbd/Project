using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZPMC.VR.Unity
{
    public class InfoSTS : MonoBehaviour
    {
        public string NestName = "Vessel004";
        public int IndxSTS = 0;
        [SerializeField]
        private int Bay = 0;
        private InfoCell[] cells;
        [SerializeField]
        private Toggle relativeToggle = null;
        [SerializeField]
        private PlanWorkType workType = PlanWorkType.Discharge;
        [SerializeField]
        private bool isInitial = false;


        public static Dictionary<int, InfoSTS> DischargeMarshal = new Dictionary<int, InfoSTS>();
        public static Dictionary<int, InfoSTS> LoadMarshal = new Dictionary<int, InfoSTS>();
        // Use this for initialization
        void Start()
        {
            if (this.workType == PlanWorkType.Discharge)
            {
                if (!DischargeMarshal.ContainsKey(this.IndxSTS))
                    DischargeMarshal.Add(this.IndxSTS, this);
            }
            else
            {
                if (!LoadMarshal.ContainsKey(this.IndxSTS))
                    LoadMarshal.Add(this.IndxSTS, this);
            }
            cells = GetComponentsInChildren<InfoCell>();
            SequenceList<InfoCell> cellMarshal = new SequenceList<InfoCell>();
            for (int i = 0; i < this.cells.Length; i++)
                cellMarshal.Enqueue(cells[i]);
            this.cells = cellMarshal.ConvertToArray();

            if (!this.isInitial)
                this.gameObject.SetActive(false);
        }

        public List<CellCode> BuildCode()
        {
            List<CellCode> results = new List<CellCode>(cells.Length);
            if (this.relativeToggle.isOn)
            {
                for (int i = 0; i < this.cells.Length; i++)
                {
                    if (this.cells[i].isOn)
                        results.Add(new CellCode() { NestName = this.NestName, Bay = this.Bay, Column = cells[i].Column, Tier = cells[i].tier });
                }
            }
            return results;
        }

        public bool IsDischargeResource { get { return workType == PlanWorkType.Discharge; } }

        // 自动实验读取实际船上源箱，不依赖隐藏页面的Toggle状态。
        public List<CellCode> GetAutomaticSourceCodes()
        {
            var result = new List<CellCode>();
            var manager = VesselManager.GlobalInstance();
            var buffer = manager == null ? null : manager.FindVesselBuffer(NestName);
            if (buffer == null) return result;
            foreach (var cell in GetComponentsInChildren<InfoCell>(true))
            {
                var column = cell.GetComponentInParent<InfoColumn>(true);
                if (column == null || buffer.FindContainer(Bay, column.Column, cell.tier) == null) continue;
                result.Add(new CellCode { NestName = NestName, Bay = Bay, Column = column.Column, Tier = cell.tier });
            }
            result.Sort((a,b) => a.Tier != b.Tier ? b.Tier.CompareTo(a.Tier) : a.Column.CompareTo(b.Column));
            return result;
        }

        private void OnDestroy()
        {
            InfoSTS value;
            if (DischargeMarshal.TryGetValue(IndxSTS, out value) && value == this) DischargeMarshal.Remove(IndxSTS);
            if (LoadMarshal.TryGetValue(IndxSTS, out value) && value == this) LoadMarshal.Remove(IndxSTS);
        }

        public void OnAllSelecting(bool isOn)
        {
            for (int i = 0; i < this.cells.Length; i++)
            {
                this.cells[i].ChangeManual(isOn);
            }
        }
    }

    public class CellCode
    {
        public string NestName { get; set; }
        public int Bay { get; set; }
        public int Column { get; set; }
        public int Tier { get; set; }
    }

    public enum PlanWorkType
    {
        Discharge = 1,
        Load = 2,
    }
}
