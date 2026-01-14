using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private string _clutchColor = "#0085F1";
        private string _brakeColor = "#D80404";
        private string _throttleColor = "#09B61A";
        private PedalDetector? _detector;
        private bool _isDetecting = false;
        private int? _detectedClutchAxis;
        private int? _detectedBrakeAxis;
        private int? _detectedThrottleAxis;

        public SettingsWindow(HidReader? hidReader)
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing window: {ex.Message}\n\n{ex.StackTrace}", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            try
            {
                _hidReader = hidReader;
                _config = Config.Load();
                
                // Load non-device settings first
                LoadNonDeviceSettings();
                
                // Load device settings after window is loaded to avoid initialization issues
                // Use a longer delay to ensure window is fully rendered before attempting device operations
                Loaded += SettingsWindow_Loaded;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error loading settings: {ex.Message}";
                if (ex.StackTrace != null)
                {
                    errorMsg += $"\n\nStack Trace:\n{ex.StackTrace}";
                }
                MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load device settings asynchronously to avoid blocking UI
            // Use a longer delay to ensure window is fully rendered
            _ = Task.Delay(200).ContinueWith(_ =>
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            RefreshDevices();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in RefreshDevices: {ex.Message}\n{ex.StackTrace}");
                            // Show error but don't crash
                            MessageBox.Show($"Warning: Could not refresh devices. {ex.Message}", 
                                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    });
                    
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            LoadDeviceSettings();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in LoadDeviceSettings: {ex.Message}\n{ex.StackTrace}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error loading device settings: {ex.Message}\n\n{ex.StackTrace}", 
                            "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            }, TaskScheduler.Default);
        }

        private void LoadNonDeviceSettings()
        {
            // Load trace seconds
            try
            {
                if (TraceSecondsSlider != null && TraceSecondsLabel != null)
                {
                    // Temporarily remove event handler to prevent ValueChanged from firing
                    TraceSecondsSlider.ValueChanged -= TraceSecondsSlider_ValueChanged;
                    TraceSecondsSlider.Value = _config.TraceSeconds;
                    TraceSecondsLabel.Content = $"{_config.TraceSeconds} seconds";
                    // Re-add event handler
                    TraceSecondsSlider.ValueChanged += TraceSecondsSlider_ValueChanged;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading trace seconds: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Load colors
            try
            {
                if (_config.Colors != null)
                {
                    if (_config.Colors.ContainsKey("clutch"))
                        _clutchColor = _config.Colors["clutch"];
                    if (_config.Colors.ContainsKey("brake"))
                        _brakeColor = _config.Colors["brake"];
                    if (_config.Colors.ContainsKey("throttle"))
                        _throttleColor = _config.Colors["throttle"];
                }

                UpdateColorButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading colors: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadDeviceSettings()
        {
            // Load axis indices
            _detectedClutchAxis = _config.ClutchAxis;
            _detectedBrakeAxis = _config.BrakeAxis;
            _detectedThrottleAxis = _config.ThrottleAxis;

            // Load selected devices
            try
            {
                if (_config.ClutchHid.HasValue)
                    SetComboSelection(ClutchCombo, _config.ClutchHid.Value);
                if (_config.BrakeHid.HasValue)
                    SetComboSelection(BrakeCombo, _config.BrakeHid.Value);
                if (_config.ThrottleHid.HasValue)
                    SetComboSelection(ThrottleCombo, _config.ThrottleHid.Value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting combo selections: {ex.Message}");
            }

            // Show current axis in status labels
            try
            {
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting status labels: {ex.Message}");
            }
        }

        private void RefreshDevices()
        {
            if (_hidReader == null)
            {
                MessageBox.Show("HID reader not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if combo boxes are initialized
            if (ClutchCombo == null || BrakeCombo == null || ThrottleCombo == null)
            {
                System.Diagnostics.Debug.WriteLine("Combo boxes not initialized yet");
                return;
            }

            try
            {
                var devices = _hidReader.GetAvailableDevices();
                var deviceList = devices?.ToList() ?? new List<System.Collections.Generic.KeyValuePair<Guid, string>>();

                // Clear and populate combo boxes
                foreach (var combo in new[] { ClutchCombo, BrakeCombo, ThrottleCombo })
                {
                    if (combo == null) continue;
                    
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
            catch (Exception ex)
            {
                var errorMsg = $"Error refreshing devices: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                MessageBox.Show(errorMsg, "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                // Still populate with "None" option even if device enumeration fails
                foreach (var combo in new[] { ClutchCombo, BrakeCombo, ThrottleCombo })
                {
                    if (combo == null) continue;
                    combo.Items.Clear();
                    combo.Items.Add(new ComboBoxItem { Content = "None", Tag = (int?)null });
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
            // Disable button while refreshing
            if (sender is Button btn)
            {
                btn.IsEnabled = false;
                btn.Content = "Refreshing...";
            }
            
            Task.Run(() =>
            {
                try
                {
                    Dispatcher.Invoke(() => RefreshDevices());
                    Dispatcher.Invoke(() => LoadDeviceSettings());
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error refreshing devices: {ex.Message}", 
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (sender is Button button)
                        {
                            button.IsEnabled = true;
                            button.Content = "Refresh Devices";
                        }
                    });
                }
            });
        }

        private void TraceSecondsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TraceSecondsLabel == null) return; // Label not initialized yet
            
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
            try
            {
                if (ClutchColorBtn != null)
                {
                    ClutchColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_clutchColor));
                }
                if (BrakeColorBtn != null)
                {
                    BrakeColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_brakeColor));
                }
                if (ThrottleColorBtn != null)
                {
                    ThrottleColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_throttleColor));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating color buttons: {ex.Message}");
            }
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
                
                // Restart HidReader when detection is stopped
                _hidReader?.Start();
                return;
            }

            _isDetecting = true;
            button.Content = "Stop";
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D80404"));
            statusLabel.Content = "Waiting for input...";

            // Temporarily stop HidReader to release devices for detection
            _hidReader?.Stop();

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
                            statusLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D80404"));
                        }

                        _isDetecting = false;
                        button.Content = "Detect";
                        button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                        
                        // Restart HidReader after detection completes (success or failure)
                        _hidReader?.Start();
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
            
            // Make sure HidReader is restarted if window closes during detection
            if (_isDetecting)
            {
                _hidReader?.Start();
            }
            
            base.OnClosed(e);
        }
    }
}
