using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;



namespace DesktopStation
{
    /// <summary>
    /// Interaction logic for QuadControlWindow.xaml
    /// </summary>
    public partial class QuadControlWindow : Window
    {
        bool startRecord, isPreparing;
        QuadDataProcessing quadDataProcessing;



        public QuadControlWindow(MainWindow mainWindow)
        {
            isPreparing = true;

            InitializeComponent();


            quadDataProcessing = new QuadDataProcessing(mainWindow, this);
            sliderSpeed.Value = quadDataProcessing.Speed;

            btnStillCalibration.Click += BtnStillCalibration_Click;
            
            startRecord = false;

            isPreparing = false;
        }



        private void BtnStillCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (startRecord)
                quadDataProcessing.StartCalibrating();
        }



        private void btnStartRecording_Click(object sender, RoutedEventArgs e)
        {
            if (startRecord)
            {
                startRecord = false;
                quadDataProcessing.StopProcessing();

                btnStartRecording.Content = "Start Recording";
            }
            else
            {
                startRecord = true;
                quadDataProcessing.StartProcessing();

                btnStartRecording.Content = "Stop Recording";
            }
        }



        private void btnStartQuadcopter_Click(object sender, RoutedEventArgs e)
        {
            if (!startRecord) return;

            if (quadDataProcessing.StartQuadcopter)
            {
                quadDataProcessing.StartQuadcopter = false;
                lblBtnStartQuadcopter.Text = "Start Quadcopter\n(UP)";
            }                
            else
            {
                try
                {
                    quadDataProcessing.StartQuadcopter = true;
                    lblBtnStartQuadcopter.Text = "Stop Quadcopter\n(UP)";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error has occured: " + ex.Message, MainWindow.PROGRAM_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            
        }



        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            quadDataProcessing.StopCalibrating();
            quadDataProcessing.StopProcessing();
            quadDataProcessing.StartQuadcopter = false;
        }



        public void UpdateFlyingCommandButton(char command, bool effective)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                TextBlock button = (TextBlock)this.FindName("lbl" + command);

                button.Background = new SolidColorBrush(
                    effective ? Color.FromRgb(255, 200, 81) : Color.FromArgb(255, 255, 255, 255)
                    );
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }



        public void UpdateFlightStatus(string status)
        {
            lblFlightStatus.Dispatcher.BeginInvoke((Action)(() =>
            {
                lblFlightStatus.Text = status;
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }



        public void PerformButtonClick(Button button)
        {
            button.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (startRecord)
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }



        private void sliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isPreparing)
                quadDataProcessing.Speed = (float)Math.Round(sliderSpeed.Value, 3);
        }
    }
}
