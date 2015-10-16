using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;



namespace DesktopStation
{
    public class QuadDataProcessing
    {
        const int DEFAULT_STILL_X = -500;
        const int DEFAULT_STILL_Y = 377;
        const int DEFAULT_STILL_Z = -772;
        const int NUM_CALIBRATING_SAMPLE = 220;
        const int MAX_COUNTER_SPEED = 20;

        public readonly char FLYING_COMMAND_NONE = '\0';
        public readonly char[] FLY_COMMAND_SYMBOLS = { 'U', 'D', 'L', 'R', 'F', 'B', 'S' };



        public int[] StillData { get; set; }
        public float Speed { get; set; }

        public bool StartQuadcopter
        {
            get
            {
                return startQuadcopter;
            }
            set
            {               
                if (value == false)
                {
                    if (curCommandId != -1)
                        quadControlWindow.UpdateFlyingCommandButton(FLY_COMMAND_SYMBOLS[curCommandId], false);

                    curCommandId = -1;
                    startQuadcopter = false;

                    VREP.simxFinish(-1);

                    quadControlWindow.UpdateFlightStatus("");
                }
                else
                {
                    VREP.simxFinish(-1);
                    VREP.clientID = VREP.simxStart("127.0.0.1", 19999, true, true, 5000, 5);

                    if (VREP.clientID == -1)
                        throw new InvalidOperationException("Cannot connect to V-REP!");

                    if (VREP.simxGetObjectHandle(VREP.clientID, "Quadricopter_target", out VREP.handle, VREP.simx_opmode.oneshot_wait)
                        != VREP.simx_error.noerror)
                        throw new InvalidOperationException("Cannot get Object Handle!");

                    startQuadcopter = true;
                    timeCounter = 0;
                }
            }
        }




        bool startQuadcopter;

        double[] data = new double[3];
        public IDataSmoothing[] smooth = new IDataSmoothing[3];

        bool isCalibrating = false;
        List<double>[] calibrateData = new List<double>[3];

        delegate bool FlyCommand(double[] data);

        FlyCommand[] flyCommandCheckerFunctions = 
            { FlyCommandChecker.Up, FlyCommandChecker.Down,
                FlyCommandChecker.Left, FlyCommandChecker.Right,
                FlyCommandChecker.Forward, FlyCommandChecker.Backward,
                FlyCommandChecker.Still };

        int curCommandId, timeCounter;

        MainWindow mainWindow;
        QuadControlWindow quadControlWindow;



        public QuadDataProcessing(MainWindow mainWindow, QuadControlWindow quadControlWindow)
        {
            this.mainWindow = mainWindow;
            this.quadControlWindow = quadControlWindow;

            for (int i = 0; i < 3; ++i)
            {
                smooth[i] = new MovingAverageDataSmoothing(45);
                calibrateData[i] = new List<double>();
            }

            StillData = new int[] { DEFAULT_STILL_X, DEFAULT_STILL_Y, DEFAULT_STILL_Z };
            Speed = 0.03f;
            timeCounter = 0;

            startQuadcopter = false;
        }



        public void StartProcessing()
        {
            Globals.phoneListener.NewSensorDataArrived += NewDataArrived;
            Globals.phoneListener.NewFrequencyArrived += NewDataArrived;
            Globals.phoneListener.NewCommandArrived += NewDataArrived;

            Globals.graphManager.Reset();
        }



        public void StopProcessing()
        {
            Globals.phoneListener.NewSensorDataArrived -= NewDataArrived;
            Globals.phoneListener.NewFrequencyArrived -= NewDataArrived;
            Globals.phoneListener.NewCommandArrived -= NewDataArrived;

            for (int i = 0; i < 3; ++i)
                smooth[i].Reset();
        }



        public void StartCalibrating()
        {
            isCalibrating = true;

            for (int i = 0; i < 3; ++i)
                calibrateData[i].Clear();

            mainWindow.UpdateLog("New Quadcontrol Calibration");
        }



        public void StopCalibrating()
        {
            isCalibrating = false;

            mainWindow.UpdateLog("Stop Quadcontrol Calibration");
        }



        void Calibrate()
        {
            if (calibrateData[0].Count == NUM_CALIBRATING_SAMPLE)
            {
                timeCounter = 0;
                isCalibrating = false;

                for (int i = 0; i < 3; ++i)
                    StillData[i] = (int)Math.Round(calibrateData[i].Average());

                mainWindow.UpdateLog(String.Format(
                    "Quad Control Still Calibration: X={0} Y={1} Z={2}",
                    StillData[0], StillData[1], StillData[2]));
            }
            else
            {
                for (int i = 0; i < 3; ++i)
                    calibrateData[i].Add(data[i]);
            }
        }



        void NewDataArrived(Object sender, NewDataEventArgs e)
        {
            switch (e.DataType)
            {
                case DataKey.FREQUENCY:
                    Globals.graphManager.timeStep = 1000 / e.IntData;

                    if (Globals.showDataInDebug)
                        Debug.Print("Freq={0}", 1000 / e.IntData);

                    break;
                case DataKey.SENSOR:
                    for (int i = 0; i < 3; ++i)
                        data[i] = Math.Round(smooth[i].Process(e.ArrayData[i]));

                    if (isCalibrating)
                        Calibrate();

                    for (int i = 0; i < 3; ++i)
                        data[i] -= StillData[i];

                    if (startQuadcopter && !isCalibrating)
                        PerformFly();

                    if (Globals.showGraph)
                        Globals.graphManager.Update(data);

                    if (Globals.showDataInDebug)
                        Debug.Print("{0:0.00} {1:0.00} {2:0.00}", data[0], data[1], data[2]);

                    break;
                case DataKey.COMMAND:
                    switch (e.IntData)
                    {
                        case ButtonCommand.UP_CLICK:
                            quadControlWindow.PerformButtonClick(quadControlWindow.btnStartQuadcopter);

                            break;
                        case ButtonCommand.DOWN_CLICK:
                            quadControlWindow.PerformButtonClick(quadControlWindow.btnStillCalibration);

                            break;
                    }

                    break;
            }
        }



        void PerformFly()
        {
            int commandId = -1;

            for (int i = 0; i < 7; ++i)
                if (flyCommandCheckerFunctions[i](data))
                {
                    commandId = i;
                    break;
                }

            if (commandId == -1)
                commandId = 6;

            if (curCommandId != -1)
                quadControlWindow.UpdateFlyingCommandButton(FLY_COMMAND_SYMBOLS[curCommandId], false);

            curCommandId = commandId;
            quadControlWindow.UpdateFlyingCommandButton(FLY_COMMAND_SYMBOLS[commandId], true);

            ++timeCounter;
            if (timeCounter == MAX_COUNTER_SPEED)
            {
                timeCounter = 0;

                float[] v = { 0, 0, 0 };

                int res = (int)VREP.simxGetObjectPosition(VREP.clientID, VREP.handle, -1, v, VREP.simx_opmode.streaming);

                if (res <= 1)
                    quadControlWindow.UpdateFlightStatus(String.Format(
                        "X = {0:0.0000}\nY = {1:0.0000}\nZ = {2:0.0000}",
                        v[0], v[1], v[2]
                        ));
                else
                    quadControlWindow.UpdateFlightStatus("Error occured!");

                switch (curCommandId)
                {
                    case 0:
                        v[2] += Speed;
                        break;
                    case 1:
                        v[2] -= Speed;
                        break;
                    case 2:
                        v[0] -= Speed;
                        break;
                    case 3:
                        v[0] += Speed;
                        break;
                    case 4:
                        v[1] += Speed;
                        break;
                    case 5:
                        v[1] -= Speed;
                        break;
                }

                VREP.simxSetObjectPosition(VREP.clientID, VREP.handle, -1, v, VREP.simx_opmode.oneshot);
            }
        }



        static class FlyCommandChecker
        {
            public static bool Still(double[] data)
            {
                const int DEVIATION = 200;
                return Math.Abs(data[0]) <= DEVIATION && Math.Abs(data[1]) <= DEVIATION && Math.Abs(data[2]) <= DEVIATION;
            }



            public static bool Up(double[] data)
            {
                if (data[0] > 0 || data[1] > 0 || data[2] < 0) return false;
                return !Backward(data);
            }



            public static bool Down(double[] data)
            {
                if (Right(data)) return false;
                return data[0] >= 800 && data[1] <= -200 && data[2] > -20;
            }



            public static bool Left(double[] data)
            {
                return data[0] > 0 && data[1] >= 500 && data[2] >= 500;
            }



            public static bool Right(double[] data)
            {
                return data[0] >= 500 && data[1] <= -650;
            }



            public static bool Forward(double[] data)
            {
                if (data[0] < 0 || data[1] > 0 || data[2] > 0) return false;
                return !Right(data);
            }



            public static bool Backward(double[] data)
            {
                if (data[0] > 0 || data[1] > 0 || data[2] < 450) return false;
                return Math.Max(Math.Abs(data[0]), Math.Abs(data[1])) <= Math.Abs(data[2]);
            }
        }
    }



    public static class VREP
    {
        public static int clientID = -1;
        public static int handle = -1;



        public enum simx_error
        {
            noerror = 0x000000,
            novalue_flag = 0x000001,
            timeout_flag = 0x000002,
            illegal_opmode_flag = 0x000004,
            remote_error_flag = 0x000008,
            split_progress_flag = 0x000010,
            local_error_flag = 0x000020,
            initialize_error_flag = 0x000040,
        }

        public enum simx_opmode
        {
            oneshot = 0x000000,
            oneshot_wait = 0x010000,
            continuous = 0x020000,
            streaming = 0x020000,

            oneshot_split = 0x030000,
            continuous_split = 0x040000,
            streaming_split = 0x040000,

            discontinue = 0x050000,
            buffer = 0x060000,
            remove = 0x070000,
        }



        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void simxFinish(int clientID);

        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int simxStart(string ip, int port, bool waitForConnection, bool reconnectOnDisconnect, int timeoutMS, int cycleTimeMS);

        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern simx_error simxGetObjectHandle(int clientID, string objectName, out int handle, simx_opmode opMode);

        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern simx_error simxGetObjectPosition(int clientID, int objectHandle, int relativeToHandle, [Out] float[] positions, simx_opmode opMode);

        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int simxSetObjectPosition(int clientID, int objectHandle, int relativeToHandle, [In] float[] position, simx_opmode opMode);

        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int simxStartSimulation(int clientID, simx_opmode opMode);

        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int simxStopSimulation(int clientID, simx_opmode opMode);

        [DllImport("remoteApi.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int simxPauseSimulation(int clientID, simx_opmode opMode);
    }
}
