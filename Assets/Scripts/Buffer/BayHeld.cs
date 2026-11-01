using System.Collections;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    /// <summary>
    /// Bay
    /// </summary>
    public class BayHeld : IEnumerable
	{
        public MapBuffer AttachedBuffer { get; set; }
        public int BayNO { get; set; }
        private SortedList<int, CellHeld> cellMarshal;

        /// <summary>
        /// Cell Count
        /// </summary>
        public int Count
        {
            get { return this.cellMarshal.Count; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="bayNO">Bay NO</param>
        /// <param name="attachedBlock">BlockManager</param>
        public BayHeld(MapBuffer buffer, int bayNO)
        {
            this.AttachedBuffer = buffer;
            this.cellMarshal = new SortedList<int, CellHeld>();
            this.BayNO = bayNO;
        }

        public CellHeld FindCell(int column)
        {
            if (this.cellMarshal.ContainsKey(column))
                return this.cellMarshal[column];
            else
                return null;
        }

        /// <summary>
        /// Get or Create the Cell
        /// </summary>
        /// <param name="columnNO">Column NO</param>
        /// <returns>Cell</returns>
        public CellHeld FindOrCreateCell(int columnNO)
        {
            if (this.cellMarshal.ContainsKey(columnNO))
                return this.cellMarshal[columnNO];
            else
            {
                CellHeld newCell;
                if (this.AttachedBuffer.GetType()==typeof(VesselBuffer))
                    newCell=new DeckCell(columnNO);
                else
                    newCell = new CellHeld(columnNO);
                this.cellMarshal.Add(columnNO, newCell);
                newCell.AttchedBay = this;
                return newCell;
            }
        }

        public Container RemoveContainer(int columnNO, int tierNO)
        {
            CellHeld cell = this.cellMarshal[columnNO];
            Container container = cell.RemoveContainer(tierNO);
            if (cell.Count < 1)
                this.cellMarshal.Remove(columnNO);
            return container;
        }

        public void DestroyAllContainer()
        {
            foreach (var item in this.cellMarshal.Values)
            {
                item.DestroyAllContainer();
            }
            this.cellMarshal.Clear();
        }

        public IEnumerator GetEnumerator()
        {
            return this.cellMarshal.Values.GetEnumerator();
        }
	}
}
