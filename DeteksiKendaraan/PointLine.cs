using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AForge;

namespace DeteksiKendaraan
{
    class PointLine
    {
        private IntPoint point1;
        private IntPoint point2;

        public IntPoint Point1 {
            set { point1 = value; }
            get { return point1; }
        }

        public IntPoint Point2 {
            set { point2 = value; }
            get { return point2; }
        }
    }
}
