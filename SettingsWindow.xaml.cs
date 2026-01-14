using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PedalTelemetry
{
    public partial class SettingsWindow : Window
    {
        private readonly HidReader? _hidReader;
        private Config _config;
        private string _clutchColor = "#FF6B6B";
        private string _brakeColor = "#4ECDC4";
        private string _throttleColor = "#95E1D3";
        private PedalDetector? _detector;
        private bool _isDetecting = false;
        private int? _detectedClutchAxis;
        private int? _detectedBrakeAxis;
        private int? _detectedThrottleAxis;

        public SettingsWindow(HidReader? hidReader)
        {
            InitializeComponent();
            _hidReader = hidReader;
            _config = Config.Load();
            LoadSettings();
        }

        private void LoadSettings()
        {
            RefreshDevices();

            // Load selected devices
            if (_config.ClutchHid.HasValue)
                SetComboSelection(ClutchCombo, _config.ClutchHid.Value);
            if (_config.BrakeHid.HasValue)
                SetComboSelection(BrakeCombo, _config.BrakeHid.Value);
            if (_config.ThrottleHid.HasValue)
                SetComboSelection(ThrottleCombo, _config.ThrottleHid.Value);

            // Load axis indices
            _detectedClutchAxis = _config.ClutchAxis;
            _detectedBrakeAxis = _config.BrakeAxis;
            _detectedThrottleAxis = _config.ThrottleAxis;

            // Show current axis in status labels
            if (_config.ClutchHid.HasValue && _detectedClutchAxis.HasValue)
            {
                var axisLabel = _detectedClutchAxis.Value >= 100 ? $"Slider {_detectedClutchAxis.Value - 100}" : $"Axis {_detectedClutchAxis.Value}";
                ClutchDetectStatus.Content = $"Current: {axisLabel}";
            }
            if (_config.BrakeHid.HasValue && _detectedBrakeAxis.HasValue)
            {
                var axisLabel = _detectedBrakeAxis.Value >= 100 ? $"Slider {_detectedBrakeAxis.Value - 100}" : $"Axis {_detectedBrakeAxis.Value}";
                BrakeDetectStatus.Content = $"Current: {axisLabel}";
            }
            if (_config.ThrottleHid.HasValue && _detectedThrottleAxis.HasValue)
            {
                var axisLabel = _detectedThrottleAxis.Value >= 100 ? $"Slider {_detectedThrottleAxis.Value - 100}" : $"Axis {_detectedThrottleAxis.Value}";
                ThrottleDetectStatus.Content = $"Current: {axisLabel}";
            }

            // Load trace seconds
            TraceSecondsSlider.Value = _config.TraceSeconds;
            TraceSecondsLabel.Content = $"{_config.TraceSeconds} seconds";

            // Load colors
            if (_config.Colors.ContainsKey("clutch"))
                _clutchColor = _config.Colors["clutch"];
            if (_config.Colors.ContainsKey("brake"))
                _brakeColor = _config.Colors["brake"];
            if (_config.Colors.ContainsKey("throttle"))
                _throttleColor = _config.Colors["throttle"];

            UpdateColorButtons();
        }

        private void RefreshDevices()
        {
            if (_hidReader == null)
            {
                MessageBox.Show("HID reader not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var devices = _hidReader.GetAvailableDevices();
            var deviceList = devices.ToList();

            // Clear and populate combo boxes
            foreach (var combo in new[] { ClutchCombo, BrakeCombo, ThrottleCombo })
            {
                combo.Items.Clear();
                combo.Items.Add(new ComboBoxItem { Content = "None", Tag = (int?)null });

                for (int i = 0; i < deviceList.Count; i++)
                {
                    var device = deviceList[i];
                    combo.Items.Add(new ComboBoxItem 
                    { 
                        Content = $"Device {i}: {device.Value}", 
                        Tag = i 
                    });
                }
            }
        }

        private void SetComboSelection(System.Windows.Controls.ComboBox combo, int index)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item && item.Tag is int tag && tag == index)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private int? GetSelectedDeviceIndex(System.Windows.Controls.ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is int index)
                return index;
            return null;
        }

        private void RefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            RefreshDevices();
        }

        private void TraceSecondsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var seconds = (int)e.NewValue;
            TraceSecondsLabel.Content = $"{seconds} seconds";
        }

        private void ClutchColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ChooseColor("Clutch", ref _clutchColor, ClutchColorBtn);
        }

        private void BrakeColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ChooseColor("Brake", ref _brakeColor, BrakeColorBtn);
        }

        private void ThrottleColorBtn_Click(object sender, RoutedEventArgs e)
        {
            ChooseColor("Throttle", ref _throttleColor, ThrottleColorBtn);
        }

        private void ChooseColor(string pedalName, ref string colorHex, System.Windows.Controls.Button button)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            try
            {
                var currentColor = (Color)ColorConverter.ConvertFromString(colorHex);
                colorDialog.Color = System.Drawing.Color.FromArgb(
                    currentColor.A, currentColor.R, currentColor.G, currentColor.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = colorDialog.Color;
                    colorHex = $"#{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
                    button.Background = new SolidColorBrush(Color.FromRgb(newColor.R, newColor.G, newColor.B));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error choosing color: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateColorButtons()
        {
            ClutchColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_clutchColor));
            BrakeColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_brakeColor));
            ThrottleColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_throttleColor));
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save HID devices
            _config.ClutchHid = GetSelectedDeviceIndex(ClutchCombo);
            _config.BrakeHid = GetSelectedDeviceIndex(BrakeCombo);
            _config.ThrottleHid = GetSelectedDeviceIndex(ThrottleCombo);

            // Note: Axis indices are saved when detected via the Detect button
            // They're stored in the config when detection completes

            // Save trace seconds
            _config.TraceSeconds = (int)TraceSecondsSlider.Value;

            // Save colors
            _config.Colors["clutch"] = _clutchColor;
            _config.Colors["brake"] = _brakeColor;
            _config.Colors["throttle"] = _throttleColor;

            _config.Save();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _detector?.StopDetection();
            _detector?.Dispose();
            DialogResult = false;
            Close();
        }

        private void ClutchDetectBtn_Click(object sender, RoutedEventArgs e)
        {
            StartDetection("clutch", ClutchCombo, ClutchDetectBtn, ClutchDetectStatus);
        }

        private void BrakeDetectBtn_Click(object sender, RoutedEventArgs e)
        {
            StartDetection("brake", BrakeCombo, BrakeDetectBtn, BrakeDetectStatus);
        }

        private void ThrottleDetectBtn_Click(object sender, RoutedEventArgs e)
        {
            StartDetection("throttle", ThrottleCombo, ThrottleDetectBtn, ThrottleDetectStatus);
        }

        private void StartDetection(string pedalName, System.Windows.Controls.ComboBox combo, 
            System.Windows.Controls.Button button, System.Windows.Controls.Label statusLabel)
        {
            if (_isDetecting)
            {
                // Stop current detection
                _detector?.StopDetection();
                _isDetecting = false;
                button.Content = "Detect";
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                statusLabel.Content = "";
                return;
            }

            _isDetecting = true;
            button.Content = "Stop";
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B"));
            statusLabel.Content = "Waiting for input...";

            if (_detector == null)
            {
                _detector = new PedalDetector();
            }

            _detector.StartDetection(
                result =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (result != null)
                        {
                            // Set the combo box to the detected device
                            SetComboSelection(combo, result.DeviceIndex);
                            
                            // Store the detected axis
                            if (pedalName == "clutch")
                            {
                                _config.ClutchAxis = result.AxisIndex;
                                _detectedClutchAxis = result.AxisIndex;
                            }
                            else if (pedalName == "brake")
                            {
                                _config.BrakeAxis = result.AxisIndex;
                                _detectedBrakeAxis = result.AxisIndex;
                            }
                            else if (pedalName == "throttle")
                            {
                                _config.ThrottleAxis = result.AxisIndex;
                                _detectedThrottleAxis = result.AxisIndex;
                            }
                            
                            var axisLabel = result.AxisIndex >= 100 ? $"Slider {result.AxisIndex - 100}" : $"Axis {result.AxisIndex}";
                            statusLabel.Content = $"Detected: {axisLabel}";
                            statusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                        }
                        else
                        {
                            statusLabel.Content = "Not detected";
                            statusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B"));
                        }

                        _isDetecting = false;
                        button.Content = "Detect";
                        button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    });
                },
                status =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusLabel.Content = status;
                    });
                },
                timeoutSeconds: 10
            );
        }

        protected override void OnClosed(EventArgs e)
        {
            _detector?.StopDetection();
            _detector?.Dispose();
            base.OnClosed(e);
        }
    }
}
