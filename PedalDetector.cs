using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.DirectInput;

namespace PedalTelemetry
{
    public class PedalDetector
    {
        private DirectInput _directInput;
        private bool _detecting;
        private CancellationTokenSource? _cancellationTokenSource;
        private Dictionary<Guid, Joystick> _monitoredDevices = new();

        public PedalDetector()
        {
            _directInput = new DirectInput();
        }

        public class DetectionResult
        {
            public Guid DeviceGuid { get; set; }
            public string DeviceName { get; set; } = "";
            public int DeviceIndex { get; set; }
            public int AxisIndex { get; set; }
        }

        public void StartDetection(Action<DetectionResult?> onDetected, Action<string> onStatusUpdate, int timeoutSeconds = 10)
        {
            if (_detecting)
            {
                StopDetection();
            }

            _detecting = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            Task.Run(() =>
            {
                try
                {
                    // Get all available devices
                    var devices = new Dictionary<Guid, string>();
                    foreach (var deviceInstance in _directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                    {
                        devices[deviceInstance.InstanceGuid] = deviceInstance.ProductName;
                    }
                    foreach (var deviceInstance in _directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                    {
                        if (!devices.ContainsKey(deviceInstance.InstanceGuid))
                        {
                            devices[deviceInstance.InstanceGuid] = deviceInstance.ProductName;
                        }
                    }

                    if (devices.Count == 0)
                    {
                        onStatusUpdate?.Invoke("No devices found. Please connect a controller.");
                        onDetected?.Invoke(null);
                        return;
                    }

                    // Open and monitor all devices
                    var deviceList = devices.ToList();
                    var baselineValues = new Dictionary<(Guid, int), int>();

                    foreach (var (guid, name) in devices)
                    {
                        try
                        {
                            var joystick = new Joystick(_directInput, guid);
                            joystick.Properties.BufferSize = 128;
                            joystick.Acquire();
                            _monitoredDevices[guid] = joystick;

                            // Get baseline values for all axes
                            joystick.Poll();
                            var state = joystick.GetCurrentState();
                            var axes = new int[] { state.X, state.Y, state.Z, state.RotationX, state.RotationY, state.RotationZ };
                            for (int i = 0; i < axes.Length; i++)
                            {
                                baselineValues[(guid, i)] = axes[i];
                            }
                        }
                        catch (Exception ex)
                        {
                            // Skip devices that can't be opened (might be in use by HidReader)
                            System.Diagnostics.Debug.WriteLine($"Could not open device {name}: {ex.Message}");
                        }
                    }

                    if (_monitoredDevices.Count == 0)
                    {
                        onStatusUpdate?.Invoke("No devices available for detection. They may be in use.");
                        onDetected?.Invoke(null);
                        return;
                    }

                    onStatusUpdate?.Invoke("Press the pedal now...");

                    // Wait a moment to let baselines stabilize
                    Thread.Sleep(200);

                    // Re-establish baselines after the wait
                    foreach (var (guid, joystick) in _monitoredDevices)
                    {
                        try
                        {
                            joystick.Poll();
                            var state = joystick.GetCurrentState();
                            var axes = new int[] { state.X, state.Y, state.Z, state.RotationX, state.RotationY, state.RotationZ };
                            for (int i = 0; i < axes.Length; i++)
                            {
                                baselineValues[(guid, i)] = axes[i];
                            }
                            
                            // Also set slider baselines
                            if (state.Sliders != null)
                            {
                                for (int sliderIndex = 0; sliderIndex < state.Sliders.Length; sliderIndex++)
                                {
                                    baselineValues[(guid, 100 + sliderIndex)] = state.Sliders[sliderIndex];
                                }
                            }
                        }
                        catch
                        {
                            // Skip if can't read
                        }
                    }

                    var startTime = DateTime.Now;
                    var threshold = 3000; // Lower threshold (about 9% of full range) for better sensitivity

                    while (!token.IsCancellationRequested && (DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
                    {
                        foreach (var (guid, joystick) in _monitoredDevices)
                        {
                            try
                            {
                                joystick.Poll();
                                var state = joystick.GetCurrentState();
                                var axes = new int[] { state.X, state.Y, state.Z, state.RotationX, state.RotationY, state.RotationZ };

                                for (int axisIndex = 0; axisIndex < axes.Length; axisIndex++)
                                {
                                    var key = (guid, axisIndex);
                                    if (baselineValues.TryGetValue(key, out var baseline))
                                    {
                                        var current = axes[axisIndex];
                                        var change = Math.Abs(current - baseline);

                                        // If axis changed significantly, we found it!
                                        if (change > threshold)
                                        {
                                            var deviceIndex = deviceList.FindIndex(d => d.Key == guid);
                                            var deviceName = devices[guid];

                                            onStatusUpdate?.Invoke($"Detected: {deviceName} (Axis {axisIndex})");
                                            
                                            var result = new DetectionResult
                                            {
                                                DeviceGuid = guid,
                                                DeviceName = deviceName,
                                                DeviceIndex = deviceIndex,
                                                AxisIndex = axisIndex
                                            };

                                            onDetected?.Invoke(result);
                                            return;
                                        }
                                    }
                                }

                                // Also check sliders
                                if (state.Sliders != null)
                                {
                                    for (int sliderIndex = 0; sliderIndex < state.Sliders.Length; sliderIndex++)
                                    {
                                        var current = state.Sliders[sliderIndex];
                                        var key = (guid, 100 + sliderIndex); // Use offset to distinguish from axes
                                        if (baselineValues.TryGetValue(key, out var sliderBaseline))
                                        {
                                            var change = Math.Abs(current - sliderBaseline);
                                            if (change > threshold)
                                            {
                                                var deviceIndex = deviceList.FindIndex(d => d.Key == guid);
                                                var deviceName = devices[guid];

                                                onStatusUpdate?.Invoke($"Detected: {deviceName} (Slider {sliderIndex})");
                                                
                                                var result = new DetectionResult
                                                {
                                                    DeviceGuid = guid,
                                                    DeviceName = deviceName,
                                                    DeviceIndex = deviceIndex,
                                                    AxisIndex = 100 + sliderIndex // Mark as slider
                                                };

                                                onDetected?.Invoke(result);
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Device might have been disconnected, skip it
                            }
                        }

                        Thread.Sleep(50); // Check every 50ms
                    }

                    // Timeout
                    if (!token.IsCancellationRequested)
                    {
                        onStatusUpdate?.Invoke("Detection timeout. No pedal input detected.");
                        onDetected?.Invoke(null);
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error during detection: {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMsg += $"\nInner: {ex.InnerException.Message}";
                    }
                    System.Diagnostics.Debug.WriteLine($"Detection error: {errorMsg}\n{ex.StackTrace}");
                    onStatusUpdate?.Invoke(errorMsg);
                    onDetected?.Invoke(null);
                }
            }, token);
        }

        public void StopDetection()
        {
            _detecting = false;
            _cancellationTokenSource?.Cancel();

            // Dispose all monitored devices
            foreach (var joystick in _monitoredDevices.Values)
            {
                try
                {
                    joystick.Unacquire();
                    joystick.Dispose();
                }
                catch { }
            }
            _monitoredDevices.Clear();
        }

        public void Dispose()
        {
            StopDetection();
            _directInput?.Dispose();
        }
    }
}
