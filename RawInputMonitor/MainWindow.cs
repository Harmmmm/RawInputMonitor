using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;

namespace RawInputMonitor
{
    public partial class MainWindow : Form
    {
        private int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
        private int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
        private const int WM_INPUT = 0x00FF;
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVNODES_CHANGED = 0x0007;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Log(string s)
        {
            txtLog.AppendText(s + "\r\n");
        }

        private string GetFancyDeviceName(RawInputDevice device)
        {
            string fancyName = "";

            if (device == null)
                return "Unknown device";

            if (device.DevicePath != null)
            {
                // Aimtrak
                if (device.VendorId == 0xD209 && device.ProductId >= 0x1601 && device.ProductId <= 0x1608)
                {
                    fancyName = String.Format("Ultimarc AimTrak #{0}", device.ProductId - 0x1600);
                }
                // Sinden
                else if (device.VendorId == 0x16C0)
                {
                    if (device.ProductId == 0x0F01)
                        fancyName = "Sinden Lightgun Blue";
                    else if (device.ProductId == 0x0F02)
                        fancyName = "Sinden Lightgun Red";
                    else if (device.ProductId == 0x0F38)
                        fancyName = "Sinden Lightgun Black";
                    else if (device.ProductId == 0x0F39)
                        fancyName = "Sinden Lightgun Player 2";
                }
                // DolphinBar
                else if (device.VendorId == 0x0079 && device.ProductId == 0x1802)
                {
                    fancyName = "Mayflash DolphinBar";
                }
            }

            // Other
            if (fancyName == "")
            {
                string manufacturer = device.ManufacturerName.Trim();

                if (manufacturer == "(Standard keyboards)" || device.ProductName.Contains(manufacturer))
                    manufacturer = "";

                fancyName = String.Format("{0} {1}", manufacturer, device.ProductName.Trim()).Trim();
            }

            return fancyName;
        }

        private void DeviceListAdd(RawInputDevice device)
        {
            string vid = "0000";
            string pid = "0000";

            if (device.DevicePath != null)
            {
                vid = device.VendorId.ToString("X4");
                pid = device.ProductId.ToString("X4");
            }

            string[] row = { RawInputDeviceHandle.GetRawValue(device.Handle).ToString("X8"), device.DeviceType.ToString(), vid, pid, device.ProductName, device.ManufacturerName, "?", "?", "?", (screenWidth / 2).ToString(), (screenHeight / 2).ToString(), GetFancyDeviceName(device), device.DevicePath };
            table.Rows.Add(row);
        }

        private void DeviceListUpdate()
        {
            var devices = RawInputDevice.GetDevices();
            var keyboards = devices.OfType<RawInputKeyboard>();
            var mice = devices.OfType<RawInputMouse>();

            table.Rows.Clear();

            foreach (var device in mice)
                DeviceListAdd(device);

            foreach (var device in keyboards)
                DeviceListAdd(device);

            table.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        }

        protected override void WndProc(ref Message m)
        {
            // Raw input event
            if (m.Msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(m.LParam);

                if (data == null)
                {
                    Log("RawInputData NULL!");
                    base.WndProc(ref m);
                    return;
                }

                // Device type
                switch (data)
                {
                    case RawInputMouseData mouse:
                        string handleM = "00000000";

                        if (mouse.Device != null)
                            handleM = RawInputDeviceHandle.GetRawValue(mouse.Device.Handle).ToString("X8");

                        var nameM = GetFancyDeviceName(mouse.Device);
                        var mode = mouse.Mouse.Flags;
                        var x = mouse.Mouse.LastX;
                        var y = mouse.Mouse.LastY;

                        for (int i = 0; i < table.Rows.Count; i++)
                        {
                            if ((string)(table.Rows[i].Cells[0].Value) == handleM)
                            {
                                bool resize = false;

                                if (table.Rows[i].Cells[6].Value.ToString() != mode.ToString())
                                    resize = true;

                                table.Rows[i].Cells[6].Value = mode;
                                table.Rows[i].Cells[7].Value = x;
                                table.Rows[i].Cells[8].Value = y;

                                if (mode.HasFlag(RawMouseFlags.MoveRelative))
                                {
                                    // Super advanced position determination
                                    table.Rows[i].Cells[9].Value = Math.Min(Math.Max(Int32.Parse(table.Rows[i].Cells[9].Value.ToString()) + x, 0), screenWidth);
                                    table.Rows[i].Cells[10].Value = Math.Min(Math.Max(Int32.Parse(table.Rows[i].Cells[10].Value.ToString()) + y, 0), screenHeight);
                                }
                                else if (mode.HasFlag(RawMouseFlags.MoveAbsolute))
                                {
                                    table.Rows[i].Cells[9].Value = Math.Round((float)x / (float)0xFFFF * (float)screenWidth).ToString();
                                    table.Rows[i].Cells[10].Value = Math.Round((float)y / (float)0xFFFF * (float)screenHeight).ToString();

                                }

                                if (resize)
                                    table.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

                                break;
                            }
                        }

                        // Log event?
                        if (chkEnabled.Checked)
                        {
                            if (mouse.Mouse.Buttons != RawMouseButtonFlags.None || (mouse.Mouse.Buttons == RawMouseButtonFlags.None && chkPosition.Checked))
                            {
                                Log($"Handle: {handleM:X8}\tName: {nameM}\tX: {mouse.Mouse.LastX}\tY: {mouse.Mouse.LastY}\tFlags: {mouse.Mouse.Flags}\tButtons: {mouse.Mouse.Buttons}\tData: {mouse.Mouse.ButtonData}");
                            }
                        }
                        
                        break;
                    case RawInputKeyboardData keyboard:
                        string handleK = "00000000";

                        if (keyboard.Device != null)
                            handleK = RawInputDeviceHandle.GetRawValue(keyboard.Device.Handle).ToString("X8");

                        var nameK = GetFancyDeviceName(keyboard.Device);

                        // Log event?
                        if (chkEnabled.Checked)
                            Log($"Handle: {handleK:X8}\tName: {nameK}\tKeyCode: {keyboard.Keyboard.VirutalKey}\tScanCode: {keyboard.Keyboard.ScanCode}\tKey: {(Keys)keyboard.Keyboard.VirutalKey}\tFlags: {keyboard.Keyboard.Flags}");

                        break;
                    case RawInputHidData hid:
                        Log(hid.Hid.ToString());
                        break;
                }
            }
            // Device added or removed event
            else if (m.Msg == WM_DEVICECHANGE)
            {
                if ((int)m.WParam == DBT_DEVNODES_CHANGED)
                    DeviceListUpdate();
            }
            
            base.WndProc(ref m);
        }

        private void MainWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            // Update title
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = $"Raw Input Monitor V{v.Major}.{v.Minor}";

            // Get devices
            DeviceListUpdate();

            // Get our handle
            int pid = Process.GetCurrentProcess().Id;
            Process proc = Process.GetProcessById(pid);
            var wHandle = proc.MainWindowHandle;

            // Register to raw input events
            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, wHandle);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, wHandle);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/Harmmmm/RawInputMonitor");
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }
    }
}
