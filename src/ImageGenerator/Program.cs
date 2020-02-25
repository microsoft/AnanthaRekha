// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using ImageLayout;
using Bitmap = System.Drawing.Bitmap;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Layout.Incremental;
using Microsoft.Msagl.Core.DataStructures;
using System.Text;
using System.Threading;

delegate ICurve GetNodeBoundaryDelegate(Microsoft.Msagl.Drawing.Node n);
namespace ImageGenerator
{
    public enum LayoutType { SugiyamaLayout, FastIncrementalLayout, SugiyamaWithSplines  }
    class Program
    {
       
        protected static Random s_random = new Random(Guid.NewGuid().GetHashCode());
        public class json_shape
        {
            public json_shape() { }
            public json_shape(bounding_box boundingBox, String shapeType)
            {
                BoundingBox = boundingBox;
                ShapeType = shapeType;
            }
            public bounding_box BoundingBox { get; set; }
            public String ShapeType { get; set; }
        }

        public class bounding_box
        {
            public bounding_box() { }

            public bounding_box(double left, double right, double top, double bottom)
            {
                Left = (int) Math.Round(left, 0);
                Right = (int) Math.Round(right, 0);
                Top = (int) Math.Round(top * -1, 0);
                Bottom = (int) Math.Round(bottom * -1, 0);
            }
            public int Left { get; set; }
            public int Right { get; set; }
            public int Top { get; set; }
            public int Bottom { get; set; }
        }


        public class json_connection
        {
            public json_connection() { }
            public json_connection(json_shape from, json_shape to)
            {
                From = from;
                To = to;
            }
            public json_shape From { get; set; }
            public json_shape To { get; set; }
        }
        static void Main(string[] args)
        {
            if ( args.Count() < 3 )
            {
                Console.WriteLine("Arguments: <diagram_type> <number of images> <generated images path> ");
                Environment.Exit(1);
            }


            String diagram = args[0].ToLower();
            int numDiag = Int32.Parse(args[1]);
            String directory = args[2];
            try
            {
                // Determine whether the directory exists.
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine("The directory was created");
                }


            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot create directory: {0}", e.ToString());
            }
            finally { }

            //specify diagram type here
            DiagramType diagramType;
            switch (diagram)
            {
                case "flowchart":
                    diagramType = DiagramType.FlowChart;
                    break;
                case "sequencediagram":
                    diagramType = DiagramType.SequenceDiagram;
                    break;
                case "statediagram":
                    diagramType = DiagramType.StateDiagram;
                    break;
                default:
                    diagramType = DiagramType.FlowChart;
                    break;
            }


            ImageLayout.ImageConstraints constraints = new ImageLayout.ImageConstraints();
            switch (diagramType)
            {
                case DiagramType.FlowChart:
                    constraints.Width = 299; constraints.Height = 299;
                    var allowedShapes = new List<ShapeType>() { ShapeType.Circle, ShapeType.Ellipse, ShapeType.Rectangle, ShapeType.Rhombus, ShapeType.Parallelogram };
                    if (args.Length >= 4)
                        allowedShapes = args[3].ToLower().Equals("roundedrectangles") ? new List<ShapeType>() { ShapeType.Circle, ShapeType.Ellipse, ShapeType.CurvedRectangle, ShapeType.Rhombus, ShapeType.Parallelogram } : allowedShapes;
                    constraints.AllowedShapes = allowedShapes;
                    constraints.MandatoryShapes = new List<ShapeType>() { ShapeType.Ellipse };
                    constraints.SingleOccurrenceShapes = new List<ShapeType>() { ShapeType.Ellipse };
                    constraints.MaxShapes = 8; constraints.MinShapes = 3;

                    constraints.ShapeConstraints.Add(ShapeType.Ellipse,
                    new ShapeConstraints()
                    {
                        HasFixedLocation = true,
                        MinInDegree = 0,
                        MaxInDegree = 0,
                        MinOutDegree = 1,
                        MaxOutDegree = 1
                    });

                    var stopConstraints = new ShapeConstraints()
                    {
                        PlacementConstraint = (row, col) => row > 0,
                        HasFixedLocation = false,
                        MinInDegree = 1,
                        MaxInDegree = Int32.MaxValue,
                        MaxOutDegree = 0,
                        MinOutDegree = 0
                    };

                    constraints.ShapeConstraints.Add(ShapeType.Circle, stopConstraints);

                    var processConstraints = new ShapeConstraints(stopConstraints);
                    processConstraints.MaxInDegree = 1;
                    processConstraints.MinInDegree = 1;
                    processConstraints.MaxOutDegree = 1;
                    processConstraints.MinOutDegree = 1;

                    var conditionConstraints = new ShapeConstraints(stopConstraints);
                    conditionConstraints.MaxInDegree = 1;
                    conditionConstraints.MaxOutDegree = 4;
                    conditionConstraints.MinInDegree = 1;
                    conditionConstraints.MinOutDegree = 2;

                    constraints.ShapeConstraints.Add(ShapeType.Rectangle, processConstraints);
                    constraints.ShapeConstraints.Add(ShapeType.CurvedRectangle, processConstraints);
                    constraints.ShapeConstraints.Add(ShapeType.Parallelogram, processConstraints);
                    constraints.ShapeConstraints.Add(ShapeType.Rhombus, conditionConstraints);
                    break;

                case DiagramType.StateDiagram:
                    constraints.Width = 299; constraints.Height = 299;
                    constraints.AllowedShapes = new List<ShapeType>() { ShapeType.Circle, ShapeType.Ellipse, ShapeType.Rectangle };
                    constraints.MaxShapes = 4; constraints.MinShapes = 3;
                    constraints.SameTypeShapes = true;

                    var stateConstraints = new ShapeConstraints();
                    stateConstraints.MaxInDegree = 4;
                    stateConstraints.MaxOutDegree = 4;
                    stateConstraints.MinInDegree = 1;
                    stateConstraints.MinOutDegree = 1;

                    constraints.ShapeConstraints.Add(ShapeType.Rectangle, stateConstraints);
                    constraints.ShapeConstraints.Add(ShapeType.Circle, stateConstraints);
                    constraints.ShapeConstraints.Add(ShapeType.Ellipse, stateConstraints);

                    break;

                case DiagramType.SequenceDiagram:
                    constraints.Width = 299; constraints.Height = 299;
                    constraints.AllowedShapes = new List<ShapeType>() { ShapeType.Rectangle };
                    constraints.MaxShapes = 6; constraints.MinShapes = 3;
                    constraints.SameTypeShapes = true;

                    var objectConstraints = new ShapeConstraints();
                    objectConstraints.MaxInDegree = 4;
                    objectConstraints.MaxOutDegree = 4;
                    objectConstraints.MinInDegree = 1;
                    objectConstraints.MinOutDegree = 1;

                    constraints.ShapeConstraints.Add(ShapeType.Rectangle, objectConstraints);
                    break;
            }

            LogicalImageGenerator generator = new LogicalImageGenerator(constraints);
            int i = 1;
            bool shuffleShapes = false;
            if (args.Length >= 5)
                shuffleShapes = args[4].ToLower().Equals("shuffleshapes");
            if (diagramType == DiagramType.FlowChart || diagramType == DiagramType.StateDiagram)
            {
                
                foreach (var image in generator.GenerateImage(numDiag, shuffleShapes))
                {
                 
                    if (i > numDiag) break;
                    Console.WriteLine($"Figure: {i}");
                    //printImage(image, constraints);
                    /*new Thread(() => build_image(directory, image, constraints, i)).Start();*/
                    
                    
                    build_image(directory, image, constraints, i, LayoutType.SugiyamaLayout);
                    /*bool foundRectangle = false;
                    for (int imageIndex=0; imageIndex < image.Shapes.Count(); i++)
                    {
                        if (image.Shapes.ElementAt(imageIndex).ShapeType == ShapeType.Rectangle)
                        {
                            foundRectangle = true;
                            image.Shapes.ElementAt(imageIndex).ShapeType = ShapeType.CurvedRectangle;
                        }
                    }
                    if (foundRectangle)
                    {
                        build_image(directory + "\\roundedrectangles", image, constraints, i);
                    } */
                    i++;
                }
            }
            else if (diagramType == DiagramType.SequenceDiagram)
            {

                foreach (var image in generator.GenerateImage(numDiag, shuffleShapes))
                {
                    if (i > numDiag) break;
                    Console.WriteLine($"Figure: {i}");
                    //printImage(image, constraints);
                    build_image_sd(directory, image, constraints, i);
                    i++;
                }
            }


        }

        static void create_json(String directory, Graph graph, int index, bool isSquareImage)
        {
            List<json_shape> json_shapes = new List<json_shape>();
            List<json_connection> json_connections = new List<json_connection>();
            foreach (Microsoft.Msagl.Drawing.Node node in graph.Nodes)
            {
                var nodeLineWidth = node.Attr.LineWidth/2;
                var bb = new bounding_box(node.BoundingBox.Left - nodeLineWidth, node.BoundingBox.Right + nodeLineWidth, node.BoundingBox.Top + nodeLineWidth, node.BoundingBox.Bottom - nodeLineWidth);
                json_shapes.Add(new json_shape(bb, node.Attr.Id.Split()[0]));
            }
            foreach (Microsoft.Msagl.Drawing.Edge edge in graph.Edges)
            {
                var bb_src = new bounding_box(edge.SourceNode.BoundingBox.Left - edge.SourceNode.Attr.LineWidth/2, edge.SourceNode.BoundingBox.Right + edge.SourceNode.Attr.LineWidth/2, edge.SourceNode.BoundingBox.Top + edge.SourceNode.Attr.LineWidth/2, edge.SourceNode.BoundingBox.Bottom - edge.SourceNode.Attr.LineWidth/2);
                var bb_dst = new bounding_box(edge.TargetNode.BoundingBox.Left - edge.TargetNode.Attr.LineWidth/2, edge.TargetNode.BoundingBox.Right + edge.TargetNode.Attr.LineWidth/2, edge.TargetNode.BoundingBox.Top + edge.TargetNode.Attr.LineWidth/2, edge.TargetNode.BoundingBox.Bottom - edge.TargetNode.Attr.LineWidth/2);
                json_connections.Add(new json_connection(
                        new json_shape(bb_src, edge.SourceNode.Id.Split()[0]),
                        new json_shape(bb_dst, edge.TargetNode.Id.Split()[0])
                    ));
            }
            JavaScriptSerializer jss = new JavaScriptSerializer();
            string Json_Shape = new JavaScriptSerializer().Serialize(json_shapes);
            string Json_connection = new JavaScriptSerializer().Serialize(json_connections);
            string tmp1 = $"{{ \"Shapes\" : {Json_Shape} }}";
            string tmp2 = $"{{ \"Connections\" : {Json_connection} }}";
            int graphSize = 0;
            string tmp4 = $"{{ \"Height\" : {(int)graph.Height} }}";
            string tmp5 = $"{{ \"Width\" : {(int)graph.Width} }}";
            string jsonPath = System.IO.Path.Combine(directory, index + ".txt");

            if (isSquareImage)
            {
                graphSize = (int)graph.Width > (int)graph.Height ? (int)graph.Width : (int)graph.Height;
                tmp4 = $"{{ \"Height\" : {graphSize} }}";
                tmp5 = $"{{ \"Width\" : {graphSize} }}";
                jsonPath = System.IO.Path.Combine(directory, "square"+index + ".txt");
            }
           
            string tmp3 = "[" + tmp1 + "," + tmp2 + "," + tmp4 + "," + tmp5 + "]";
            string json = $"{{ \"JSON\" : {tmp3} }}";
            System.IO.File.WriteAllText(jsonPath, json);
        }

        public static void build_image_sd(String directory, LogicalImage image, ImageConstraints constraints, int index)
        {
            //draw all line
            System.Drawing.Image img = new Bitmap(constraints.Width, constraints.Height, PixelFormat.Format32bppPArgb);
            System.Drawing.Graphics graphicsObj = System.Drawing.Graphics.FromImage(img);//myform.CreateGraphics();

            System.Drawing.Pen myPen = new System.Drawing.Pen(System.Drawing.Color.Black, s_random.Next(200, 230) / 100.0f);
            System.Drawing.Pen dashPen = new System.Drawing.Pen(System.Drawing.Color.Black, 2);
            dashPen.DashPattern = new float[] { 4, 2 };

            int b = s_random.Next(constraints.Width / 10, constraints.Width / 7);//buffer
            int s = (int)((float)(constraints.Width - b) / (float)(image.Shapes.Count()));//stride;

            foreach (var shape in image.Shapes)
            {
                //Draw object
                int x = b + shape.Id * s;
                graphicsObj.DrawRectangle(myPen, new System.Drawing.Rectangle(x - (int)(s * 0.4f), b, (int)(s * 0.4f), b / 2));
                graphicsObj.DrawLine(dashPen, x, b * 2, x, constraints.Height - 0.8f * b);
            }

            //pick connections in random order and draw
            myPen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
            int sv = (constraints.Height - 3 * b) / image.Connections.Count(); //vertical stride
            int i = 1;
            foreach (var connection in image.Connections)
            {
                ImageLayout.Shape src = connection.Source;
                ImageLayout.Shape dst = connection.Destination;
                int x1 = b + src.Id * s;
                int x2 = b + dst.Id * s;
                int y = 2 * b + sv * i++;
                graphicsObj.DrawLine(myPen, x1, y, x2, y);
            }

            img.Save(directory + @"\images\" + index + ".jpg");
        }

        public static void build_image(String directory, LogicalImage image, ImageConstraints constraints, int index, LayoutType layoutType)
        {
            Console.WriteLine(directory);
            Graph graph = new Graph("graph");
            GetNodeBoundaryDelegate boundry = new GetNodeBoundaryDelegate(GetNodeBoundary);
            /*double thickness = s_random.Next(20, 60) / 10.0;*/
            double nodeThickness = s_random.Next(20, 70) / 10.0;
            double edgeThickness = s_random.Next(10, 40) / 10.0;
            
            var settingSelection = s_random.Next(0, 3);
            switch (layoutType)
            {
                case LayoutType.SugiyamaLayout:
                    graph.LayoutAlgorithmSettings = new SugiyamaLayoutSettings
                    {
                        EdgeRoutingSettings = { CornerRadius = 0.0, EdgeRoutingMode = EdgeRoutingMode.Rectilinear, Padding = 1, BendPenalty = 99 },
                        AspectRatio = 1
                    };
                    break;
                case LayoutType.FastIncrementalLayout:
                    graph.LayoutAlgorithmSettings = new FastIncrementalLayoutSettings
                    {
                        RouteEdges = true,
                        AvoidOverlaps = true,
                        ApplyForces = true,
                        //RepulsiveForceConstant = 0.5,
                        AttractiveForceConstant = 0,
                        RespectEdgePorts = true
                    };
                    break;
                case LayoutType.SugiyamaWithSplines:
                    graph.LayoutAlgorithmSettings = new SugiyamaLayoutSettings
                    {
                        EdgeRoutingSettings = { CornerRadius = 0.0, EdgeRoutingMode = EdgeRoutingMode.SugiyamaSplines, Padding = 1, BendPenalty = 99 },
                        AspectRatio = 1
                    };
                    break;
                default:
                    graph.LayoutAlgorithmSettings = new SugiyamaLayoutSettings
                    {
                        EdgeRoutingSettings = { CornerRadius = 0.0, EdgeRoutingMode = EdgeRoutingMode.Rectilinear, Padding = 1, BendPenalty = 99 },
                        AspectRatio = 1
                    };
                    break;
            }


            foreach (var connection in image.Connections)
            {
                
                ImageLayout.Shape src = connection.Source;
                ImageLayout.Shape dst = connection.Destination;
                String label = "";//s_random.Next(0,10) > 1 ? RandomString(s_random.Next(4, 9)) :
                var edge = graph.AddEdge(src.ToString(), label, dst.ToString());
                if(edge.Label != null)
                edge.Label.FontSize = 6;
                edge.Attr.LineWidth = edgeThickness;
            }

            foreach (var shape in image.Shapes)
            {
                
                Microsoft.Msagl.Drawing.Node node = graph.FindNode(shape.ToString());
                node.Attr.Shape = Microsoft.Msagl.Drawing.Shape.DrawFromGeometry;
                node.NodeBoundaryDelegate = new DelegateToSetNodeBoundary(boundry);
                node.Label.Text = "";//s_random.Next(0, 10) > 1 ? RandomString(7) : 
                node.Attr.LineWidth = nodeThickness;
                if (node.Label != null)
                    node.Label.FontSize = 6;
                //node.Attr.FillColor = new Microsoft.Msagl.Drawing.Color((byte)s_random.Next(255), (byte)s_random.Next(255), (byte)s_random.Next(255));
            }

            var renderer = new GraphRenderer(graph);
            renderer.CalculateLayout();
            Point topleft = graph.GeometryGraph.BoundingBox.LeftTop;
            graph.GeometryGraph.Transform(new PlaneTransformation(1, 0, 0 - topleft.X, 0, 1, 0 - topleft.Y));//fix bounding box
            
            //for graph height and width
            Bitmap bitmap = new Bitmap((int)graph.Width, (int)graph.Height, PixelFormat.Format32bppPArgb);//PixelFormat.Format32bppPArgb);
            renderer.Render(bitmap);
            bitmap.Save(System.IO.Path.Combine(directory, index + ".jpg"));
            create_json(directory, graph, index, false);

            //for 1:1 aspect ratio
            /*int image_height = (int)graph.Width > (int)graph.Height ? (int)graph.Width : (int)graph.Height;
            Bitmap bitmap_11 = new Bitmap(image_height, image_height, PixelFormat.Format32bppPArgb);//PixelFormat.Format32bppPArgb);
            renderer.Render(bitmap_11);
            bitmap_11.Save(System.IO.Path.Combine(directory, "square"+index + ".jpg"));
            create_json(directory, graph, index, true);*/
        }

        public static ICurve GetNodeBoundary(Microsoft.Msagl.Drawing.Node n)
        {
            double cell = s_random.Next(50, 80);
            double ecc = (((double)s_random.Next(20, 100) / 100.00) * cell);
            Console.WriteLine(n.Id);
            switch (n.Id.Split()[0])
            {
                case "Rhombus":
                    double height = ecc / 4.0 >= n.Attr.LineWidth ? ecc / 2.0 : n.Attr.LineWidth * 2;
                    return CurveFactory.CreateDiamond(cell / 2.0, height, new Microsoft.Msagl.Core.Geometry.Point());
                case "Circle":
                    return CurveFactory.CreateCircle(cell / 2.0, new Microsoft.Msagl.Core.Geometry.Point());
                case "Ellipse":
                    if(s_random.Next(0, 2) == 0)
                    {
                        goto case "ElongatedEllipse";
                    }
                    return CurveFactory.CreateEllipse(cell / 2.0, ecc / 2.0, new Microsoft.Msagl.Core.Geometry.Point());
                case "Rectangle":
                    /*if (s_random.Next(0, 2) == 0)
                        goto case "CurvedRectangle";*/
                    return CurveFactory.CreateRectangle(cell, ecc, new Point());
                case "Parallelogram":
                    {
                        ICurve baseRect = CurveFactory.CreateRectangle(cell, ecc, new Point());
                        return baseRect.Transform(new PlaneTransformation(1, (double)s_random.Next(0, 100) / 100.00, 0, 0, 1, 0));
                    }
                case "CurvedRectangle":
                    return CurveFactory.CreateRectangleWithRoundedCorners(cell * 2, ecc, ecc / 4, ecc / 4, new Microsoft.Msagl.Core.Geometry.Point());
                case "Process":
                    {
                        Curve curve = (Curve)CurveFactory.CreateRectangle(cell * 2, ecc, new Microsoft.Msagl.Core.Geometry.Point());
                        Curve curveInner = (Curve)CurveFactory.CreateRectangle(cell * 1.5, ecc, new Microsoft.Msagl.Core.Geometry.Point());
                        Curve.CloseCurve(curve);
                        Curve.CloseCurve(curveInner);
                        Curve.AddLineSegment(curve, curve.Start, curveInner.Start);
                        curve.AddSegment(curveInner);
                        return curve;
                    }
                case "ElongatedEllipse":
                    {
                        var curve = new Curve();
                        double x = cell;
                        double y = ecc;
                        double r = x / 2;
                        curve.AddSegment(new Ellipse(1.5 * Math.PI, 2.5 * Math.PI, new Point(r, 0), new Point(0, y), new Point(x, 0)));
                        curve.AddSegment(new LineSegment(curve.End, new Point(-1 * x, y)));
                        curve.AddSegment(new Ellipse(0.5 * Math.PI, 1.5 * Math.PI, new Point(r, 0), new Point(0, y), new Point(-1 * x, 0)));
                        Curve.CloseCurve(curve);
                        return curve;
                    }
                case "Database":
                    {
                        var curve = new Curve();
                        double x = ecc;
                        double y = cell;
                        double r = y / 2;
                        curve.AddSegment(new Ellipse(new Point(x, 0), new Point(0, r), new Point(0, 0)));
                        curve.AddSegment(new Ellipse(0, Math.PI, new Point(x, 0), new Point(0, r), new Point(0, 0)));
                        curve.AddSegment(new LineSegment(curve.End, new Point(-1 * x, -1 * y)));
                        curve.AddSegment(new LineSegment(curve.End, new Point(x, -1 * y)));
                        Curve.CloseCurve(curve);
                        return curve;
                    }
            }
            throw new Exception("unrecognised shape type");
        }

        public static string RandomString(int size)
        {
            StringBuilder builder = new StringBuilder();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(s_random.Next(65, 123));
                builder.Append(ch);
            }
            
            return builder.ToString();
        }
        public static void printImage(LogicalImage image, ImageConstraints constraints)
        {
            foreach (var connection in image.Connections)
            {
                Console.WriteLine(connection);
            }
        }
    }
}

