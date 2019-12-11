using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImageLayout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Z3;
using System.Diagnostics;

namespace ImageLayout.Tests
{
    [TestClass()]
    public class LogicalImageGeneratorTests
    {
        [TestMethod()]
        public void isFlowChartTest()
        {
            int n = 4;
            Trace.AutoFlush = true;
            Trace.Listeners.Add(new ConsoleTraceListener());
            var R = prepareExpr(4, new List<Tuple<uint, uint>>()
            {
                new Tuple<uint,uint>(0,1),
                new Tuple<uint, uint>(1,2),
                new Tuple<uint, uint>(2,1),
                new Tuple<uint, uint>(2,3)
            });

            Assert.IsTrue(LogicalImageGenerator.isFlowChart(n, 0, R));

        }

        private Expr[,] prepareExpr(int numVertices, List<Tuple<uint,uint>> edges)
        {
            using (Microsoft.Z3.Context ctx = new Microsoft.Z3.Context()){
                Expr[,] R = new Expr[numVertices, numVertices];
                for (uint i = 0; i < numVertices; i++){
                    for (uint j = 0; j < numVertices; j++){
                        if (edges.Any(tuple => tuple.Item1 == i && tuple.Item2 == j)){
                            R[i, j] = ctx.MkInt(1);
                        } else {
                            R[i, j] = ctx.MkInt(0);
                        }
                    }
                }
                return R;
            }


        }
    }
}