using UnityEngine;
using System.Collections;

namespace ZPMC.VR.Unity
{
    public class DeckCell : CellHeld
    {
        public DeckCell(int columnNO)
            :base(columnNO)
        {
        }

        public override float GetTierPosition(int tier)
        {
            if (tier <= 80)
                return -10f;

            float height = 0;
            while (tier > 82)
            {
                tier -= 2;
                if (this.ctnMarshal.ContainsKey(tier))
                    height += this.ctnMarshal[tier].HeightMetre;
                else
                    height += Container.EightHalfFeet;
            }
            return height;
        }

        //protected override void AdjustAboveContainer(int tierNO, float offset)
        //{
        //    base.AdjustAboveContainer(tierNO, offset);
        //}
    }
}