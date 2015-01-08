using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AForge;
using AForge.Controls;

namespace DeteksiKendaraan
{
    public partial class FuzzyForm : Form
    {
        public FuzzyForm()
        {
            InitializeComponent();
            chart.RangeX = new Range(0, 100);
            chart.AddDataSeries("SEPI", Color.CornflowerBlue, Chart.SeriesType.Line, 3, true);
            chart.AddDataSeries("SEDANG", Color.LightBlue, Chart.SeriesType.Line, 3, true);
            chart.AddDataSeries("RAMAI", Color.LightCoral, Chart.SeriesType.Line, 3, true);
            chart.AddDataSeries("PADAT", Color.Firebrick, Chart.SeriesType.Line, 3, true);

            FuzzyObject fuzzy = new FuzzyObject();

            var chartValues = fuzzy.GetChartValue();
            // plot membership to a chart
            chart.UpdateDataSeries("SEPI", chartValues[0]);
            chart.UpdateDataSeries("SEDANG", chartValues[1]);
            chart.UpdateDataSeries("RAMAI", chartValues[2]);
            chart.UpdateDataSeries("PADAT", chartValues[3]);
        }
    }
}
