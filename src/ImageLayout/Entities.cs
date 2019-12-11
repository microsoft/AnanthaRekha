// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageLayout
{
    public enum DiagramType { FlowChart, SequenceDiagram, StateDiagram }
    public enum ShapeType { Circle, Rectangle, Rhombus, Ellipse, Parallelogram }

    public class Shape
    {
        public Shape(int id, ShapeType shapeType)
        {
            OutDegree = 0;
            InDegree = 0;
            Id = id;
            ShapeType = shapeType;
        }

        public override bool Equals(object obj)
        {
            Shape objAsShape = obj as Shape;
            return objAsShape != null ? objAsShape.GetHashCode().Equals(this.GetHashCode()) : false;
        }
        public override int GetHashCode()
        {
            return ShapeType.GetHashCode();
        }

        public override string ToString()
        {
            return $"{ShapeType} num {Id}";
        }
        public int OutDegree;
        public int InDegree;
        public int Id;
        public ShapeType ShapeType;
    }

    public class Connection
    {
        public Connection(Shape source, Shape dest, bool isArrow = true)
        {
            Source = source;
            Destination = dest;
            IsArrow = isArrow;
        }
        public Shape Source { get; }
        public Shape Destination { get; }
        public bool IsArrow { get; }

        public override bool Equals(object obj)
        {
            var conx = obj as Connection;
            return conx != null ? conx.Source.Equals(this.Source) && conx.Destination.Equals(this.Destination) && (conx.IsArrow == this.IsArrow) : false;
        }

        public override int GetHashCode()
        {
            return (this.Source.GetHashCode() << 16 | this.Destination.GetHashCode()) << 1 | (this.IsArrow ? 1 : 0);
        }

        public override string ToString()
        {
            return $"Edge from {Source} to {Destination} with Arrow: {IsArrow}";
        }
    }


    public class LogicalImage
    {
        public LogicalImage(List<Shape> shapes)
        {
            Shapes = shapes;
        }

        public LogicalImage()
        {
            Shapes = new List<Shape>();
            Connections = new List<Connection>();
        }
        public IEnumerable<Shape> Shapes;
        public IEnumerable<Connection> Connections;
    }

    public class ShapeTypeComparer : IEqualityComparer<Shape>
    {
        public bool Equals(Shape x, Shape y)
        {
            return x.ShapeType == y.ShapeType;
        }

        public int GetHashCode(Shape obj)
        {
            return obj.ShapeType.GetHashCode();
        }
    }
}
