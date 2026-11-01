using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

namespace ZPMC.VR.Unity
{
    /// <summary>
    /// 一个箱位图模板
    /// </summary>
    [Serializable]
    public class MapTemplate
    {
        public List<MapFunction> FunctionList { get; set; }
        public List<InstanceInfo> InstanceList { get; set; }

        public MapTemplate()
        {
            this.FunctionList = new List<MapFunction>();
            this.InstanceList=new List<InstanceInfo> ();
        }

        public MapFunction GetMapTemplate(string name)
        {
            foreach (var item in FunctionList)
            {
                if (item.FunctionName == name)
                    return item;
            }
            return null;
        }


    }

    [Serializable]
    public class Key2Value
    {
        public int Key { get; set; }
        public float Value { get; set; }

        public Key2Value()
        {
        }
    }



    /// <summary>
    /// 箱位图模板的一个维度
    /// </summary>
    [Serializable]
    public class MapFunction
    {
        public string FunctionName { get; set; }
        public List<Key2Value> NestedPairs { get; set; }
        public MapFunction()
        {
            this.NestedPairs = new List<Key2Value>();
        }
    }

    /// <summary>
    /// 一个箱位图
    /// </summary>
    public class MapTable 
    {
        public string BayFunctionName;
        public string ColumnFunctionName;
        public Dictionary<int, float> BayPosition;
        public Dictionary<int, float> ColPosition;

        public MapTable()
        {
            this.BayPosition = new Dictionary<int, float>();
            this.ColPosition = new Dictionary<int, float>();
        }

        public Vector3 GetMapPostion(int bayNo, int columnNo)
        {
            if (!this.BayPosition.ContainsKey(bayNo) || !this.ColPosition.ContainsKey(columnNo))
            {
                Debug.Log(string.Format("Error in Map={0},Bay={1},Column={2}", BayFunctionName, bayNo, columnNo));
                return Vector3.zero;
            }
            else
            {
                return new Vector3(this.BayPosition[bayNo], 0, this.ColPosition[columnNo]);
            }
        }

        public void FillMissingBays(int minBay, int maxBay)
        {
            if (this.BayPosition == null || this.BayPosition.Count == 0)
                return;

            for (int bay = minBay; bay <= maxBay; bay++)
            {
                if (this.BayPosition.ContainsKey(bay))
                    continue;

                int leftKey = -1;
                int rightKey = -1;

                foreach (int key in this.BayPosition.Keys)
                {
                    if (key < bay)
                    {
                        if (leftKey < 0 || key > leftKey)
                            leftKey = key;
                    }

                    if (key > bay)
                    {
                        if (rightKey < 0 || key < rightKey)
                            rightKey = key;
                    }
                }

                if (leftKey > 0 && rightKey > 0)
                {
                    float leftValue = this.BayPosition[leftKey];
                    float rightValue = this.BayPosition[rightKey];

                    float ratio = (float)(bay - leftKey) / (float)(rightKey - leftKey);
                    float value = Mathf.Lerp(leftValue, rightValue, ratio);

                    this.BayPosition.Add(bay, value);
                }
                else if (leftKey > 0)
                {
                    float value = this.BayPosition[leftKey] + 3.25f * (bay - leftKey);
                    this.BayPosition.Add(bay, value);
                }
                else if (rightKey > 0)
                {
                    float value = this.BayPosition[rightKey] - 3.25f * (rightKey - bay);
                    this.BayPosition.Add(bay, value);
                }
            }

            Debug.Log(
                "已补齐贝位坐标："
                + this.BayFunctionName
                + "，范围="
                + minBay
                + "-"
                + maxBay
            );
        }

        public float GetWorldOffsetBay(int bay)
        {
            return this.BayPosition[bay];
        }

        public float GetWorldOffsetColumn(int column)
        {
            return this.ColPosition[column];//this.OffsetPosition.z + 
        }
    }

    /// <summary>
    /// 箱位图的集合
    /// </summary>
    public class MapTableMarshal
    {
        private List<MapTable> mapMarshal;
        private List<InstanceInfo> infoList;

        public MapTableMarshal()
        {
            this.mapMarshal = new List<MapTable>();
            this.infoList = new List<InstanceInfo>();
        }

        public ICollection GetInstanceInfos()
        {
            return this.infoList;
        }

        public MapTableMarshal(MapTemplate settings)
            : this()
        {
            this.infoList = settings.InstanceList;

            foreach (InstanceInfo item in settings.InstanceList)
            {
                if (this.GetMapTable(item.BayFunction, item.ColumnFunction) != null)
                    continue;
                MapTable piece = new MapTable();
                piece.BayFunctionName = item.BayFunction;
                piece.ColumnFunctionName = item.ColumnFunction;

                MapFunction template = settings.GetMapTemplate(item.BayFunction);
                foreach (Key2Value pair in template.NestedPairs)
                {
                    piece.BayPosition.Add(pair.Key, pair.Value);
                }

                template = settings.GetMapTemplate(item.ColumnFunction);
                foreach (Key2Value pair in template.NestedPairs)
                {
                    piece.ColPosition.Add(pair.Key, pair.Value);
                }

                this.mapMarshal.Add(piece);
            }
        }

        public MapTable GetMapTable(string bayFunctionName,string columnFunctionName)
        {
            foreach (MapTable item in this.mapMarshal)
            {
                if (item.BayFunctionName == bayFunctionName && item.ColumnFunctionName == columnFunctionName)
                    return item;
            }
            return null;
        }

        public MapTable GetMapTableByName(string objName)
        {
            foreach (InstanceInfo item in this.infoList)
            {
                if (item.ObjectName == objName)
                    return this.GetMapTable(item.BayFunction, item.ColumnFunction);
            }
            return null;
        }
    }

    [Serializable]
    public class InstanceInfo
    {
        public string ObjectName { get; set; }
        public string PrefabAlias { get; set; }
        public string BayFunction { get; set; }
        public string ColumnFunction { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float EulerAngleY { get; set; }
        public float OffsetX { get; set; } //对于船舶有效，堆垛不需要
        public float OffsetY { get; set; } //对于船舶有效，堆垛不需要
        public float OffsetZ { get; set; } //对于船舶有效，堆垛不需要

        public InstanceInfo()
        {
        }
    }
}
