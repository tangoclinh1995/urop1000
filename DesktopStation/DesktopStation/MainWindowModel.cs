using System;
using System.ComponentModel;
using System.Linq;
using System.Text;

using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;



namespace DesktopStation
{
    public class MainWindowModel : INotifyPropertyChanged
    {
        PlotModel plotModel;

        public PlotModel PlotModel
        {
            get { return plotModel; }
            set
            {
                plotModel = value;
                OnPropertyChanged("PlotModel");
            }
        }



        public MainWindowModel(LinearAxis[] axes, LineSeries[] series)
        {
            PlotModel = new PlotModel();

            PlotModel.LegendTitle = "Sensor Data";
            PlotModel.LegendOrientation = LegendOrientation.Horizontal;
            PlotModel.LegendPlacement = LegendPlacement.Outside;
            PlotModel.LegendPosition = LegendPosition.TopRight;
            PlotModel.LegendBackground = OxyColor.FromAColor(200, OxyColors.White);
            PlotModel.LegendBorder = OxyColors.Black;


            axes.ToList().ForEach(e => PlotModel.Axes.Add(e));
            series.ToList().ForEach(e => PlotModel.Series.Add(e));
        }

   
        
        public event PropertyChangedEventHandler PropertyChanged;


        
        protected virtual void OnPropertyChanged(string propertyName)
        {

            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
