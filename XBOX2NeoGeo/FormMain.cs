using FTD2XX_NET;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XBOX2NeoGeo.Properties;
using XInputDotNetPure;
using static System.Windows.Forms.AxHost;

namespace Zanac.XBOX2NeoGeo
{
    public partial class FormMain : Form
    {
        private GamePadState lastGamePadState;

        public const int FTDI_BAUDRATE = 115200 / 16;
        public const int FTDI_BAUDRATE_MUL = 100;

        private FTD2XX_NET.FTDI ftdi = new FTD2XX_NET.FTDI();

        // High resolution timers to replace designer System.Windows.Forms.Timer
        private HighResolutionTimer hrRapidTimer;

        public FormMain()
        {
            InitializeComponent();

            restoreCheckStatus(1, Settings.Default.DPadUp);
            restoreCheckStatus(2, Settings.Default.DPadLeft);
            restoreCheckStatus(3, Settings.Default.DPadRight);
            restoreCheckStatus(4, Settings.Default.DPadDown);
            restoreCheckStatus(5, Settings.Default.LStickUp);
            restoreCheckStatus(6, Settings.Default.LStickLeft);
            restoreCheckStatus(7, Settings.Default.LStickRight);
            restoreCheckStatus(8, Settings.Default.LStickDown);
            restoreCheckStatus(9, Settings.Default.RStickUp);
            restoreCheckStatus(10, Settings.Default.RStickLeft);
            restoreCheckStatus(11, Settings.Default.RStickRight);
            restoreCheckStatus(12, Settings.Default.RStickDown);
            restoreCheckStatus(13, Settings.Default.Y);
            restoreCheckStatus(14, Settings.Default.X);
            restoreCheckStatus(15, Settings.Default.B);
            restoreCheckStatus(16, Settings.Default.A);
            restoreCheckStatus(17, Settings.Default.LTrigger);
            restoreCheckStatus(18, Settings.Default.LBumper);
            restoreCheckStatus(19, Settings.Default.RTrigger);
            restoreCheckStatus(20, Settings.Default.RBumper);
            restoreCheckStatus(21, Settings.Default.Back);
            restoreCheckStatus(22, Settings.Default.Start);

            // Rapid fire timer (uses numericUpDownFireRate in ms)
            UpdateRapidTimer();
        }

        private void UpdateRapidTimer()
        {
            // recreate rapid timer with new interval
            hrRapidTimer?.Stop();
            long intervalNs = (long)(1_000_000_000m / numericUpDownFireRate.Value);
            hrRapidTimer = new HighResolutionTimer(() =>
            {
                try
                {
                    if (!IsDisposed && !Disposing)
                        BeginInvoke(new Action(() => timerRapid_Tick(this, EventArgs.Empty)));
                }
                catch { }
            }, intervalNs, useTimeBeginPeriod: true);
            hrRapidTimer.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            Settings.Default.DPadUp = storeCheckStatus(1);
            Settings.Default.DPadLeft = storeCheckStatus(2);
            Settings.Default.DPadRight = storeCheckStatus(3);
            Settings.Default.DPadDown = storeCheckStatus(4);
            Settings.Default.LStickUp = storeCheckStatus(5);
            Settings.Default.LStickLeft = storeCheckStatus(6);
            Settings.Default.LStickRight = storeCheckStatus(7);
            Settings.Default.LStickDown = storeCheckStatus(8);
            Settings.Default.RStickUp = storeCheckStatus(9);
            Settings.Default.RStickLeft = storeCheckStatus(10);
            Settings.Default.RStickRight = storeCheckStatus(11);
            Settings.Default.RStickDown = storeCheckStatus(12);
            Settings.Default.Y = storeCheckStatus(13);
            Settings.Default.X = storeCheckStatus(14);
            Settings.Default.B = storeCheckStatus(15);
            Settings.Default.A = storeCheckStatus(16);
            Settings.Default.LTrigger = storeCheckStatus(17);
            Settings.Default.LBumper = storeCheckStatus(18);
            Settings.Default.RTrigger = storeCheckStatus(19);
            Settings.Default.RBumper = storeCheckStatus(20);
            Settings.Default.Back = storeCheckStatus(21);
            Settings.Default.Start = storeCheckStatus(22);

            // stop and dispose high resolution timers
            try { hrRapidTimer?.Stop(); hrRapidTimer?.Dispose(); hrRapidTimer = null; } catch { }
        }

        private void restoreCheckStatus(int rowNo, int val)
        {
            for (int i = 0; i < 12; i++)
            {
                int stat = val & 0x3;
                CheckBox cb = (CheckBox)tableLayoutPanelCheck.GetControlFromPosition(1 + i, rowNo);
                switch (stat)
                {
                    case 0:
                        cb.CheckState = CheckState.Unchecked;
                        break;
                    case 1:
                        cb.CheckState = CheckState.Indeterminate;
                        break;
                    default:
                        cb.CheckState = CheckState.Checked;
                        break;
                }
                val = val >> 2;
            }
        }

        private int storeCheckStatus(int rowNo)
        {
            int stat = 0;
            for (int i = 11; i >= 0; i--)
            {
                stat = stat << 2;
                CheckBox cb = (CheckBox)tableLayoutPanelCheck.GetControlFromPosition(1 + i, rowNo);
                switch (cb.CheckState)
                {
                    case CheckState.Unchecked:
                        stat |= 0;
                        break;
                    case CheckState.Indeterminate:
                        stat |= 1;
                        break;
                    case CheckState.Checked:
                        stat |= 2;
                        break;
                }
            }
            return stat;
        }

        private uint processButton(int rowNo)
        {
            uint stat = 0;
            for (int i = 11; i >= 0; i--)
            {
                stat = stat << 1;
                CheckBox cb = (CheckBox)tableLayoutPanelCheck.GetControlFromPosition(1 + i, rowNo);
                switch (cb.CheckState)
                {
                    case CheckState.Unchecked:
                        stat |= 0;
                        break;
                    case CheckState.Indeterminate:
                        stat |= (uint)rapidFire;
                        break;
                    case CheckState.Checked:
                        stat |= 1;
                        break;
                }
            }
            return stat;
        }

        private uint processButton(int rowIdx, int columnIdx)
        {
            uint stat = 0;
            {
                CheckBox cb = (CheckBox)tableLayoutPanelCheck.GetControlFromPosition(1 + columnIdx, 1 + rowIdx);
                switch (cb.CheckState)
                {
                    case CheckState.Unchecked:
                        stat |= 0;
                        break;
                    case CheckState.Indeterminate:
                        stat |= (uint)rapidFire;
                        break;
                    case CheckState.Checked:
                        stat |= 1;
                        break;
                }
            }
            stat = stat << columnIdx;
            return stat;
        }

        //int cnt = 0;

        private int rapidFire;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerRapid_Tick(object sender, EventArgs e)
        {
            rapidFire = rapidFire ^ 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="val"></param>
        /// <param name="row"></param>
        private uint processPseudoAnalog(float val, int row)
        {
            var cb = (CheckBox)tableLayoutPanelCheck.GetControlFromPosition(12, 5);
            if (cb.Tag == null)
                cb.Tag = 0f;

            float lval = (float)cb.Tag;

            lval += Math.Abs(val);

            uint bstat = 0;

            if (lval > 1.0f)
            {
                bstat = processButton(row);
                lval -= 1.0f;
            }

            cb.Tag = lval;

            return bstat;
        }

        private void timerController_Tick(object sender, EventArgs e)
        {
            GamePadState stat;
            if (Settings.Default.DInput)
                stat = GamePad.GetStateX();
            else
                stat = GamePad.GetState((PlayerIndex)((int)numericUpDownCtrlId.Value));

            //if (stat.PacketNumber != lastGamePadState.PacketNumber)
            //{
            //cnt = 0;

            //processGuideButton(stat);
            uint bstat = 0;

            var bs = stat.ButtonState;
            //var lbs = lastGamePadState.ButtonState;

            if ((bs & ButtonStates.DPadUp) == ButtonStates.DPadUp)
                bstat |= processButton(1);

            if ((bs & ButtonStates.DPadLeft) == ButtonStates.DPadLeft)
                bstat |= processButton(2);
            if ((bs & ButtonStates.DPadRight) == ButtonStates.DPadRight)
                bstat |= processButton(3);
            if ((bs & ButtonStates.DPadDown) == ButtonStates.DPadDown)
                bstat |= processButton(4);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 5)).Checked && stat.ThumbSticks.Left.Y > 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Left.Y, 5);
            }
            else if (stat.ThumbSticks.Left.Y > 0.5)
                bstat |= processButton(5);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 6)).Checked && stat.ThumbSticks.Left.X < 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Left.X, 6);
            }
            else if (stat.ThumbSticks.Left.X < -0.5)
                bstat |= processButton(6);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 7)).Checked && stat.ThumbSticks.Left.X > 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Left.X, 7);
            }
            else if (stat.ThumbSticks.Left.X > 0.5)
                bstat |= processButton(7);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 8)).Checked && stat.ThumbSticks.Left.Y < 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Left.Y, 8);
            }
            else if (stat.ThumbSticks.Left.Y < -0.5)
                bstat |= processButton(8);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 9)).Checked && stat.ThumbSticks.Right.Y > 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Right.Y, 9);
            }
            else if (stat.ThumbSticks.Right.Y > 0.5)
                bstat |= processButton(9);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 10)).Checked && stat.ThumbSticks.Right.X < 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Right.X, 10);
            }
            else if (stat.ThumbSticks.Right.X < -0.5)
                bstat |= processButton(10);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 11)).Checked && stat.ThumbSticks.Right.X > 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Right.X, 11);
            }
            else if (stat.ThumbSticks.Right.X > 0.5)
                bstat |= processButton(11);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 12)).Checked && stat.ThumbSticks.Right.Y < 0)
            {
                bstat |= processPseudoAnalog(stat.ThumbSticks.Right.Y, 12);
            }
            else if (stat.ThumbSticks.Right.Y < -0.5)
                bstat |= processButton(12);
            if ((bs & ButtonStates.Y) == ButtonStates.Y)
                bstat |= processButton(13);
            if ((bs & ButtonStates.X) == ButtonStates.X)
                bstat |= processButton(14);
            if ((bs & ButtonStates.B) == ButtonStates.B)
                bstat |= processButton(15);
            if ((bs & ButtonStates.A) == ButtonStates.A)
                bstat |= processButton(16);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 17)).Checked)
            {
                bstat |= processPseudoAnalog(stat.Triggers.Left, 17);
            }
            else if (stat.Triggers.Left > 0.5)
                bstat |= processButton(17);
            if ((bs & ButtonStates.LeftShoulder) == ButtonStates.LeftShoulder)
                bstat |= processButton(18);
            if (((CheckBox)tableLayoutPanelCheck.GetControlFromPosition(13, 19)).Checked)
            {
                bstat |= processPseudoAnalog(stat.Triggers.Right, 19);
            }
            else if (stat.Triggers.Right > 0.5)
                bstat |= processButton(19);
            if ((bs & ButtonStates.RightShoulder) == ButtonStates.RightShoulder)
                bstat |= processButton(20);
            if ((bs & ButtonStates.Back) == ButtonStates.Back)
                bstat |= processButton(21);
            if ((bs & ButtonStates.Start) == ButtonStates.Start)
                bstat |= processButton(22);

            bstat = ~bstat;

            uint bytesWritten = 0;

            // 0x80 MPSSEコマンド: Write Low Byte
            // 0xXX Bit data for AD pis
            // 0xFF Output
            byte[] ad_data = { 0x80, (byte)bstat, 0xFF };
            ftdi.Write(ad_data, ad_data.Length, ref bytesWritten);

            // 0x82 MPSSEコマンド: Write High Byte
            // 0xXX Bit data for AC pis
            // 0xFF Output
            byte[] ac_data = { 0x82, (byte)(bstat >> 8), 0xFF };
            ftdi.Write(ac_data, ac_data.Length, ref bytesWritten);

            lastGamePadState = stat;
        }

        private string serialNumber;

        private void checkBoxConn_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxConn.Checked)
            {
                var stat = ftdi.OpenByIndex((uint)numericUpDownPort.Value);
                if (stat == FTDI.FT_STATUS.FT_OK)
                {
                    ftdi.GetSerialNumber(out serialNumber);
                    toolStripStatusLabel1.Text = "Connected to: " + serialNumber;
                    ftdi.SetBaudRate(FTDI_BAUDRATE * FTDI_BAUDRATE_MUL);
                    ftdi.SetTimeouts(500, 500);
                    ftdi.SetLatency(0);
                    ftdi.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE);

                    uint bytesWritten = 0;

                    // 0x80 MPSSEコマンド: Write Low Byte
                    // 0xXX Bit data for AD pis
                    // 0xFF Output
                    byte[] ad_data = { 0x80, (byte)0xFF, 0xFF };
                    ftdi.Write(ad_data, ad_data.Length, ref bytesWritten);

                    // 0x82 MPSSEコマンド: Write High Byte
                    // 0xXX Bit data for AC pis
                    // 0xFF Output
                    byte[] ac_data = { 0x82, (byte)0x0F, 0xFF };
                    ftdi.Write(ac_data, ac_data.Length, ref bytesWritten);
                }
                else
                {
                    checkBoxConn.Checked = false;
                }
            }
            else
            {
                ftdi.Close();
                toolStripStatusLabel1.Text = "";
                serialNumber = null;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            // Update high resolution rapid timer interval
            try
            {
                UpdateRapidTimer();
            }
            catch
            {
                // no fallback to designer timer; hrRapidTimer expected
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            RegisterHidNotification();
            base.OnHandleCreated(e);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Win32.WM_DEVICECHANGE: OnDeviceChange(ref m); break;
            }
            base.WndProc(ref m);
        }

        void OnDeviceChange(ref Message msg)
        {
            int wParam = (int)msg.WParam;
            if (wParam == Win32.DBT_DEVICEARRIVAL)
            {
                if (!ftdi.IsOpen && serialNumber != null)
                {
                    var stat = ftdi.OpenBySerialNumber(serialNumber);
                    if (stat == FTDI.FT_STATUS.FT_OK)
                    {
                        ftdi.GetSerialNumber(out serialNumber);
                        toolStripStatusLabel1.Text = "Connected to: " + serialNumber;
                        ftdi.SetBaudRate(FTDI_BAUDRATE * FTDI_BAUDRATE_MUL);
                        ftdi.SetTimeouts(500, 500);
                        ftdi.SetLatency(0);
                        ftdi.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE);

                        uint bytesWritten = 0;

                        // 0x80 MPSSEコマンド: Write Low Byte
                        // 0xXX Bit data for AD pis
                        // 0xFF Output
                        byte[] ad_data = { 0x80, (byte)0xFF, 0xFF };
                        ftdi.Write(ad_data, ad_data.Length, ref bytesWritten);

                        // 0x82 MPSSEコマンド: Write High Byte
                        // 0xXX Bit data for AC pis
                        // 0xFF Output
                        byte[] ac_data = { 0x82, (byte)0x0F, 0xFF };
                        ftdi.Write(ac_data, ac_data.Length, ref bytesWritten);
                    }
                }
            }
            else if (wParam == Win32.DBT_DEVICEREMOVECOMPLETE)
            {
                UInt32 ftdiDeviceCount = 0;
                FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
                ftStatus = ftdi.GetNumberOfDevices(ref ftdiDeviceCount);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    return;

                FTDI.FT_DEVICE_INFO_NODE[] devList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];
                ftdi.GetDeviceList(devList);
                foreach (var dev in devList)
                {
                    if (dev.SerialNumber == serialNumber)
                        return;
                }
                ftdi.Close();
                toolStripStatusLabel1.Text = "Disconnecting...: " + serialNumber;
            }
        }

        void RegisterHidNotification()
        {
            Win32.DEV_BROADCAST_DEVICEINTERFACE dbi = new
            Win32.DEV_BROADCAST_DEVICEINTERFACE();
            int size = Marshal.SizeOf(dbi);
            dbi.dbcc_size = size;
            dbi.dbcc_devicetype = Win32.DBT_DEVTYP_DEVICEINTERFACE;
            dbi.dbcc_reserved = 0;
            dbi.dbcc_classguid = Win32.GUID_DEVINTERFACE_HID;
            dbi.dbcc_name = 0;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(dbi, buffer, true);
            IntPtr r = Win32.RegisterDeviceNotification(Handle, buffer,
            Win32.DEVICE_NOTIFY_WINDOW_HANDLE);
            if (r == IntPtr.Zero)
            {
                //label1.Text = Win32.GetLastError().ToString();
            }
        }
    }

    class Win32
    {
        public const int
        WM_DEVICECHANGE = 0x0219;
        public const int
        DBT_DEVICEARRIVAL = 0x8000,
        DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const int
        DEVICE_NOTIFY_WINDOW_HANDLE = 0,
        DEVICE_NOTIFY_SERVICE_HANDLE = 1;
        public const int
        DBT_DEVTYP_DEVICEINTERFACE = 5;
        public static Guid
        GUID_DEVINTERFACE_HID = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        //Thi code will show you how to detect Human Input Devices (HID) like USB devices, mouse, keyboard, keypad, joystick, etc... If you want to detect all USB devices, uses GUID_DEVINTERFACE_USB_DEVICE = "A5DCBF10-6530-11D2-901F-00C04FB951ED" instead.

        [StructLayout(LayoutKind.Sequential)]
        public class DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            public short dbcc_name;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterDeviceNotification(
        IntPtr hRecipient,
        IntPtr NotificationFilter,
        Int32 Flags);

        [DllImport("kernel32.dll")]
        public static extern int GetLastError();

        public const int DIGCF_PRESENT = 2;

        public static Guid GUID_DEVCLASS_MOUSE = new
        Guid("4D36E96F-E325-11CE-BFC1-08002BE10318");

        [DllImport("setupapi.dll")]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid,
        IntPtr Enumerator, IntPtr hWndParent, int Flags);

        [DllImport("setupapi.dll")]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet,
        int Supplies, ref SP_DEVINFO_DATA DeviceInfoData);

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public int DevInst;
            public int Reserved;
        }
    }

}
