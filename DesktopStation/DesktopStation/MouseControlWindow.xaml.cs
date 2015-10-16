using System;
using System.Windows;



namespace DesktopStation
{
    /// <summary>
    /// Interaction logic for MouseControlWindow.xaml
    /// </summary>
    public partial class MouseControlWindow : Window
    {
        bool isPreparing;
        bool startRecord;
        MouseDataProcessing mouseDataProcessing;



        public MouseControlWindow()
        {
            isPreparing = true;

            InitializeComponent();

            mouseDataProcessing = new MouseDataProcessing();

            sliderHandVerticalSensitivity.Value = mouseDataProcessing.HandVerticalSensivity;
            sliderHandHorizontalSensitivity.Value = mouseDataProcessing.HandHorizontalSensivity;

            isPreparing = false;

            startRecord = false;
        }



        private void btnStartMouse_Click(object sender, RoutedEventArgs e)
        {
            mouseDataProcessing.MouseEnabled = !mouseDataProcessing.MouseEnabled;

            if (mouseDataProcessing.MouseEnabled)
            {
                btnStartMouse.Content = "Stop Mouse";

                //Move mouse to middle of the screen
                MouseDataProcessing.Win32.MouseMouse(32767, 32767, true);
            }                
            else
            {
                btnStartMouse.Content = "Start Mouse";
            }
                
        }



        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            mouseDataProcessing.StopProcessing();
        }



        private void sliderX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isPreparing)
                mouseDataProcessing.HandVerticalSensivity = Math.Round(sliderHandVerticalSensitivity.Value, 3);
        }



        private void sliderY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isPreparing)
                mouseDataProcessing.HandHorizontalSensivity = Math.Round(sliderHandHorizontalSensitivity.Value, 3);
        }



        private void btnStartRecording_Click(object sender, RoutedEventArgs e)
        {
            if (startRecord)
            {
                startRecord = false;
                mouseDataProcessing.StopProcessing();

                btnStartRecording.Content = "Start Recording";
            }
            else
            {
                startRecord = true;
                mouseDataProcessing.StartProcessing();

                btnStartRecording.Content = "Stop Recording";
            }
        }
    }
}
