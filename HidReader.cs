using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpDX.DirectInput;

namespace PedalTelemetry
{
    public class HidReader
    {
        private DirectInput _directInput;
        private Joystick? _clutchDevice;
        private Joystick? _brakeDevice;
        private Joystick? _throttleDevice;
        private int _clutchAxis = 2;
        private int _brakeAxis = 1;
        private int _throttleAxis = 0;
        private bool _running;
        private Thread? _readThread;
        private readonly Action<PedalInput> _callback;

        public HidReader(Action<PedalInput> callback)
        {
            _callback = callback;
            _directInput = new DirectInput();
        }

        public Dictionary<Guid, string> GetAvailableDevices()
        {
            var devices = new Dictionary<Guid, string>();
            
            try
            {
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating devices: {ex.Message}");
            }

            return devices;
        }

        public void SetHidMappings(Guid? clutchGuid, Guid? brakeGuid, Guid? throttleGuid, 
            int clutchAxis = 2, int brakeAxis = 1, int throttleAxis = 0)
        {
            // Store axis indices
            _clutchAxis = clutchAxis;
            _brakeAxis = brakeAxis;
            _throttleAxis = throttleAxis;

            // Dispose old devices
            _clutchDevice?.Dispose();
            _brakeDevice?.Dispose();
            _throttleDevice?.Dispose();

            _clutchDevice = null;
            _brakeDevice = null;
            _throttleDevice = null;

            try
            {
                if (clutchGuid.HasValue)
                {
                    _clutchDevice = new Joystick(_directInput, clutchGuid.Value);
                    _clutchDevice.Properties.BufferSize = 128;
                    _clutchDevice.Acquire();
                }

                if (brakeGuid.HasValue)
                {
                    _brakeDevice = new Joystick(_directInput, brakeGuid.Value);
                    _brakeDevice.Properties.BufferSize = 128;
                    _brakeDevice.Acquire();
                }

                if (throttleGuid.HasValue)
                {
                    _throttleDevice = new Joystick(_directInput, throttleGuid.Value);
                    _throttleDevice.Properties.BufferSize = 128;
                    _throttleDevice.Acquire();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up devices: {ex.Message}");
            }
        }

        private float ReadAxisValue(Joystick device, int axisIndex)
        {
            try
            {
                device.Poll();
                var state = device.GetCurrentState();

                // If axisIndex >= 100, it's a slider
                if (axisIndex >= 100)
                {
                    var sliderIndex = axisIndex - 100;
                    if (state.Sliders != null && sliderIndex >= 0 && sliderIndex < state.Sliders.Length)
                    {
                        var value = state.Sliders[sliderIndex];
                        var normalized = (value + 32768) / 65536.0f;
                        return Math.Max(0.0f, Math.Min(1.0f, normalized));
                    }
                    return 0.0f;
                }

                // Read from axes array
                var axes = new int[] { state.X, state.Y, state.Z, state.RotationX, state.RotationY, state.RotationZ };
                if (axisIndex >= 0 && axisIndex < axes.Length)
                {
                    var value = axes[axisIndex];
                    // Normalize from -32768 to 32767 to 0.0 to 1.0
                    // For pedals, values typically range from -32768 (released) to 32767 (pressed)
                    // or sometimes inverted. We'll normalize to 0.0 (released) to 1.0 (pressed)
                    var normalized = (value + 32768) / 65536.0f;
                    // Clamp to valid range
                    return Math.Max(0.0f, Math.Min(1.0f, normalized));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading axis {axisIndex}: {ex.Message}");
            }

            return 0.0f;
        }

        public void Start()
        {
            if (_running) return;

            _running = true;
            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true
            };
            _readThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _readThread?.Join(1000);
        }

        private void ReadLoop()
        {
            while (_running)
            {
                var input = new PedalInput();

                // Read clutch using configured axis
                if (_clutchDevice != null)
                {
                    try
                    {
                        input.Clutch = ReadAxisValue(_clutchDevice, _clutchAxis);
                    }
                    catch { }
                }

                // Read brake using configured axis
                if (_brakeDevice != null)
                {
                    try
                    {
                        input.Brake = ReadAxisValue(_brakeDevice, _brakeAxis);
                    }
                    catch { }
                }

                // Read throttle using configured axis
                if (_throttleDevice != null)
                {
                    try
                    {
                        input.Throttle = ReadAxisValue(_throttleDevice, _throttleAxis);
                    }
                    catch { }
                }

                _callback(input);
                Thread.Sleep(16); // ~60 FPS
            }
        }

        public void Dispose()
        {
            Stop();
            _clutchDevice?.Dispose();
            _brakeDevice?.Dispose();
            _throttleDevice?.Dispose();
            _directInput?.Dispose();
        }
    }
}
