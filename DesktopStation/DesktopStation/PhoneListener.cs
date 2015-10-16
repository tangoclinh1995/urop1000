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
using System.Net;
using System.Net.Sockets;
using System.Threading;

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;



namespace DesktopStation
{
    public class PhoneListener
    {
        public enum ConnectionStatus
        {
            DISCONNECTED, CONNECTING, CONNECTED, DISCONNECTING
        }



        const int CONNECTION_TIMEOUT = 2000;
        const int MAX_NUM_SAMPLES_RECEIVED_PERTIME = 3;
        const string ACKNOWLEDGE_MESSAGE = "PAck";
        const string CLIENT_DISCONNECT_MESSAGE = "PDis";



        public event NewDataArrivedEventHandler NewSensorDataArrived;
        public event NewDataArrivedEventHandler NewFrequencyArrived;
        public event NewDataArrivedEventHandler NewCommandArrived;



        public int PortAndroid { get; set; }
        public int PortPC { get; set; }
        public string DeviceSerial { get; set; }

        ConnectionStatus connectionStatus;
        TcpClient socket;
        NetworkStream stream;

        MainWindow mainWindow;



        public ConnectionStatus Status
        {
            get
            {
                return connectionStatus;
            }
        }



        public PhoneListener(MainWindow mainWindow)
        {
            connectionStatus = ConnectionStatus.DISCONNECTED;

            this.mainWindow = mainWindow;
        }



        private bool OpenSocketConnection()
        {
            try
            {
                connectionStatus = ConnectionStatus.CONNECTING;
                mainWindow.UpdateLog("Connecting...");

                mainWindow.UpdateButtonText(mainWindow.btnConnect, "Connecting...");

                socket = new TcpClient("localhost", PortPC);                
                stream = socket.GetStream();

                stream.ReadTimeout = CONNECTION_TIMEOUT;
                stream.WriteTimeout = CONNECTION_TIMEOUT;

                byte[] buffer = new byte[4];

                //If receive the Acknowledgement message, the connection success
                stream.Read(buffer, 0, 4);
                if (Encoding.ASCII.GetString(buffer) != ACKNOWLEDGE_MESSAGE)
                    return false;

                //Also send Acknowledgement message to server
                buffer = Encoding.ASCII.GetBytes(ACKNOWLEDGE_MESSAGE);
                stream.Write(buffer, 0, buffer.Length);

                connectionStatus = ConnectionStatus.CONNECTED;
                mainWindow.UpdateLog("CONNECTED!");

                mainWindow.UpdateButtonText(mainWindow.btnConnect, "Stop");

                return true;
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                mainWindow.UpdateLog("Error occured while connecting: " + e.Message);

                return false;
            }
        }



        void StreamData()
        {
            int packetType, len;
            byte[] rawData = new byte[MAX_NUM_SAMPLES_RECEIVED_PERTIME * 14];
            int[] sensorData = new int[3];

            while (socket.Connected && connectionStatus == ConnectionStatus.CONNECTED)
            {
                if (!stream.DataAvailable)
                    continue;

                packetType = stream.ReadByte();

                switch (packetType)
                {
                    case DataKey.SENSOR:
                        len = stream.ReadByte();

                        //Note that the byte data is Little-Endian
                        //But luckily, Windows architecture is also Little-Endian, so we don't have to reverse the byte array
                        for (int i = 0; i < len; ++i)
                        {
                            stream.Read(rawData, 0, 6);

                            for (int j = 0; j < 3; ++j)
                                sensorData[j] = (int)BitConverter.ToInt16(rawData, j * 2);

                            OnNewSensorDataArrived(sensorData);
                        }

                        break;
                    case DataKey.FREQUENCY:
                        len = stream.ReadByte();

                        OnNewFrequencyArrived(len);                        

                        break;
                    case DataKey.COMMAND:
                        len = stream.ReadByte();

                        OnNewCommandArrived(len);                        

                        break;
                    case 4:     //Server close:
                        connectionStatus = ConnectionStatus.DISCONNECTED;
                        socket.Close();

                        mainWindow.UpdateLog("SERVER DISCONNECTED!");
                        mainWindow.UpdateButtonText(mainWindow.btnConnect, "Start");

                        break;
                }

            }
        }



        private void OnNewSensorDataArrived(int[] data)
        {
 	        NewDataArrivedEventHandler handler = NewSensorDataArrived;
            if (handler != null)
            {
                NewDataEventArgs e = new NewDataEventArgs();
                e.DataType = DataKey.SENSOR;
                e.ArrayData = (int[])data.Clone();

                handler(this, e);
            }
        }



        public void OnNewFrequencyArrived(int frequency)
        {
            NewDataArrivedEventHandler handler = NewFrequencyArrived;
            if (handler != null)
            {
                NewDataEventArgs e = new NewDataEventArgs();
                e.IntData = frequency;
                e.DataType = DataKey.FREQUENCY;

                handler(this, e);
            }
        }



        public void OnNewCommandArrived(int command)
        {
            NewDataArrivedEventHandler handler = NewCommandArrived;
            if (handler != null)
            {
                NewDataEventArgs e = new NewDataEventArgs();
                e.IntData = command;
                e.DataType = DataKey.COMMAND;

                handler(this, e);
            }
        }



        public void StopConnection()
        {
            if (socket == null || socket != null && !socket.Connected)
                return;
            
            connectionStatus = ConnectionStatus.DISCONNECTING;
            mainWindow.UpdateButtonText(mainWindow.btnConnect, "Disconnecting...");
            mainWindow.UpdateLog("Disconnecting...");

            //Send Client "disconnect" message to server before closing socket
            byte[] buffer = Encoding.ASCII.GetBytes(CLIENT_DISCONNECT_MESSAGE);
            stream.Write(buffer, 0, buffer.Length);

            socket.Close();

            connectionStatus = ConnectionStatus.DISCONNECTED;
            mainWindow.UpdateButtonText(mainWindow.btnConnect, "Start");
            mainWindow.UpdateLog("DISCONNECTED");
        }



        bool USBPortForward()
        {
            Process processAdb = new Process();
            string error;

            processAdb.StartInfo.FileName = "adb.exe";
            processAdb.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processAdb.StartInfo.RedirectStandardError = true;
            processAdb.StartInfo.UseShellExecute = false;

            mainWindow.UpdateLog("Turn on adb...");
                        
            processAdb.StartInfo.Arguments = "start-server";

            try
            {
                processAdb.Start();
                error = processAdb.StandardError.ReadToEnd().Trim();
                processAdb.WaitForExit();

                if (error != "")
                {
                    mainWindow.UpdateLog("Cannot turn on ADB!");
                    return false;
                }
            }
            catch
            {
                mainWindow.UpdateLog("adb not found in application's directory!");
                return false;
            }

            mainWindow.UpdateLog("adb ON");

            mainWindow.UpdateLog(String.Format("Forward tcp:{0} to tcp:{1} on device {2} ...", PortPC, PortAndroid, DeviceSerial));

            processAdb.StartInfo.Arguments = String.Format("-s {0} forward --remove-all", DeviceSerial);
            processAdb.Start();
            processAdb.WaitForExit();

            processAdb.StartInfo.Arguments = String.Format("-s {0} forward tcp:{1} tcp:{2}", DeviceSerial, PortPC, PortAndroid);
            processAdb.Start();
            error = processAdb.StandardError.ReadToEnd().Trim();
            processAdb.WaitForExit();

            if (error != "")
            {
                mainWindow.UpdateLog("Cannot forward!");
                return false;
            }

            mainWindow.UpdateLog("Forward COMPLETE");
            return true;
        }



        public void StartConnection()
        {
            if (!USBPortForward())
            {
                MessageBox.Show("ADB error! Please check the logs for more details", MainWindow.PROGRAM_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            if (!OpenSocketConnection())
            {
                connectionStatus = ConnectionStatus.DISCONNECTED;

                mainWindow.UpdateButtonText(mainWindow.btnConnect, "Start");
                MessageBox.Show("Connection Failed", MainWindow.PROGRAM_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            StreamData();
        }
    }



    public static class DataKey
    {
        public const int SENSOR = 1;
        public const int FREQUENCY = 2;
        public const int COMMAND = 3;
    }



    public static class ButtonCommand
    {
        public const int UP_CLICK = 1;
        public const int UP_DOUBLE_CLICK = 2;
        public const int UP_LONG_CLICK_START = 3;
        public const int UP_LONG_CLICK_STOP = 4;

        public const int DOWN_CLICK = 5;
        public const int DOWN_DOUBLE_CLICK = 6;
        public const int DOWN_LONG_CLICK_START = 7;
        public const int DOWN_LONG_CLICK_STOP = 8;
    }



    public class NewDataEventArgs : EventArgs
    {
        public int DataType { get; set; }
        public int[] ArrayData { get; set; }
        public int IntData { get; set; }
    }



    public delegate void NewDataArrivedEventHandler(Object sender, NewDataEventArgs e);
}
