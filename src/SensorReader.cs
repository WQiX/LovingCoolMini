using System;
using System.Collections.Generic;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace LovingCoolMini
{
    public sealed class SensorReading
    {
        public double Tctl { get; set; }
        public double Tdie { get; set; }
        public string SensorName { get; set; }
        public DateTime Time { get; set; }
    }

    public sealed class SensorReadingEventArgs : EventArgs
    {
        public SensorReading Reading { get; private set; }

        public SensorReadingEventArgs(SensorReading reading)
        {
            Reading = reading;
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

        public SensorReader(Logger logger, int intervalMs)
        {
            _logger = logger;
            _intervalMs = Math.Max(500, Math.Min(5000, intervalMs));
        }

        public event EventHandler<SensorReadingEventArgs> ReadingAvailable;
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
                        _computer.IsMotherboardEnabled = false;
                        _computer.IsMemoryEnabled = false;
                        _computer.IsGpuEnabled = false;
                        _computer.IsStorageEnabled = false;
                        _computer.Open();
                        _logger.Info("LibreHardwareMonitor 已打开");
                        RaiseStatus(true, "传感器已启动");
                    }

                    SensorReading reading = ReadOnce();
                    if (reading != null)
                    {
                        failures = 0;
                        EventHandler<SensorReadingEventArgs> handler = ReadingAvailable;
                        if (handler != null)
                        {
                            try { handler(this, new SensorReadingEventArgs(reading)); } catch { }
                        }
                    }
                    else
                    {
                        failures++;
                        if (failures == 5)
                        {
                            RaiseStatus(false, "暂时读不到 CPU 温度，正在重试...");
                        }
                        if (failures >= 15)
                        {
                            _logger.Error("连续多次读取不到 CPU 温度，重启传感器", null);
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

        private SensorReading ReadOnce()
        {
            if (_computer == null) return null;

            double bestFallback = 0;
            string bestFallbackName = null;
            double tdie = 0;

            foreach (IHardware hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                try { hw.Update(); } catch { continue; }

                foreach (ISensor sensor in hw.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature) continue;
                    if (!sensor.Value.HasValue) continue;
                    double value = sensor.Value.Value;
                    if (value <= 0 || double.IsNaN(value)) continue;
                    string name = sensor.Name ?? string.Empty;
                    string lower = name.ToLowerInvariant();

                    if (lower.Contains("tctl/tdie") || lower.Contains("tctl") || lower.Contains("cpu package"))
                    {
                        if (tdie <= 0 && lower.Contains("tdie") && !lower.Contains("tctl"))
                        {
                            tdie = value;
                        }
                        return new SensorReading
                        {
                            Tctl = value,
                            Tdie = tdie,
                            SensorName = name,
                            Time = DateTime.Now
                        };
                    }

                    if (value > bestFallback)
                    {
                        bestFallback = value;
                        bestFallbackName = name;
                    }
                }
            }

            if (bestFallback > 0)
            {
                return new SensorReading
                {
                    Tctl = bestFallback,
                    Tdie = tdie,
                    SensorName = bestFallbackName ?? "CPU",
                    Time = DateTime.Now
                };
            }
            return null;
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
