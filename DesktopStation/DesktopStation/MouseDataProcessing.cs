using System;
using System.Diagnostics;
using System.Runtime.InteropServices;



namespace DesktopStation
{
    public class MouseDataProcessing
    {
        public bool MouseEnabled { get; set; }

        public double HandVerticalSensivity { get; set; }
        public double HandHorizontalSensivity { get; set; }



        double[] data = new double[3];
        double[] preData = null;
        IDataSmoothing[] smooth = new IDataSmoothing[3];



        public MouseDataProcessing()
        {
            for (int i = 0; i < 3; ++i)
                smooth[i] = new MovingAverageDataSmoothing(40);

            HandVerticalSensivity = 0.8;
            HandHorizontalSensivity = 0.6;

            MouseEnabled = false;
        }



        public void StartProcessing() {
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

            preData = null;
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

                    if (MouseEnabled)
                        Win32.MouseMouse((int)(-HandHorizontalSensivity * (data[1] - preData[1])), (int)(HandVerticalSensivity * (data[0] - preData[0])));


                    if (preData == null)
                        preData = (double[])data.Clone();
                    else Array.Copy(data, preData, 3);
                                        
                    if (Globals.showGraph)
                        Globals.graphManager.Update(data);

                    if (Globals.showDataInDebug)
                        Debug.Print("{0:0.00} {1:0.00} {2:0.00} {3:0.00} {4:0.00} {5:0.00}"
                            , data[0], data[1], data[2], e.ArrayData[0], e.ArrayData[1], e.ArrayData[2]);
                        

                    break;
                case DataKey.COMMAND:
                    if (MouseEnabled)
                        Win32.PerformClick(e.IntData);

                    break;
            }
        }



        public static class Win32
        {
            public struct POINT
            {
                public int X;
                public int Y;
            }



            [DllImport("user32.dll", EntryPoint = "GetCursorPos", CharSet = CharSet.Unicode)]
            public static extern bool GetCursorPos(out POINT cursor);

            [DllImport("user32.dll", EntryPoint = "SetCursorPos", CharSet = CharSet.Unicode)]
            public static extern long SetCursorPos(int x, int y);



            const int MOUSEEVENTF_ABSOLUTE = 0x8000;
            const int MOUSEEVENTF_HWHEEL = 0x1000;
            const int MOUSEEVENTF_MOVE = 0x0001;
            const int MOUSEEVENTF_LEFTDOWN = 0x0002;
            const int MOUSEEVENTF_LEFTUP = 0x0004;
            const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
            const int MOUSEEVENTF_RIGHTUP = 0x0010;
            const int MOUSEEVENTF_WHEEL = 0x0800;

            const int INPUT_MOUSE = 0;



            [StructLayout(LayoutKind.Sequential)]
            struct MOUSEINPUT
            {
                public int dx;
                public int dy;
                public uint mouseData;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct KEYBDINPUT
            {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct HARDWAREINPUT
            {
                public uint uMsg;
                public ushort wParamL;
                public ushort wParamH;
            }

            [StructLayout(LayoutKind.Explicit)]
            struct INPUT
            {
                [FieldOffset(0)]
                public int type;
                [FieldOffset(4)]
                public MOUSEINPUT mi;
                [FieldOffset(4)]
                public KEYBDINPUT ki;
                [FieldOffset(4)]
                public HARDWAREINPUT hi;
            }



            [DllImport("user32.dll")]
            static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] INPUT[] inputs, int cbSize);



            public static void PerformClick(int code)
            {
                INPUT[] inputs = new INPUT[]
                {
                    new INPUT { type = INPUT_MOUSE },
                    new INPUT { type = INPUT_MOUSE }
                };

                switch (code)
                {
                    case ButtonCommand.UP_CLICK:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP;

                        break;
                    case ButtonCommand.UP_DOUBLE_CLICK:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP;
                        inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP;

                        break;
                    case ButtonCommand.UP_LONG_CLICK_START:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;

                        break;
                    case ButtonCommand.UP_LONG_CLICK_STOP:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;

                        break;
                    case ButtonCommand.DOWN_CLICK:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP;

                        break;
                    case ButtonCommand.DOWN_DOUBLE_CLICK:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP;
                        inputs[1].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP;

                        break;
                    case ButtonCommand.DOWN_LONG_CLICK_START:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;

                        break;
                    case ButtonCommand.DOWN_LONG_CLICK_STOP:
                        inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTUP;

                        break;
                }

                if (code != ButtonCommand.DOWN_DOUBLE_CLICK && code != ButtonCommand.UP_DOUBLE_CLICK)
                    SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
                else
                    SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
            }



            public static void MouseMouse(int x, int y, bool absolute = false)
            {
                INPUT[] inputs = new INPUT[]
                {
                    new INPUT {
                        type = INPUT_MOUSE,
                        mi = new MOUSEINPUT
                        {
                            dwFlags = MOUSEEVENTF_MOVE,
                            dx = x,
                            dy = y
                        }
                    },
                };

                if (absolute)
                    inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;

                SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
        }
    }
}
