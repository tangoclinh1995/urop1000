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



namespace DesktopStation
{  
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string PROGRAM_TITLE = "Gesture Controller Desktop Station";

        const string DEFAULT_CONFIGURATION_FILE = "config.txt";
        const int DEFAULT_ANDROID_PORT = 12476;
        const int DEFAULT_PC_PORT = 24533;
        const string DEFAULT_SERIAL = "ZH8005AMCM";

        public MainWindowModel viewModel;
        public MouseControlWindow mouseControlWindow;
        public QuadControlWindow quadControlWindow;



        public MainWindow()
        {
            Globals.graphManager = new GraphManager(this);
            viewModel = new MainWindowModel(Globals.graphManager.axes, Globals.graphManager.series);
            DataContext = viewModel;
            
            InitializeComponent();

            this.Title = PROGRAM_TITLE;

            txtMinY.Text = GraphManager.DEFAULT_MIN_Y.ToString();
            txtMaxY.Text = GraphManager.DEFAULT_MAX_Y.ToString();
            checkDynamicYScaling.IsChecked = false;

            txtRangeX.Text = GraphManager.DEFAULT_RANGE_X.ToString();
            
            btnConnect.Click += btnConnect_Click;
            btnClearLog.Click += btnClearLog_Click;
            btnDefaultConfig.Click += btnDefaultConfig_Click;
            btnSetRangeY.Click += btnSetRangeY_Click;
            btnSetRangeX.Click += btnSetRangeX_Click;

            this.Closed += MainWindow_Closed;

            checkShowDataInDebug.Checked += checkShowDataInLog_Checked;
            checkShowDataInDebug.Unchecked += checkShowDataInLog_Checked;

            checkShowGraph.Checked += checkShowGraph_Checked;
            checkShowGraph.Unchecked += checkShowGraph_Checked;
            
            checkDynamicYScaling.Checked += checkDynamicYScaling_Checked;
            checkDynamicYScaling.Unchecked += checkDynamicYScaling_Checked;

            checkX.Tag = 0;
            checkY.Tag = 1;
            checkZ.Tag = 2;
            checkX.Checked += checkSensorData_Checked;
            checkX.Unchecked += checkSensorData_Checked;
            checkY.Checked += checkSensorData_Checked;
            checkY.Unchecked += checkSensorData_Checked;
            checkZ.Checked += checkSensorData_Checked;
            checkZ.Unchecked += checkSensorData_Checked;

            Globals.phoneListener = new PhoneListener(this);

            mouseControlWindow = null;
            quadControlWindow = null;

            LoadConfiguration();
        }



        void checkShowGraph_Checked(object sender, RoutedEventArgs e)
        {
            Globals.showGraph = (bool)checkShowGraph.IsChecked;
        }



        void checkShowDataInLog_Checked(object sender, RoutedEventArgs e)
        {
            Globals.showDataInDebug = (bool)checkShowDataInDebug.IsChecked;
        }



        void btnSetRangeX_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Globals.graphManager.rangeX = int.Parse(txtRangeX.Text);
            }
            catch
            {
                UpdateLog("Invalid Range X!");
            }
        }



        void btnSetRangeY_Click(object sender, RoutedEventArgs e)
        {
            SetRangeForY();
        }



        void checkDynamicYScaling_Checked(object sender, RoutedEventArgs e)
        {
            Globals.graphManager.dynamicYScaling = (bool)checkDynamicYScaling.IsChecked;

            txtMinY.IsEnabled = !Globals.graphManager.dynamicYScaling;
            txtMaxY.IsEnabled = !Globals.graphManager.dynamicYScaling;
            btnSetRangeY.IsEnabled = !Globals.graphManager.dynamicYScaling;

            if (!Globals.graphManager.dynamicYScaling)
                SetRangeForY();
        }



        private void checkSensorData_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chk = (CheckBox)sender;
            Globals.graphManager.series[(int)chk.Tag].IsVisible = (bool)chk.IsChecked;
            viewModel.PlotModel.InvalidatePlot(true);
        }



        void btnDefaultConfig_Click(object sender, RoutedEventArgs e)
        {
            txtPortAndroid.Text = DEFAULT_ANDROID_PORT.ToString();
            txtPortPC.Text = DEFAULT_PC_PORT.ToString();
            txtSerial.Text = DEFAULT_SERIAL;
        }



        void MainWindow_Closed(object sender, EventArgs e)
        {
            if (mouseControlWindow != null)
                mouseControlWindow.Close();

            if (quadControlWindow != null)
                quadControlWindow.Close();

            Globals.phoneListener.StopConnection();
            SaveConfiguration();
        }



        void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Text = "";
        }
        


        void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            switch (Globals.phoneListener.Status)
            {
                case PhoneListener.ConnectionStatus.DISCONNECTED:
                    try
                    {
                        Globals.phoneListener.PortAndroid = int.Parse(txtPortAndroid.Text);
                        Globals.phoneListener.PortPC = int.Parse(txtPortPC.Text);

                        txtSerial.Text = txtSerial.Text.Trim().ToUpper();
                        if (txtSerial.Text == "")
                            throw new Exception("Serial is empty");

                        Globals.phoneListener.DeviceSerial = txtSerial.Text;

                        Thread networkThread = new Thread(new ThreadStart(Globals.phoneListener.StartConnection));
                        networkThread.Start();
                    }
                    catch
                    {
                        MessageBox.Show("Invalid Configuration!", PROGRAM_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    break;
                case PhoneListener.ConnectionStatus.CONNECTED:
                    Globals.phoneListener.StopConnection();

                    break;
            } 
        }



        void LoadConfiguration()
        {
            try
            {
                UpdateLog("Load Configuration...");                
                
                StreamReader reader = new StreamReader(DEFAULT_CONFIGURATION_FILE, Encoding.ASCII);

                txtPortAndroid.Text = reader.ReadLine().Trim();
                txtPortPC.Text = reader.ReadLine().Trim();
                txtSerial.Text = reader.ReadLine().Trim().ToUpper();

                reader.Close();

                UpdateLog("DONE!");
            }
            catch
            {
                txtPortAndroid.Text = "";
                txtPortPC.Text = "";
                txtSerial.Text = "";

                UpdateLog("Configuration not exist or invalid");
            }
        }



        void SaveConfiguration()
        {
            UpdateLog("Saving Configuration...");
            
            try
            {
                StreamWriter writer = new StreamWriter(DEFAULT_CONFIGURATION_FILE, false, Encoding.ASCII);

                writer.WriteLine(Globals.phoneListener.PortAndroid);
                writer.WriteLine(Globals.phoneListener.PortPC);
                writer.WriteLine(Globals.phoneListener.DeviceSerial);

                writer.Close();

                UpdateLog("DONE");
            }
            catch
            {
                UpdateLog("Cannot save Configuration!");
            }
        }



        public void UpdateLog(string text)
        {
            txtLog.Dispatcher.BeginInvoke
                (
                    (Action)(() =>
                    {
                        txtLog.Text += ("\n" + text);
                        txtLog.ScrollToEnd();
                    })
                    , System.Windows.Threading.DispatcherPriority.Normal
                );
        }



        public void UpdateButtonText(Button button, string text)
        {
            button.Dispatcher.BeginInvoke
                (
                    (Action)(() =>
                    {
                        button.Content = text;
                    }),
                    System.Windows.Threading.DispatcherPriority.Normal
                );

        }



        private void SetRangeForY()
        {
            try
            {
                int minY = int.Parse(txtMinY.Text);
                int maxY = int.Parse(txtMaxY.Text);

                Globals.graphManager.minY = minY;
                Globals.graphManager.axes[1].Minimum = minY;

                Globals.graphManager.maxY = maxY;
                Globals.graphManager.axes[1].Maximum = maxY;

                viewModel.PlotModel.InvalidatePlot(true);
            }
            catch
            {
                UpdateLog("Input of range Y is invalid!");
            }
        }



        private void btnMouseControl_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.Windows.OfType<QuadControlWindow>().Count() != 0)
                return;

            if (Application.Current.Windows.OfType<MouseControlWindow>().Count() == 0)
                mouseControlWindow = new MouseControlWindow();
           
            mouseControlWindow.Show();
            mouseControlWindow.Activate();
        }



        private void btnQuadControl_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.Windows.OfType<MouseControlWindow>().Count() != 0)
                return;

            if (Application.Current.Windows.OfType<QuadControlWindow>().Count() == 0)
                quadControlWindow = new QuadControlWindow(this);

            quadControlWindow.Show();
            quadControlWindow.Activate();
        }
    }
}