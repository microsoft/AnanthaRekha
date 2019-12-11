using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Z3;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

namespace ImageLayout
{
    public class LogicalImageGenerator
    {
        protected ImageConstraints _imageConstraints;
        protected static Random s_random = new Random();
        int[] leader = Enumerable.Repeat(0, 55).ToArray(); // 55 = imageconstraints's maxshapes
        Dictionary<int, Shape> idToShape = new Dictionary<int, Shape>();// cleared after each image is made
        int numberofShapes;


        private const int S_BITVEC_SIZE = 6;

        public LogicalImageGenerator(ImageConstraints constraints)
        {
            _imageConstraints = constraints;
        }
        public IEnumerable<LogicalImage> GenerateImage(int numberOfDiagrams)
        {
            for (int l = 0; l < numberOfDiagrams; l++)
            {

                numberofShapes = s_random.Next(_imageConstraints.MinShapes, _imageConstraints.MaxShapes + 1);
                IEnumerable<Shape> vertices = chooseShapes(numberofShapes);
                Microsoft.Z3.Global.ToggleWarningMessages(true);
                Log.Open("test.log");

                //Trace.Write("Z3 Full Version: ");
                //Trace.WriteLine(Microsoft.Z3.Version.ToString());

                int n = vertices.Count();
                int startIndex = 0;

                int[] maxOutDeg = new int[n];
                int[] maxInDeg = new int[n];
                int[] minOutDeg = new int[n];
                int[] minInDeg = new int[n];
                for (int i = 0; i < n; i++)
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
                    IntExpr[][] X = new IntExpr[n][];
                    for (uint i = 0; i < n; i++)
                    {
                        X[i] = new IntExpr[n];
                        for (uint j = 0; j < n; j++)
                            X[i][j] = (IntExpr)ctx.MkConst(ctx.MkSymbol("x_" + (i) + "_" + (j)), ctx.IntSort);
                    }

                    //Debug
                    Trace.WriteLine("X");
                    for (uint i = 0; i < n; i++) {
                        for (uint j = 0; j < n; j++) {
                            Trace.Write($"{X[i][j]} ");
                        }
                        Trace.WriteLine("");
                    }
                    Trace.Flush();
                    //End Debug

                    // each cell contains a value in {0,1}
                    Expr[][] cells_c = new Expr[n][];
                    for (uint i = 0; i < n; i++)
                    {
                        cells_c[i] = new BoolExpr[n];
                        for (uint j = 0; j < n; j++)
                        {
                            cells_c[i][j] = ctx.MkOr(
                                ctx.MkEq(ctx.MkInt(1), X[i][j]),
                                ctx.MkEq(X[i][j], ctx.MkInt(0))
                                );
                        }
                    }

                    //Debug
                    Trace.WriteLine("CELL_C");
                    for (uint i = 0; i < n; i++) {
                        for (uint j = 0; j < n; j++) {
                            Trace.Write($"{cells_c[i][j]} ");
                        }
                        Trace.WriteLine("");
                    }
                    Trace.Flush();
                    //EndDebug

                    // no self connections
                    BoolExpr[] diag_c = new BoolExpr[n];
                    for (uint j = 0; j < n; j++)
                    {
                        diag_c[j] = ctx.MkEq(X[j][j], ctx.MkInt(0));
                    }

                    // each row has fixed out-degree range
                    BoolExpr[] rows_c = new BoolExpr[n];
                    for (uint i = 0; i < n; i++)
                    {
                        rows_c[i] = ctx.MkAnd(
                            ctx.MkLe(ctx.MkInt(minOutDeg[i]), ctx.MkAdd(X[i])),
                            ctx.MkLe(ctx.MkAdd(X[i]), ctx.MkInt(maxOutDeg[i]))
                        );
                    }

                    //Debug
                    Trace.WriteLine("ROWS:");
                    for (uint i = 0; i < n; i++)
                    {
                        Trace.WriteLine($"{rows_c[i]} ");
                    }
                    //EndDebug


                    // each column has fixed in-degree range
                    BoolExpr[] cols_c = new BoolExpr[n];
                    for (uint j = 0; j < n; j++)
                    {
                        IntExpr[] column = new IntExpr[n];
                        for (uint i = 0; i < n; i++)
                            column[i] = X[i][j];

                        cols_c[j] = ctx.MkAnd(
                            ctx.MkLe(ctx.MkInt(minInDeg[j]), ctx.MkAdd(column)),
                            ctx.MkLe(ctx.MkAdd(column), ctx.MkInt(maxInDeg[j]))
                        );
                    }

                    //Debug
                    Trace.WriteLine("COLS:");
                    for (uint i = 0; i < n; i++)
                    {
                        Trace.WriteLine($"{cols_c[i]} ");
                    }
                    //EndDebug



                    Solver s = ctx.MkSolver();
                    BoolExpr flowChart_c = ctx.MkTrue();
                    foreach (BoolExpr[] t in cells_c)
                        flowChart_c = ctx.MkAnd(ctx.MkAnd(t), flowChart_c);
                    flowChart_c = ctx.MkAnd(ctx.MkAnd(diag_c), flowChart_c);
                    flowChart_c = ctx.MkAnd(ctx.MkAnd(rows_c), flowChart_c);
                    flowChart_c = ctx.MkAnd(ctx.MkAnd(cols_c), flowChart_c);

                    /*
                    // there exists a path from the start state
                    var sf = ctx.MkFixedpoint();
                    BoolSort B = ctx.BoolSort;
                    FuncDecl edge = ctx.MkFuncDecl("edge", new Sort[] { ctx.IntSort, ctx.IntSort }, B);
                    FuncDecl path = ctx.MkFuncDecl("path", new Sort[] { ctx.IntSort, ctx.IntSort }, B);
                    IntExpr x = (IntExpr)ctx.MkBound(0, ctx.IntSort);
                    IntExpr y = (IntExpr)ctx.MkBound(1, ctx.IntSort);
                    IntExpr z = (IntExpr)ctx.MkBound(2, ctx.IntSort);
                    sf.RegisterRelation(edge);
                    sf.RegisterRelation(path);


                    for (uint i = 0; i < n; i++)
                    {
                        for (uint j = 0; j < n; j++)
                        {
                            sf.AddRule(ctx.MkImplies(ctx.MkEq(X[i][j], ctx.MkInt(1)), (BoolExpr)edge[ctx.MkInt(i), ctx.MkInt(j)]));
                        }
                    }
                    sf.AddRule(ctx.MkImplies((BoolExpr)edge[x, y], (BoolExpr)path[x, y]));
                    sf.AddRule(ctx.MkImplies(ctx.MkAnd((BoolExpr)path[x, y], (BoolExpr)path[y, z]),
                                            (BoolExpr)path[x, z]));

                    Trace.WriteLine(sf.Rules.ToString());
                    //BoolExpr[] start_c = new BoolExpr[n];
                    BoolExpr start_c = ctx.MkTrue();
                    for (int i = 0; i < n; i++)
                    {
                        if (i != startIndex)
                        {

                            //Status st = sf.Query((BoolExpr)path[ctx.MkInt(startIndex), ctx.MkInt(i)]);
                            //start_c[i] = (BoolExpr)ctx.MkNot(ctx.MkEq(sf.GetAnswer(), null));

                            //start_c = ctx.MkAnd((BoolExpr)path[ctx.MkInt(startIndex), ctx.MkInt(i)], start_c);

                            sf.Assert((BoolExpr)path[ctx.MkInt(startIndex), ctx.MkInt(i)]);
                            //Trace.WriteLine("done " + i);
                        } //else
                            //start_c[i] = ctx.MkTrue();
                    }

                    //Add path to start as a constraint
                    //sf.Query(start_c);
                    //BoolExpr is_start_c = (BoolExpr)ctx.MkNot(ctx.MkEq(sf.GetAnswer(), null));
                    //flowChart_c = ctx.MkAnd(ctx.MkAnd(is_start_c), flowChart_c);

                    //flowChart_c = ctx.MkAnd(ctx.MkAnd(start_c), flowChart_c);

                    //flowChart_c = ctx.MkAnd(ctx.MkAnd(sf.Rules), flowChart_c);
                    flowChart_c = ctx.MkAnd(ctx.MkAnd(sf.Assertions), flowChart_c);
                    */
                    //---------------------------------------------------------------------------------
                    //instance, we use '0' as no edge and '1' as edge exists
                    int[,] instance = new int[n, n];
                    BoolExpr instance_c = ctx.MkTrue();
                    for (uint i = 0; i < n; i++)
                        for (uint j = 0; j < n; j++)
                            instance_c = ctx.MkAnd(instance_c,
                                (BoolExpr)
                                ctx.MkITE(ctx.MkEq(ctx.MkInt(instance[i, j]), ctx.MkInt(0)),
                                            ctx.MkTrue(),
                                            ctx.MkEq(X[i][j], ctx.MkInt(instance[i, j]))));

                    s.Assert(flowChart_c);
                    s.Assert(instance_c);
                    while (s.Check() == Status.SATISFIABLE)
                    {
                        LogicalImage image = new LogicalImage(vertices.ToList());
                        Model m = s.Model;
                        Expr[,] R = new Expr[n, n];
                        for (uint i = 0; i < n; i++)
                            for (uint j = 0; j < n; j++)
                                R[i, j] = m.Evaluate(X[i][j]);
                        //Trace.WriteLine("Connections solution:");
                        for (uint i = 0; i < n; i++)
                        {
                            for (uint j = 0; j < n; j++)
                                Trace.Write(" " + R[i, j]);
                            Trace.WriteLine("");
                        }
                        Trace.Flush();

                        BoolExpr currentSolution = ctx.MkFalse();
                        for (uint i = 0; i < n; i++)
                        {
                            for (uint j = 0; j < n; j++)
                            {
                                currentSolution = ctx.MkOr(ctx.MkNot(ctx.MkEq(X[i][j], R[i, j])), currentSolution);
                            }
                        }
                        s.Assert(currentSolution);
                        if (!isFlowChart(n, startIndex, R)) continue;


                        //make connections
                        List<Connection> validConnections = new List<Connection>();
                        for (uint i = 0; i < n; i++)
                        {
                            for (uint j = 0; j < n; j++)
                            {
                                if (R[i, j].ToString().Equals("1"))
                                {
                                    var conx = new Connection(vertices.ElementAt((int)i), vertices.ElementAt((int)j));
                                    validConnections.Add(conx);
                                }
                            }
                        }
                        image.Connections = validConnections;
                        yield return image;
                        //remove previous model

                    }
                }
                idToShape.Clear();
            }
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
                idToShape.Add(shapeid++, currentShape); // shapeid is zero indexed 
            }
            return shapes;
        }

        public static bool isFlowChart(int n, int startIndex, Expr[,] R)
        {

            Trace.WriteLine("-----------------------------");
            Trace.WriteLine($"In 'isFlowChart', n:{n}, startIndex:{startIndex}");

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

                Trace.WriteLine(s.ToString());
                Trace.WriteLine(start_c.ToString());
                Trace.WriteLine("-----------------------------");

                var status = s.Query(start_c);
                if (status == Status.SATISFIABLE)
                {
                    return true;
                }
                else
                {
                    Trace.WriteLine(s.GetAnswer());
                    Trace.WriteLine("fail");
                    return false;
                }
            }
        }



    }
}


