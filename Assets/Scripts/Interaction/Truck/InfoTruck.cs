using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZPMC.VR.Unity
{
    public class InfoTruck : MonoBehaviour
    {
        public string NestName = "A37";
        public int IndxASC = 0;
        [SerializeField]
        private int Bay = 70;
        [SerializeField]
        private bool isInverse = true;

        private InfoCell[] cells;

        public static InfoTruck Instance;

        // Use this for initialization
        void Start()
        {
            cells = GetComponentsInChildren<InfoCell>();
            SequenceList<InfoCell> cellMarshal = new SequenceList<InfoCell>();
            for (int i = 0; i < this.cells.Length; i++)
                cellMarshal.Enqueue(cells[i]);
            if (isInverse)
                this.cells = cellMarshal.ConvertToArrayInverse();
            else
                this.cells = cellMarshal.ConvertToArray();
        }

        void OnEnable()
        {
            Instance = this;
        }

        public List<CellCode> BuildCode()
        {
            List<CellCode> cellMarshal = new List<CellCode>(cells.Length);
            for (int i = 0; i < this.cells.Length; i++)
            {
                if (this.cells[i].isOn)
                    cellMarshal.Add(new CellCode() { NestName = this.NestName, Bay = this.Bay, Column = cells[i].Column, Tier = cells[i].tier });
            }
            return cellMarshal;
        }
    }
}