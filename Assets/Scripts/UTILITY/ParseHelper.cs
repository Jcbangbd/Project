using System;
using System.Globalization;

namespace ZPMC.VR.Unity
{
	public class ParseHelper
	{
        /// <summary>
        /// 尝试将String转换为Integer。不抛出异常。
        /// 如果源字符串为“True”返回为1；“False”返回为0。
        /// 无匹配项则返回0值。
        /// </summary>
        /// <param name="rawValue">源字符串</param>
        /// <returns>目标值</returns>
        public static int TryAsInteger(string rawValue)
        {
            int d = 0;
            if (int.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
            else if (String.Compare(rawValue, "True", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 1;
            }
            else if (String.Compare(rawValue, "False", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 0;
            }
            else
                return 0;// throw new Exception(string.Format("Can not parse {0} to string!", rawValue));
        }

        /// <summary>
        /// 尝试将String转换为Double。不抛出异常。
        /// 如果源字符串为“True”返回为1.0；“False”返回为0.0。
        /// 无匹配项则返回0值。
        /// </summary>
        /// <param name="rawValue">源字符串</param>
        /// <returns>目标值</returns>
        public static double TryAsNumeric(string rawValue)
        {
            double d = 0.0;
            if (Double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
            else if (String.Compare(rawValue, "True", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 1.0;
            }
            else if (String.Compare(rawValue, "False", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 0.0;
            }
            else
                return 0.0;// throw new Exception(string.Format("Can not parse {0} to string!", rawValue));
        }

        /// <summary>
        /// 尝试将String转换为single。不抛出异常。
        /// 如果源字符串为“True”返回为1.0f；“False”返回为0.0f。
        /// 无匹配项则返回0值。
        /// </summary>
        /// <param name="rawValue">源字符串</param>
        /// <returns>目标值</returns>
        public static float TryAsSingle(string rawValue)
        {
            float f = 0f;
            if (float.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out f))
            {
                return f;
            }
            else if (string.Compare(rawValue, "True", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 1f;
            }
            else if (String.Compare(rawValue, "False", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 0f;
            }
            return 0f;
        }

        /// <summary>
        /// 尝试将String转换为DateTime。不抛出异常。
        /// 不成功则返回DateTime.MinValue值。
        /// </summary>
        /// <param name="rawValue">源字符串</param>
        /// <returns>目标值</returns>
        public static DateTime TryAsDateTime(string rawValue)
        {
            DateTime dt;
            if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                return dt;
            }
            else
                return DateTime.MinValue;//throw new Exception(string.Format("Can not parse {0} to DateTime!", rawValue));
        }

        /// <summary>
        /// 尝试将String转换为Bool。不抛出异常。
        /// 若源字符串为“1”或者“是”，则返回为true。
        /// 无匹配项则返回False值。
        /// </summary>
        /// <param name="rawValue">源字符串</param>
        /// <returns>目标值</returns>
        public static bool TryAsBoolean(string rawValue)
        {
            bool dt;
            if (Boolean.TryParse(rawValue, out dt))
                return dt;
            else if (String.Compare(rawValue, "1", StringComparison.InvariantCultureIgnoreCase) == 0)
                return true;
            else if (String.Compare(rawValue, "是", StringComparison.InvariantCultureIgnoreCase) == 0)
                return true;
            else
                return false; // throw new Exception(string.Format("Can not parse {0} to Boolean!", rawValue));
        }

        /// <summary>
        /// 尝试将String转换为Integer。不抛出异常。
        /// 如果源字符串为“True”返回为1；“False”返回为0。
        /// 无匹配项则返回0值。
        /// </summary>
        /// <param name="rawValue">源字符串</param>
        /// <returns>目标值</returns>
        public static byte TryAsByte(string rawValue)
        {
            byte b = 0;
            if (byte.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out b))
            {
                return b;
            }
            else if (String.Compare(rawValue, "True", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 1;
            }
            else if (String.Compare(rawValue, "False", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return 0;
            }
            else
                return 0;// throw new Exception(string.Format("Can not parse {0} to string!", rawValue));
        }

        /// <summary>
        /// 解析string为char。若源字符串为空，则解析为0；若长度大于1，则取第一个字符。
        /// </summary>
        /// <param name="rawValue">源字符串</param>
        /// <returns>目标值</returns>
        public static char TryAsChar(string rawValue)
        {
            char c;
            if (rawValue.Length < 1)
                c = '0';
            else
                c = rawValue[0];
            return c;
        }
	}
}
