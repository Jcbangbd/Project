using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZPMC.VR.Unity
{
    public class InfoColumn : MonoBehaviour
    {
        private SortedList<int, InfoCell> nestCells;
        [SerializeField]
        private PlanWorkType workType = PlanWorkType.Discharge;

        public int Column;

        void Awake()
        {
            this.nestCells = new SortedList<int, InfoCell>();

            InfoCell[] cells = GetComponentsInChildren<InfoCell>();

            for (int i = 0; i < cells.Length; i++)
            {
                this.nestCells.Add(cells[i].tier, cells[i]);
                cells[i].AttachedColumn = this;
            }
        }

        public void OnSelectOn(int tierNo)
        {
            int currentIndx = this.nestCells.IndexOfKey(tierNo);
            if (workType == PlanWorkType.Discharge)
            {
                int temp = currentIndx - 1;
                if (temp > -1)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(true);
                }
                temp = currentIndx + 1;
                if (temp < this.nestCells.Keys.Count)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(false);
                }
            }
            else
            {
                int temp = currentIndx - 1;
                if (temp > -1)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(false);
                }
                temp = currentIndx + 1;
                if (temp < this.nestCells.Keys.Count)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(true);
                }
            }            
        }

        public void OnSelectOff(int tierNo)
        {
            int currentIndx = this.nestCells.IndexOfKey(tierNo);
            if (workType == PlanWorkType.Discharge)
            {
                int temp = currentIndx - 1;
                if (temp > -1)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(false);
                }
                temp = currentIndx + 1;
                if (temp < this.nestCells.Keys.Count)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(true);
                }
            }
            else
            {
                int temp = currentIndx - 1;
                if (temp > -1)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(true);
                }
                temp = currentIndx + 1;
                if (temp < this.nestCells.Keys.Count)
                {
                    temp = this.nestCells.Keys[temp];
                    if (this.nestCells.ContainsKey(temp))
                        this.nestCells[temp].SetSelectable(false);
                }
            }            
        }

    }
}