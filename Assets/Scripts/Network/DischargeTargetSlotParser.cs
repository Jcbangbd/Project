using System;

namespace ZPMC.VR.Unity
{
    public static class DischargeTargetSlotParser
    {
        /// <summary>
        /// A40017011
        ///
        /// 解析为：
        ///
        /// BlockName = A40
        /// Bay        = 17
        /// Column     = 1
        /// Tier       = 1
        /// </summary>
        public static bool TryParse(
            string boxNumber,
            out DischargeTargetSlot target,
            out string error)
        {
            target = null;
            error = null;


            if (string.IsNullOrEmpty(
                boxNumber))
            {
                error =
                    "BoxNumber 不能为空";

                return false;
            }


            string code =
                boxNumber
                    .Trim()
                    .ToUpperInvariant();


            // 当前格式：
            //
            // A40 + 017 + 01 + 1
            //
            // 共 9 位
            if (code.Length != 9)
            {
                error =
                    "BoxNumber 长度错误: "
                    + code
                    + "，正确示例 A40017011";

                return false;
            }


            try
            {
                string blockName =
                    code.Substring(
                        0,
                        3);


                int bay =
                    int.Parse(
                        code.Substring(
                            3,
                            3));


                int column =
                    int.Parse(
                        code.Substring(
                            6,
                            2));


                int tier =
                    int.Parse(
                        code.Substring(
                            8,
                            1));


                target =
                    new DischargeTargetSlot
                    {
                        //BoxNumber = code,

                        BlockName = blockName,

                        Bay = bay,

                        Column = column,

                        Tier = tier
                    };


                return true;
            }
            catch (Exception ex)
            {
                error =
                    "BoxNumber 解析失败: "
                    + code
                    + ", "
                    + ex.Message;

                return false;
            }
        }
    }
}