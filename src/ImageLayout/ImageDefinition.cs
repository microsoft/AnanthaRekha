using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageLayout
{

    public class ImageConstraints
    {
        public ImageConstraints()
        {
            AllowedShapes = new List<ShapeType>();
            MandatoryShapes = new List<ShapeType>();
            SingleOccurrenceShapes = new List<ShapeType>();
            ShapeConstraints = new Dictionary<ShapeType, ShapeConstraints>();
            DisallowedConnections = new HashSet<Connection>();
        }
        public int Width;
        public int Height;
        public List<ShapeType> AllowedShapes;
        public List<ShapeType> MandatoryShapes;
        public List<ShapeType> SingleOccurrenceShapes;
        public int Rows;
        public int Columns;
        public float CellWidth => ((float)Width) / Columns;
        public float CellHeight =>  ((float)Height) / Rows;

        public int MinShapes;
        public int MaxShapes;
        public HashSet<Connection> MandatoryConnections;
        public HashSet<Connection> DisallowedConnections;
        public Dictionary<ShapeType, ShapeConstraints> ShapeConstraints;
        public bool DuplicateConnectionsAllowed;
        public bool SameTypeShapes;

    }

    public class ShapeConstraints
    {
        public ShapeConstraints()
        {
            MaxInDegree = MaxOutDegree = Int32.MaxValue;
            MinInDegree = MinOutDegree = 0;
            HasFixedLocation = false;
            PlacementConstraint = (row, col) => true;
        }

        public ShapeConstraints(ShapeConstraints source)
        {
            this.MaxInDegree = source.MaxInDegree; this.MaxOutDegree = source.MaxOutDegree;
            this.MinInDegree = source.MinInDegree; this.MinOutDegree = source.MinOutDegree;
            this.HasFixedLocation = source.HasFixedLocation;

            this.PlacementConstraint = source.PlacementConstraint;
        }
        public int MaxInDegree;
        public int MinInDegree;
        public int MaxOutDegree;
        public int MinOutDegree;
        public bool HasFixedLocation;
        public Func<int, int, bool> PlacementConstraint;

    }

    
}
