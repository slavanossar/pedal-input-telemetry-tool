using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PedalTelemetry
{
    public partial class MainWindow : Window
    {
        private HidReader? _hidReader;
        private Config _config;

        public MainWindow()
        {
            InitializeComponent();
            _config = Config.Load();
            InitializeHidReader();
            LoadConfig();
        }

        private void InitializeHidReader()
        {
            _hidReader = new HidReader(OnInputUpdate);
            
            // Set HID mappings from config
            var devices = _hidReader.GetAvailableDevices();
            Guid? clutchGuid = GetGuidFromIndex(_config.ClutchHid, devices);
            Guid? brakeGuid = GetGuidFromIndex(_config.BrakeHid, devices);
            Guid? throttleGuid = GetGuidFromIndex(_config.ThrottleHid, devices);
            
            _hidReader.SetHidMappings(clutchGuid, brakeGuid, throttleGuid,
                _config.ClutchAxis, _config.BrakeAxis, _config.ThrottleAxis);
            _hidReader.Start();
        }

        private Guid? GetGuidFromIndex(int? index, System.Collections.Generic.Dictionary<Guid, string> devices)
        {
            if (!index.HasValue) return null;
            var deviceList = devices.ToList();
            if (index.Value >= 0 && index.Value < deviceList.Count)
                return deviceList[index.Value].Key;
            return null;
        }

        private void LoadConfig()
        {
            // Set colors
            ClutchBar.SetColor(_config.Colors["clutch"]);
            BrakeBar.SetColor(_config.Colors["brake"]);
            ThrottleBar.SetColor(_config.Colors["throttle"]);
            
            // Update trace graph colors and duration
            TraceGraph.UpdateColors(
                _config.Colors["clutch"],
                _config.Colors["brake"],
                _config.Colors["throttle"]);
            TraceGraph.SetTraceSeconds(_config.TraceSeconds);
        }

        private void OnInputUpdate(PedalInput input)
        {
            Dispatcher.Invoke(() =>
            {
                ClutchBar.SetValue(input.Clutch);
                BrakeBar.SetValue(input.Brake);
                ThrottleBar.SetValue(input.Throttle);
                
                TraceGraph.AddSample(input.Clutch, input.Brake, input.Throttle);
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_hidReader)
            {
                Owner = this
            };
            
            if (settingsWindow.ShowDialog() == true)
            {
                // Reload config and refresh UI
                _config = Config.Load();
                LoadConfig();
                
                // Update HID mappings
                var devices = _hidReader?.GetAvailableDevices();
                if (devices != null)
                {
                    Guid? clutchGuid = GetGuidFromIndex(_config.ClutchHid, devices);
                    Guid? brakeGuid = GetGuidFromIndex(_config.BrakeHid, devices);
                    Guid? throttleGuid = GetGuidFromIndex(_config.ThrottleHid, devices);
                    _hidReader?.SetHidMappings(clutchGuid, brakeGuid, throttleGuid,
                        _config.ClutchAxis, _config.BrakeAxis, _config.ThrottleAxis);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _hidReader?.Stop();
            _hidReader?.Dispose();
            base.OnClosed(e);
        }
    }
}
