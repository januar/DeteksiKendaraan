using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AForge;
using AForge.Fuzzy;
using AForge.Controls;

namespace DeteksiKendaraan
{
    class FuzzyObject
    {
        public LinguisticVariable lvKepadatanJalan;

        public FuzzyObject()
        {
            lvKepadatanJalan = new LinguisticVariable("KepadatanJalan", 0, 100);

            TrapezoidalFunction function1 = new TrapezoidalFunction(20, 30, TrapezoidalFunction.EdgeType.Right);
            FuzzySet fsSepi = new FuzzySet("Sepi", function1);
            TrapezoidalFunction function2 = new TrapezoidalFunction(20, 30, 40, 50);
            FuzzySet fsSedang = new FuzzySet("Sedang", function2);
            TrapezoidalFunction function3 = new TrapezoidalFunction(40, 50, 60, 70);
            FuzzySet fsRamai = new FuzzySet("Ramai", function3);
            TrapezoidalFunction function4 = new TrapezoidalFunction(60, 70, TrapezoidalFunction.EdgeType.Left);
            FuzzySet fsPadat = new FuzzySet("Padat", function4);

            lvKepadatanJalan.AddLabel(fsSepi);
            lvKepadatanJalan.AddLabel(fsSedang);
            lvKepadatanJalan.AddLabel(fsRamai);
            lvKepadatanJalan.AddLabel(fsPadat);
        }

        public double[][,] GetChartValue()
        {
            // get membership of some points to the cool fuzzy set
            double[][,] chartValues = new double[4][,];
            for (int i = 0; i < 4; i++)
                chartValues[i] = new double[200, 2];

            // showing the shape of the linguistic variable - the shape of its labels memberships from start to end
            int j = 0;
            for (float x = 0; x < 100; x += 0.5f, j++)
            {
                double y1 = lvKepadatanJalan.GetLabelMembership("Sepi", x);
                double y2 = lvKepadatanJalan.GetLabelMembership("Sedang", x);
                double y3 = lvKepadatanJalan.GetLabelMembership("Ramai", x);
                double y4 = lvKepadatanJalan.GetLabelMembership("Padat", x);
                Console.WriteLine(String.Format("x : {0} y1 : {1} y2 : {2} y3 : {3} y4 : {4} j : {5}", x, y1, y2, y3, y4, j));
                chartValues[0][j, 0] = x;
                chartValues[0][j, 1] = y1;
                chartValues[1][j, 0] = x;
                chartValues[1][j, 1] = y2;
                chartValues[2][j, 0] = x;
                chartValues[2][j, 1] = y3;
                chartValues[3][j, 0] = x;
                chartValues[3][j, 1] = y4;
            }

            return chartValues;
        }
    }
}
