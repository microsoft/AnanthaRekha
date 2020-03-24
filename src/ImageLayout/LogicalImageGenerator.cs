// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Z3;

namespace ImageLayout
{
    public class LogicalImageGenerator
    {
        protected ImageConstraints _imageConstraints;
        protected static Random s_random = new Random();
        int[] leader = Enumerable.Repeat(0, 55).ToArray(); // 55 = imageconstraints's maxshapes
        Dictionary<int, Shape> idToShape = new Dictionary<int, Shape>();// cleared after each image is made
        List<List<Shape>> allShapeCombinations = new List<List<Shape>>(); //set of sets of all combinations of all shapes



        private const int S_BITVEC_SIZE = 6;

        public LogicalImageGenerator(ImageConstraints constraints)
        {
            _imageConstraints = constraints;
        }
        public IEnumerable<LogicalImage> GenerateImage(int numberOfDiagrams, bool shuffleShapes)
        {
            /// <summary>
            /// Code written by Sri Bhargava Yadavalli
            /// </summary>
            int prevBucket;
            int numberOfBuckets = _imageConstraints.MaxShapes - _imageConstraints.MinShapes + 1;
            int bucketCapacity = numberOfDiagrams / numberOfBuckets;
            int[] buckets = new int[numberOfBuckets];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = bucketCapacity;
            }
            if (numberOfDiagrams - bucketCapacity * numberOfBuckets != 0)
            {
                buckets[buckets.Length - 1] += numberOfDiagrams - bucketCapacity * numberOfBuckets;
            }
        

            int imagesGenerated = 0;
            
            /// <summary>
            /// End of Code written by Sri Bhargava Yadavalli
            /// </summary>

            for(int numberofShapes = _imageConstraints.MinShapes; numberOfDiagrams > imagesGenerated && numberofShapes <= _imageConstraints.MaxShapes; numberofShapes++)
            {
                Console.WriteLine(numberofShapes);
                //numberofShapes = s_random.next(_imageConstraints.MinShapes, _imageConstraints.MaxShapes + 1);
                
                generateAllShapesCombinations(numberofShapes);
                /*foreach (List<Shape> shapes in allShapeCombinations)
                {
                    foreach (Shape shape in shapes)
                    {
                        Console.Write(shape.ShapeType);
                    }
                    Console.WriteLine();
                }*/
                int imageLimit = 0; 
                
                if(allShapeCombinations.Count != 0)
                imageLimit = buckets[numberofShapes - _imageConstraints.MinShapes] / allShapeCombinations.Count;
                foreach (var vertices in allShapeCombinations)
                {
                    //IEnumerable<Shape> vertices = allShapeCombinations[shapeSetIndex++];//chooseShapes(numberofShapes);

                    Microsoft.Z3.Global.ToggleWarningMessages(true);
                    Log.Open("test.log");
                    //Trace.Write("Z3 Full Version: ");
                    //Trace.WriteLine(Microsoft.Z3.Version.ToString());

                    int totalVertices = vertices.Count();
                    int startIndex = 0;

                    int[] maxOutDeg = new int[totalVertices];
                    int[] maxInDeg = new int[totalVertices];
                    int[] minOutDeg = new int[totalVertices];
                    int[] minInDeg = new int[totalVertices];
                    for (int i = 0; i < totalVertices; i++)
                    {
                        Shape shape = vertices.ElementAt(i);
                        maxOutDeg[i] = _imageConstraints.ShapeConstraints[shape.ShapeType].MaxOutDegree;
                        if (maxOutDeg[i] == Int32.MaxValue) { maxOutDeg[i] = vertices.Count(); }
                        minOutDeg[i] = _imageConstraints.ShapeConstraints[shape.ShapeType].MinOutDegree;
                        maxInDeg[i] = _imageConstraints.ShapeConstraints[shape.ShapeType].MaxInDegree;
                        if (maxInDeg[i] == Int32.MaxValue) { maxInDeg[i] = vertices.Count(); }
                        minInDeg[i] = _imageConstraints.ShapeConstraints[shape.ShapeType].MinInDegree;
                        if (shape.ShapeType == ShapeType.Ellipse) startIndex = i;
                    }

                    using (Context ctx = new Context(new Dictionary<string, string>() { { "model", "true" } }))
                    {
                        // nxn matrix of integer variables
                        IntExpr[][] X = new IntExpr[totalVertices][];
                        for (uint i = 0; i < totalVertices; i++)
                        {
                            X[i] = new IntExpr[totalVertices];
                            for (uint j = 0; j < totalVertices; j++)
                                X[i][j] = (IntExpr)ctx.MkConst(ctx.MkSymbol("x_" + (i) + "_" + (j)), ctx.IntSort);
                        }

                        //Debug
                        /*Trace.WriteLine("X");
                        for (uint i = 0; i < totalVertices; i++)
                        {
                            for (uint j = 0; j < totalVertices; j++)
                            {
                                Trace.Write($"[{X[i][j]}] ");
                            }
                            Trace.WriteLine("");
                        }
                        Trace.Flush();*/
                        //End Debug

                        // each cell contains a value in {0,1}
                        Expr[][] cells_c = new Expr[totalVertices][];
                        for (uint i = 0; i < totalVertices; i++)
                        {
                            cells_c[i] = new BoolExpr[totalVertices];
                            for (uint j = 0; j < totalVertices; j++)
                            {
                                cells_c[i][j] = ctx.MkOr(
                                    ctx.MkEq(ctx.MkInt(1), X[i][j]),
                                    ctx.MkEq(X[i][j], ctx.MkInt(0))
                                    );
                            }
                        }

                        //Debug
                       /* Trace.WriteLine("CELL_C");
                        for (uint i = 0; i < totalVertices; i++)
                        {
                            for (uint j = 0; j < totalVertices; j++)
                            {
                                Trace.Write($"{cells_c[i][j]} ");
                            }
                            Trace.WriteLine("");
                        }
                        Trace.Flush();*/
                        //EndDebug

                        // no self connections
                        BoolExpr[] diag_c = new BoolExpr[totalVertices];
                        for (uint j = 0; j < totalVertices; j++)
                        {
                            diag_c[j] = ctx.MkEq(X[j][j], ctx.MkInt(0));
                        }

                        // each row has fixed out-degree range
                        BoolExpr[] rows_c = new BoolExpr[totalVertices];
                        for (uint i = 0; i < totalVertices; i++)
                        {
                            rows_c[i] = ctx.MkAnd(
                                ctx.MkLe(ctx.MkInt(minOutDeg[i]), ctx.MkAdd(X[i])),
                                ctx.MkLe(ctx.MkAdd(X[i]), ctx.MkInt(maxOutDeg[i]))
                            );
                        }

                        //Debug
                        /*Trace.WriteLine("ROWS:");
                        for (uint i = 0; i < totalVertices; i++)
                        {
                            Trace.WriteLine($"{rows_c[i]} ");
                        }*/
                        //EndDebug

                        // each column has fixed in-degree range
                        BoolExpr[] cols_c = new BoolExpr[totalVertices];
                        for (uint j = 0; j < totalVertices; j++)
                        {
                            IntExpr[] column = new IntExpr[totalVertices];
                            for (uint i = 0; i < totalVertices; i++)
                                column[i] = X[i][j];

                            cols_c[j] = ctx.MkAnd(
                                ctx.MkLe(ctx.MkInt(minInDeg[j]), ctx.MkAdd(column)),
                                ctx.MkLe(ctx.MkAdd(column), ctx.MkInt(maxInDeg[j]))
                            );
                        }

                        //Debug
                        /*Trace.WriteLine("COLS:");
                        for (uint i = 0; i < totalVertices; i++)
                        {
                            Trace.WriteLine($"{cols_c[i]} ");
                        }*/
                        //EndDebug

                        Solver solver = ctx.MkSolver();
                        BoolExpr flowChart_c = ctx.MkTrue();
                        foreach (BoolExpr[] t in cells_c)
                            flowChart_c = ctx.MkAnd(ctx.MkAnd(t), flowChart_c);
                        flowChart_c = ctx.MkAnd(ctx.MkAnd(diag_c), flowChart_c);
                        flowChart_c = ctx.MkAnd(ctx.MkAnd(rows_c), flowChart_c);
                        flowChart_c = ctx.MkAnd(ctx.MkAnd(cols_c), flowChart_c);

                        //---------------------------------------------------------------------------------
                        //instance, we use '0' as no edge and '1' as edge exists
                        int[,] instance = new int[totalVertices, totalVertices];
                        BoolExpr instance_c = ctx.MkTrue();
                        for (uint i = 0; i < totalVertices; i++)
                            for (uint j = 0; j < totalVertices; j++)
                                instance_c = ctx.MkAnd(instance_c,
                                    (BoolExpr)
                                    ctx.MkITE(ctx.MkEq(ctx.MkInt(instance[i, j]), ctx.MkInt(0)),
                                                ctx.MkTrue(),
                                                ctx.MkEq(X[i][j], ctx.MkInt(instance[i, j]))));

                        solver.Assert(flowChart_c);
                        solver.Assert(instance_c);
                        int currentBucket = numberofShapes - _imageConstraints.MinShapes;
                        while (solver.Check() == Status.SATISFIABLE && numberOfDiagrams > imagesGenerated)
                        {
                            if (buckets[currentBucket] == 0)
                            {
                                prevBucket = numberofShapes - _imageConstraints.MinShapes - 1;
                                while (prevBucket >= 0)
                                {
                                    if (buckets[prevBucket] != 0)
                                    {
                                        currentBucket = prevBucket;
                                        break;
                                    }
                                    prevBucket--;
                                }
                                if (prevBucket < 0)
                                {

                                    break;
                                }
                            }
                            LogicalImage image = new LogicalImage(vertices.ToList());
                            Model model = solver.Model;
                            Expr[,] R = new Expr[totalVertices, totalVertices];
                            for (uint i = 0; i < totalVertices; i++)
                                for (uint j = 0; j < totalVertices; j++)
                                    R[i, j] = model.Evaluate(X[i][j]);
                            //Trace.WriteLine("Connections solution:");
                            /*for (uint i = 0; i < totalVertices; i++)
                            {
                                for (uint j = 0; j < totalVertices; j++)
                                    Trace.Write(" " + R[i, j]);
                                Trace.WriteLine("");
                            }
                            Trace.Flush();*/

                            BoolExpr currentSolution = ctx.MkFalse();
                            for (uint i = 0; i < totalVertices; i++)
                            {
                                for (uint j = 0; j < totalVertices; j++)
                                {
                                    currentSolution = ctx.MkOr(ctx.MkNot(ctx.MkEq(X[i][j], R[i, j])), currentSolution);

                                }
                            }

                            solver.Assert(currentSolution);
                            if (!isFlowChart(totalVertices, startIndex, R))
                            {
                                Trace.WriteLine("IS FLOWCHART FAILED");
                                continue;
                            }
                            Trace.WriteLine("-------------------IS FLOW CHART SUCCESS");
                            /*Console.WriteLine($"image count = {imagesGenerated}");*/
                            
                            //make connections
                            List<Connection> validConnections = new List<Connection>();
                            var shuffledShapes = vertices;
                            if(shuffleShapes)
                            {
                                shuffledShapes = ShuffleShapes(vertices);
                            }
                            for (uint i = 0; i < totalVertices; i++)
                            {
                                for (uint j = 0; j < totalVertices; j++)
                                {
                                    if (R[i, j].ToString().Equals("1"))//change R here for shuffling(idea is to multiply using a permutation-R matrix)
                                    {
                                        var conx = new Connection(shuffledShapes.ElementAt((int)i), shuffledShapes.ElementAt((int)j));
                                        validConnections.Add(conx);
                                    }
                                }
                            }
                            image.Connections = validConnections;

                            ///<summary>
                            ///Code written by Sri Bhargava Yadavalli
                            ///</summary>
                            /// <code>
                            
                            /*else
                            {
                                
                            }*/
                            buckets[currentBucket]--;
                            imageLimit--;
                            /// </code>
                            ///<summary>
                            ///Code written by Sri Bhargava Yadavalli
                            ///</summary>
                            imagesGenerated++;
                            yield return image;
                            //remove previous model
                            /*if(imageLimit == 0)
                            {
                                imageLimit = buckets[numberofShapes - _imageConstraints.MinShapes] / allShapeCombinations.Count;
                                break;
                            }*/

                        }

                    }
                    idToShape.Clear();
                    if (numberOfDiagrams <= imagesGenerated)
                    {
                        break;
                    }
                        
                }
                
            }
        }

        public static List<Shape> ShuffleShapes(List<Shape> shapes)
        {
            var count = shapes.Count;
            var last = count - 1;
            for (var i = 0; i < last; ++i)
            {
                var r = s_random.Next(i, count);
                var tmp = shapes[i];
                shapes[i] = shapes[r];
                shapes[r] = tmp;
            }
            return shapes;
        }
        protected void generateAllShapesCombinations(int numberOfShapes)
        {
            allShapeCombinations.Clear();
            List<ShapeType> shapesTypes = new List<ShapeType>();

            if (_imageConstraints.SameTypeShapes)
            {
                ShapeType type = _imageConstraints.AllowedShapes[s_random.Next(_imageConstraints.AllowedShapes.Count())];
                shapesTypes.AddRange(Enumerable.Repeat(type, numberOfShapes));
            }
            else
            {
                
                var usableShapes = _imageConstraints.AllowedShapes.Except(_imageConstraints.SingleOccurrenceShapes).ToList();
                CombinationRepetition(usableShapes.ToArray(), usableShapes.Count, numberOfShapes - _imageConstraints.MandatoryShapes.Count);
               
            }
            
            return ;
        }
        
        protected void CombinationRepetitionUtil(int[] chosen, ShapeType[] arr,
            int index, int r, int start, int end)
        {
            if (index == r)
            {
                List<ShapeType> shapeTypes = new List<ShapeType>();
                shapeTypes.AddRange(_imageConstraints.MandatoryShapes);
                for (int i = 0; i < r; i++)
                {
                    shapeTypes.Add(arr[chosen[i]]);
                }
                List<Shape> shapes = new List<Shape>();
                int shapeid = 0;
                foreach (var shapeType in shapeTypes)
                {
                    var shapeConstraint = _imageConstraints.ShapeConstraints[shapeType];
                    Shape currentShape = new Shape(shapeid++, shapeType);
                    shapes.Add(currentShape); 
                }
                allShapeCombinations.Add(shapes);
                return;
            }
    
            for (int i = start; i <= end; i++)
            {
                chosen[index] = i;
                CombinationRepetitionUtil(chosen, arr, index + 1,
                        r, i, end);
            }
            return;
        }
        
       protected void CombinationRepetition(ShapeType[] shapeTypes, int n, int r)
        {
            int[] chosen = new int[r + 1];
            CombinationRepetitionUtil(chosen, shapeTypes, 0, r, 0, n - 1);
        }
        protected IEnumerable<Shape> chooseShapes(int numShapes)
        {
            List<ShapeType> shapesTypes = new List<ShapeType>();

            if (_imageConstraints.SameTypeShapes)
            {
                ShapeType type = _imageConstraints.AllowedShapes[s_random.Next(_imageConstraints.AllowedShapes.Count())];
                shapesTypes.AddRange(Enumerable.Repeat(type, numShapes));
            }
            else
            {
                shapesTypes.AddRange(_imageConstraints.MandatoryShapes);
                //int circles = 1;
                var usableShapes = _imageConstraints.AllowedShapes.Except(_imageConstraints.SingleOccurrenceShapes).ToList();
                
                for (int i = 0; i < numShapes - _imageConstraints.MandatoryShapes.Count; i++)
                {
                    ShapeType type = usableShapes[s_random.Next(usableShapes.Count())];
                    shapesTypes.Add(type);
                }
            }
            List<Shape> shapes = new List<Shape>();
            int shapeid = 0;
            foreach (var shapeType in shapesTypes)
            {
                var shapeConstraint = _imageConstraints.ShapeConstraints[shapeType];
                Shape currentShape = new Shape(shapeid, shapeType);
                shapes.Add(currentShape);
               /*idToShape.Add(shapeid++, currentShape);*/ // shapeid is zero indexed 
            }
            /*foreach (var shape in shapes)
                Console.WriteLine(shape.ShapeType);*/
            return shapes;
        }

        public static bool isFlowChart(int n, int startIndex, Expr[,] R)
        {

            /*Trace.WriteLine("-----------------------------");
            Trace.WriteLine($"In 'isFlowChart', n:{n}, startIndex:{startIndex}");*/

            using (Context ctx = new Context())
            {
                var s = ctx.MkFixedpoint();
                BoolSort B = ctx.BoolSort;
                BitVecSort V = ctx.MkBitVecSort(S_BITVEC_SIZE);

                               
                //Function declarations
                FuncDecl edge = ctx.MkFuncDecl("edge", new Sort[] { V, V }, B);
                FuncDecl path = ctx.MkFuncDecl("path", new Sort[] { V, V }, B);


                var x = (BitVecExpr)ctx.MkBound(0, V);
                var y = (BitVecExpr)ctx.MkBound(1, V);
                var z = (BitVecExpr)ctx.MkBound(2, V);

                s.RegisterRelation(edge);
                s.RegisterRelation(path);

                //Recursive reachability rules - edge and path
                //edge[x,y] => path[x,y]
                //path[x,y] && path[y,z] => path[x,z]
                s.AddRule(ctx.MkImplies((BoolExpr)edge[x, y], (BoolExpr)path[x, y]));
                s.AddRule(ctx.MkImplies(ctx.MkAnd((BoolExpr)path[x, y], (BoolExpr)path[y, z]),
                                        (BoolExpr)path[x, z]));

                //Add a edge fact if R[i,j] == 1, 
                for (uint i = 0; i < n; i++)
                {
                    for (uint j = 0; j < n; j++)
                        if (R[i, j].ToString().Equals("1"))
                        {
                            s.AddFact(edge, i, j);
                        }
                }

                //For every vertex other than startIndex, there 
                //should be a path from startIndex to vertex
                BoolExpr start_c = ctx.MkTrue();
                for (int i = 0; i < n; i++)
                {
                    if (i != startIndex)
                    {
                        start_c = ctx.MkAnd((BoolExpr)path[ctx.MkBV(startIndex, S_BITVEC_SIZE), ctx.MkBV(i, S_BITVEC_SIZE)], start_c);
                    }
                }

               /* Trace.WriteLine(s.ToString());
                Trace.WriteLine(start_c.ToString());
                Trace.WriteLine("-----------------------------");*/

                var status = s.Query(start_c);
                if (status == Status.SATISFIABLE)
                {
                    return true;
                }
                else
                {
                    /*Trace.WriteLine(s.GetAnswer());
                    Trace.WriteLine("fail");*/
                    return false;
                }
            }
        }



    }
}


