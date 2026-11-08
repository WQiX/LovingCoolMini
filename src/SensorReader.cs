using System;
using System.Collections.Generic;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace LovingCoolMini
{
    public sealed class GpuReading
    {
        public string Id;
        public string Name;
        public string Vendor;
        public bool IsIntegrated;
        public double Temperature;
        public string SensorName;

        public bool HasTemperature
        {
            get { return Temperature > 0; }
        }
    }

    public sealed class SensorSnapshot
    {
        public double CpuTemperature;
        public string CpuSensorName;
        public bool CpuAvailable;
        public List<GpuReading> Gpus;
        public DateTime Time;

        public SensorSnapshot()
        {
            Gpus = new List<GpuReading>();
        }
    }

    public sealed class SensorSnapshotEventArgs : EventArgs
    {
        public SensorSnapshot Snapshot { get; private set; }

        public SensorSnapshotEventArgs(SensorSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }

    public sealed class SensorStatusEventArgs : EventArgs
    {
        public bool Ok { get; private set; }
        public string Message { get; private set; }

        public SensorStatusEventArgs(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }
    }

    public sealed class SensorReader : IDisposable
    {
        private readonly Logger _logger;
        private readonly int _intervalMs;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Thread _thread;
        private Computer _computer;
        private volatile bool _disposed;
        private bool _firstSnapshotLogged;

        public SensorReader(Logger logger, int intervalMs)
        {
            _logger = logger;
            _intervalMs = Math.Max(500, Math.Min(5000, intervalMs));
        }

        public event EventHandler<SensorSnapshotEventArgs> SnapshotAvailable;
        public event EventHandler<SensorStatusEventArgs> StatusChanged;

        public void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Name = "SensorReader";
            _thread.Start();
        }

        public void Stop()
        {
            _stop.Set();
            Thread thread = _thread;
            if (thread != null && thread.IsAlive)
            {
                try { thread.Join(4000); } catch { }
            }
            _thread = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            CloseComputer();
        }

        private void Run()
        {
            int failures = 0;
            while (!_stop.WaitOne(0))
            {
                try
                {
                    if (_computer == null)
                    {
                        RaiseStatus(false, "正在初始化传感器...");
                        _computer = new Computer();
                        _computer.IsCpuEnabled = true;
                        _computer.IsGpuEnabled = true;
                        _computer.IsMotherboardEnabled = false;
                        _computer.IsMemoryEnabled = false;
                        _computer.IsStorageEnabled = false;
                        _computer.Open();
                        _logger.Info("LibreHardwareMonitor 已打开（CPU + GPU）");
                        RaiseStatus(true, "传感器已启动");
                    }

                    SensorSnapshot snapshot = ReadSnapshot();
                    EventHandler<SensorSnapshotEventArgs> handler = SnapshotAvailable;
                    if (handler != null)
                    {
                        try { handler(this, new SensorSnapshotEventArgs(snapshot)); } catch { }
                    }

                    bool anyTemperature = snapshot.CpuAvailable;
                    foreach (GpuReading gpu in snapshot.Gpus)
                    {
                        if (gpu.HasTemperature)
                        {
                            anyTemperature = true;
                            break;
                        }
                    }

                    if (anyTemperature)
                    {
                        failures = 0;
                        if (!_firstSnapshotLogged)
                        {
                            _firstSnapshotLogged = true;
                            LogFirstSnapshot(snapshot);
                        }
                    }
                    else
                    {
                        failures++;
                        if (failures == 5)
                        {
                            RaiseStatus(false, "暂时读不到温度，正在重试...");
                        }
                        if (failures >= 15)
                        {
                            _logger.Error("连续多次读取不到温度，重启传感器", null);
                            CloseComputer();
                            failures = 0;
                            _stop.WaitOne(1000);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("传感器线程异常", ex);
                    RaiseStatus(false, "传感器错误: " + ex.Message);
                    CloseComputer();
                    _stop.WaitOne(2000);
                }

                _stop.WaitOne(_intervalMs);
            }
            CloseComputer();
        }

        private SensorSnapshot ReadSnapshot()
        {
            SensorSnapshot snapshot = new SensorSnapshot();
            snapshot.Time = DateTime.Now;

            if (_computer == null) return snapshot;

            double cpuTctl = 0;
            string cpuTctlName = string.Empty;
            double cpuPackage = 0;
            string cpuPackageName = string.Empty;
            double cpuMax = 0;
            string cpuMaxName = string.Empty;

            foreach (IHardware hw in _computer.Hardware)
            {
                try
                {
                    if (hw.HardwareType == HardwareType.Cpu)
                    {
                        hw.Update();
                        foreach (ISensor sensor in hw.Sensors)
                        {
                            if (sensor.SensorType != SensorType.Temperature) continue;
                            if (!sensor.Value.HasValue) continue;
                            double value = sensor.Value.Value;
                            if (value <= 0 || double.IsNaN(value)) continue;
                            string name = sensor.Name ?? "CPU";
                            string lower = name.ToLowerInvariant();

                            if (lower.Contains("tctl/tdie") || lower.Contains("tctl"))
                            {
                                if (cpuTctl <= 0)
                                {
                                    cpuTctl = value;
                                    cpuTctlName = name;
                                }
                            }
                            else if (lower.Contains("cpu package") || lower.Contains("package"))
                            {
                                if (cpuPackage <= 0)
                                {
                                    cpuPackage = value;
                                    cpuPackageName = name;
                                }
                            }

                            if (value > cpuMax)
                            {
                                cpuMax = value;
                                cpuMaxName = name;
                            }
                        }
                    }
                    else if (IsGpu(hw.HardwareType))
                    {
                        hw.Update();
                        GpuReading gpu = ReadGpu(hw);
                        if (gpu != null)
                        {
                            snapshot.Gpus.Add(gpu);
                        }
                    }
                }
                catch
                {
                    // Keep reading other hardware.
                }
            }

            if (cpuTctl > 0)
            {
                snapshot.CpuTemperature = cpuTctl;
                snapshot.CpuSensorName = cpuTctlName;
            }
            else if (cpuPackage > 0)
            {
                snapshot.CpuTemperature = cpuPackage;
                snapshot.CpuSensorName = cpuPackageName;
            }
            else
            {
                snapshot.CpuTemperature = cpuMax;
                snapshot.CpuSensorName = cpuMaxName;
            }
            snapshot.CpuAvailable = snapshot.CpuTemperature > 0;

            snapshot.Gpus.Sort(delegate(GpuReading a, GpuReading b)
            {
                if (a.IsIntegrated != b.IsIntegrated)
                {
                    return a.IsIntegrated ? 1 : -1;
                }
                int vendor = VendorRank(a.Vendor).CompareTo(VendorRank(b.Vendor));
                if (vendor != 0) return vendor;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return snapshot;
        }

        private GpuReading ReadGpu(IHardware hw)
        {
            GpuReading gpu = new GpuReading();
            gpu.Id = hw.Identifier.ToString();
            gpu.Name = hw.Name;
            gpu.Vendor = VendorName(hw.HardwareType);
            gpu.IsIntegrated = IsIntegratedGpu(hw.HardwareType, hw.Name);

            ISensor best = null;
            int bestRank = int.MaxValue;
            double fallback = 0;
            ISensor fallbackSensor = null;

            foreach (ISensor sensor in hw.Sensors)
            {
                if (sensor.SensorType != SensorType.Temperature) continue;
                if (!sensor.Value.HasValue) continue;
                double value = sensor.Value.Value;
                if (value <= 0 || double.IsNaN(value)) continue;
                string name = sensor.Name ?? string.Empty;
                int rank = GpuSensorRank(name);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    best = sensor;
                }
                if (value > fallback)
                {
                    fallback = value;
                    fallbackSensor = sensor;
                }
            }

            if (best != null)
            {
                gpu.Temperature = best.Value.HasValue ? best.Value.Value : 0;
                gpu.SensorName = best.Name;
            }
            else if (fallbackSensor != null)
            {
                gpu.Temperature = fallback;
                gpu.SensorName = fallbackSensor.Name;
            }
            else
            {
                gpu.Temperature = 0;
                gpu.SensorName = string.Empty;
            }

            return gpu;
        }

        private void LogFirstSnapshot(SensorSnapshot snapshot)
        {
            if (snapshot.CpuAvailable)
            {
                _logger.Info("CPU 温度: " + snapshot.CpuSensorName + " = " + snapshot.CpuTemperature.ToString("F2") + "°C");
            }
            else
            {
                _logger.Info("CPU 温度: 不可用");
            }

            _logger.Info("显卡数量: " + snapshot.Gpus.Count);
            foreach (GpuReading gpu in snapshot.Gpus)
            {
                _logger.Info("GPU: " + gpu.Vendor + " | " + gpu.Name + " | id=" + gpu.Id +
                             " | integrated=" + gpu.IsIntegrated +
                             " | sensor=" + (gpu.SensorName.Length == 0 ? "(none)" : gpu.SensorName) +
                             " | temp=" + (gpu.HasTemperature ? gpu.Temperature.ToString("F2") : "N/A"));
            }
        }

        private static bool IsGpu(HardwareType type)
        {
            return type == HardwareType.GpuNvidia ||
                   type == HardwareType.GpuAmd ||
                   type == HardwareType.GpuIntel;
        }

        private static string VendorName(HardwareType type)
        {
            if (type == HardwareType.GpuNvidia) return "NVIDIA";
            if (type == HardwareType.GpuAmd) return "AMD";
            if (type == HardwareType.GpuIntel) return "Intel";
            return "GPU";
        }

        private static int VendorRank(string vendor)
        {
            if (vendor == "NVIDIA") return 0;
            if (vendor == "AMD") return 1;
            if (vendor == "Intel") return 2;
            return 3;
        }

        private static bool IsIntegratedGpu(HardwareType type, string name)
        {
            if (type == HardwareType.GpuIntel) return true;
            if (type == HardwareType.GpuNvidia) return false;
            string lower = (name ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("radeon(tm) graphics") ||
                lower.Contains("radeon graphics") ||
                lower.Contains("vega") ||
                lower.Contains("renoir") ||
                lower.Contains("cezanne") ||
                lower.Contains("rembrandt") ||
                lower.Contains("raphael"))
            {
                return true;
            }
            return false;
        }

        private static int GpuSensorRank(string name)
        {
            string lower = (name ?? string.Empty).ToLowerInvariant();
            if (lower == "gpu core") return 0;
            if (lower.Contains("gpu core")) return 1;
            if (lower.Contains("gpu temperature")) return 2;
            if (lower.Contains("gpu diode")) return 3;
            if (lower.Contains("gpu edge")) return 4;
            if (lower.Contains("gpu vr soc")) return 5;
            if (lower.Contains("gpu hot spot")) return 6;
            if (lower.Contains("gpu memory junction")) return 7;
            if (lower.Contains("gpu")) return 8;
            return 9;
        }

        private void CloseComputer()
        {
            try
            {
                if (_computer != null)
                {
                    _computer.Close();
                    _computer = null;
                    _logger.Info("LibreHardwareMonitor 已关闭");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("关闭传感器失败", ex);
                _computer = null;
            }
        }

        private void RaiseStatus(bool ok, string message)
        {
            EventHandler<SensorStatusEventArgs> handler = StatusChanged;
            if (handler != null)
            {
                try { handler(this, new SensorStatusEventArgs(ok, message)); } catch { }
            }
        }
    }
}
