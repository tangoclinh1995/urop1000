using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Sockets;
using System.Threading;

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections;



namespace DesktopStation
{
    public class GraphManager
    {
        public static int DEFAULT_MIN_Y = -3000;
        public static int DEFAULT_MAX_Y = 3000;
        public static int DEFAULT_RANGE_X = 2000;
        public static int DEFAULT_SAMPLING_INTERVAL = 10;


        
        string[] SERRIES_TITLES = { "X", "Y", "Z" };
        const int DEFAULT_X_AXIS_RANGE = 5000;
        const int REFRESH_RATE = 20;



        public LineSeries[] series;
        public LinearAxis[] axes;
        
        public int minY, maxY, rangeX;
        public bool dynamicYScaling;
        
        int counter = 0;
        double timeStamp = 0;
        public int timeStep = DEFAULT_SAMPLING_INTERVAL;
        MainWindow mainWindow;

        IDataSmoothing[] dataSmoothing = new MovingAverageDataSmoothing[3];
        double[] processedData = new double[3];
        


        public GraphManager(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            
            series = new LineSeries[3];

            for (int i = 0; i < 3; ++i)
            {
                series[i] = new LineSeries
                {
                    StrokeThickness = 1.25,
                    MarkerSize = 2,
                    MarkerType = MarkerType.Diamond,
                    Title = SERRIES_TITLES[i],                   
                };
            }

            series[0].Color = series[0].MarkerFill = OxyColor.FromRgb(255, 0, 0);  //X = Red
            series[1].Color = series[1].MarkerFill = OxyColor.FromRgb(0, 180, 0);  //Y = Green
            series[2].Color = series[2].MarkerFill = OxyColor.FromRgb(0, 0, 255);  //Z = Blue

            axes = new LinearAxis[]
                {
                    new LinearAxis
                        {
                            Position = AxisPosition.Bottom,
                            Title = "Time",
                            Minimum = 0,
                            Maximum = DEFAULT_X_AXIS_RANGE,
                        },
                    new LinearAxis
                        {
                            Position = AxisPosition.Left,
                            Title = "Acceleration",
                            Minimum = DEFAULT_MIN_Y,
                            Maximum = DEFAULT_MAX_Y,
                            MajorGridlineStyle = LineStyle.Dash,
                            MajorStep = 500
                        }                        
                };

            rangeX = DEFAULT_RANGE_X;
            minY = DEFAULT_MIN_Y;
            maxY = DEFAULT_MAX_Y;
            dynamicYScaling = false;

            for (int i = 0; i < 3; ++i)
                dataSmoothing[i] = new MovingAverageDataSmoothing();
        }



        public void Reset()
        {
            series.ToList().ForEach(e => e.Points.Clear());

            axes[0].Minimum = 0;
            axes[0].Maximum = rangeX;
            
            if ((bool)mainWindow.checkDynamicYScaling.IsChecked)
            {
                axes[1].Minimum = DEFAULT_MIN_Y;
                axes[1].Maximum = DEFAULT_MAX_Y;
            }
            else
            {
                axes[1].Minimum = minY;
                axes[1].Maximum = maxY;
            }

            counter = 0;
            timeStamp = 0;

            for (int i = 0; i < 3; ++i)
                dataSmoothing[i].Reset();
        }



        public void Update(double[] data)
        {
            timeStamp += timeStep;

            for (int i = 0; i < 3; ++i)
                series[i].Points.Add(new DataPoint(timeStamp, data[i]));

            ++counter;
            if (counter == REFRESH_RATE)
            {
                if (timeStamp > axes[0].Maximum)
                {
                    axes[0].Maximum = timeStamp;
                    axes[0].Minimum = timeStamp - rangeX;
                }

                if (dynamicYScaling)
                {
                    int minY = 10000, maxY = -10000;

                    for (int i = 0; i < 3; ++i)
                        if (series[i].IsVisible)
                        {
                            var tmp = series[i].Points.Where(e => e.X >= axes[0].Minimum);
                            minY = Math.Min((int)tmp.Min(e => e.Y), minY);
                            maxY = Math.Max((int)tmp.Max(e => e.Y), maxY);
                        }

                    axes[1].Minimum = ((minY / 1000) + Math.Sign(minY)) * 1000;
                    axes[1].Maximum = ((maxY / 1000) + Math.Sign(maxY)) * 1000;
                }

                mainWindow.viewModel.PlotModel.InvalidatePlot(true);
                counter = 0;
            }
        }
    }
}
