using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZPMC.VR.Unity
{
    public static class DischargeTargetTxtReader
    {
        public static List<string> ReadBoxNumbers(string filePath)
        {
            List<string> result = new List<string>();

            if (!File.Exists(filePath))
            {
                Debug.LogError("未找到目标箱位txt文件：" + filePath);
                return result;
            }

            string[] lines = File.ReadAllLines(filePath);

            foreach (string rawLine in lines)
            {
                if (string.IsNullOrEmpty(rawLine))
                    continue;

                string line = rawLine.Trim();

                if (string.IsNullOrEmpty(line))
                    continue;

                // 跳过表头
                if (line.Equals("BoxNumber", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                // 支持类似：BoxNumber=A40024051
                if (line.Contains("="))
                {
                    string[] parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        line = parts[1].Trim();
                    }
                }

                result.Add(line);
            }

            Debug.Log("成功从txt读取目标箱位数量：" + result.Count);

            return result;
        }
    }
}