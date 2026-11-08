using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LovingCoolMini
{
    public sealed class HidStatusEventArgs : EventArgs
    {
        public bool Connected { get; private set; }
        public string Message { get; private set; }
        public string Serial { get; private set; }
        public string Product { get; private set; }

        public HidStatusEventArgs(bool connected, string message, string serial, string product)
        {
            Connected = connected;
            Message = message;
            Serial = serial ?? string.Empty;
            Product = product ?? string.Empty;
        }
    }

    public sealed class HidDevice : IDisposable
    {
        private const ushort TargetVendorId = 0xF799;
        private const ushort TargetProductId = 0x4707;
        private const int ReportLength = 35;

        private static readonly Guid HidInterfaceGuid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x01;
        private const uint FILE_SHARE_WRITE = 0x02;
        private const uint OPEN_EXISTING = 3;

        private readonly Logger _logger;
        private readonly object _sync = new object();
        private SafeFileHandle _handle;
        private string _serial;
        private string _product;
        private bool _lastConnected;
        private string _lastMessage;

        public HidDevice(Logger logger)
        {
            _logger = logger;
            _serial = string.Empty;
            _product = string.Empty;
        }

        public event EventHandler<HidStatusEventArgs> StatusChanged;

        public bool IsConnected
        {
            get
            {
                lock (_sync)
                {
                    return _handle != null && !_handle.IsInvalid;
                }
            }
        }

        public string Serial
        {
            get { return _serial; }
        }

        public string Product
        {
            get { return _product; }
        }

        public DateTime LastWriteTime { get; private set; }

        public bool TryConnect()
        {
            lock (_sync)
            {
                CloseInternal(false);
                string error;
                if (!TryFindAndOpen(out error))
                {
                    SetStatus(false, error);
                    return false;
                }
                SetStatus(true, "面板已连接");
                return true;
            }
        }

        public bool WriteTemperature(int temperatureCelsius)
        {
            lock (_sync)
            {
                if (_handle == null || _handle.IsInvalid)
                {
                    SetStatus(false, "面板未连接");
                    return false;
                }

                if (temperatureCelsius < 0) temperatureCelsius = 0;
                if (temperatureCelsius > 255) temperatureCelsius = 255;

                byte[] buffer = new byte[ReportLength];
                buffer[0] = 0x01; // report ID
                buffer[1] = 0x01; // AP Series command
                buffer[2] = (byte)temperatureCelsius; // CPU temperature
                buffer[3] = 0x00; // CPU usage (not displayed by AP1000 Pro)
                buffer[4] = 0x00; // power (not displayed)
                buffer[5] = 0x00; // fan high
                buffer[6] = 0x00; // fan low

                uint written = 0;
                bool ok = WriteFile(_handle, buffer, (uint)buffer.Length, out written, IntPtr.Zero);
                if (!ok || written != buffer.Length)
                {
                    int err = Marshal.GetLastWin32Error();
                    string message = written == buffer.Length
                        ? new Win32Exception(err).Message
                        : "写入长度不匹配";
                    _logger.Error("写 HID 失败: " + message, null);
                    CloseInternal(false);
                    SetStatus(false, "写入失败，正在重连: " + message);
                    return false;
                }

                LastWriteTime = DateTime.Now;
                if (!_lastConnected)
                {
                    SetStatus(true, "面板已连接");
                }
                return true;
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                CloseInternal(true);
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void CloseInternal(bool notify)
        {
            try
            {
                if (_handle != null)
                {
                    _handle.Dispose();
                    _handle = null;
                }
            }
            catch
            {
            }
            if (notify)
            {
                SetStatus(false, "面板未连接");
            }
        }

        private void SetStatus(bool connected, string message)
        {
            if (_lastConnected == connected && string.Equals(_lastMessage, message))
            {
                return;
            }
            _lastConnected = connected;
            _lastMessage = message;
            if (connected)
            {
                _logger.Info("HID 已连接: " + _product + " serial=" + _serial);
            }
            else
            {
                _logger.Info("HID 未连接: " + message);
            }
            EventHandler<HidStatusEventArgs> handler = StatusChanged;
            if (handler != null)
            {
                try
                {
                    handler(this, new HidStatusEventArgs(connected, message, _serial, _product));
                }
                catch
                {
                }
            }
        }

        private bool TryFindAndOpen(out string error)
        {
            error = "未找到面板";
            IntPtr hdev = IntPtr.Zero;
            try
            {
                Guid interfaceGuid = HidInterfaceGuid;
                hdev = SetupDiGetClassDevs(ref interfaceGuid, IntPtr.Zero, IntPtr.Zero,
                    DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
                if (hdev == new IntPtr(-1))
                {
                    error = "枚举 HID 设备失败: " + new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return false;
                }

                uint index = 0;
                while (true)
                {
                    SP_DEVICE_INTERFACE_DATA iface = new SP_DEVICE_INTERFACE_DATA();
                    iface.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
                    Guid enumGuid = HidInterfaceGuid;
                    bool ok = SetupDiEnumDeviceInterfaces(hdev, IntPtr.Zero, ref enumGuid, index, ref iface);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err == 259) break; // ERROR_NO_MORE_ITEMS
                        error = "枚举 HID 接口失败: " + new Win32Exception(err).Message;
                        return false;
                    }
                    index++;

                    uint required = 0;
                    SetupDiGetDeviceInterfaceDetail(hdev, ref iface, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    if (required == 0)
                    {
                        continue;
                    }

                    IntPtr detail = Marshal.AllocHGlobal((int)required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        uint got = 0;
                        if (!SetupDiGetDeviceInterfaceDetail(hdev, ref iface, detail, required, out got, IntPtr.Zero))
                        {
                            continue;
                        }

                        string path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                        if (string.IsNullOrEmpty(path)) continue;

                        SafeFileHandle handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE,
                            FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                        if (handle.IsInvalid)
                        {
                            int openErr = Marshal.GetLastWin32Error();
                            if (openErr == 5 || openErr == 32)
                            {
                                error = "面板被其他程序占用";
                            }
                            handle.Dispose();
                            continue;
                        }

                        HIDD_ATTRIBUTES attrs = new HIDD_ATTRIBUTES();
                        attrs.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                        if (!HidD_GetAttributes(handle, ref attrs))
                        {
                            handle.Dispose();
                            continue;
                        }

                        if (attrs.VendorID != TargetVendorId || attrs.ProductID != TargetProductId)
                        {
                            handle.Dispose();
                            continue;
                        }

                        _handle = handle;
                        _serial = ReadString(handle, true);
                        _product = ReadString(handle, false);
                        if (string.IsNullOrEmpty(_product)) _product = "HID Nixie";
                        if (string.IsNullOrEmpty(_serial)) _serial = "unknown";
                        error = string.Empty;
                        return true;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            catch (Exception ex)
            {
                error = "HID 异常: " + ex.Message;
                _logger.Error("枚举 HID 异常", ex);
            }
            finally
            {
                if (hdev != IntPtr.Zero && hdev != new IntPtr(-1))
                {
                    SetupDiDestroyDeviceInfoList(hdev);
                }
            }
            return false;
        }

        private static string ReadString(SafeFileHandle handle, bool serial)
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                bool ok = serial
                    ? HidD_GetSerialNumberString(handle, sb, (uint)(sb.Capacity * 2))
                    : HidD_GetProductString(handle, sb, (uint)(sb.Capacity * 2));
                return ok ? sb.ToString().Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData,
            ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool HidD_GetSerialNumberString(SafeFileHandle hidDeviceObject, StringBuilder buffer, uint bufferLength);

        [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, StringBuilder buffer, uint bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle file, byte[] buffer, uint numberOfBytesToWrite,
            out uint numberOfBytesWritten, IntPtr overlapped);
    }
}
