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
        private Config _config = new Config();
        private string _clutchColor = "#0085F1";
        private string _brakeColor = "#D80404";
        private string _throttleColor = "#09B61A";

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
                            RefreshAxes();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in RefreshAxes: {ex.Message}\n{ex.StackTrace}");
                            // Show error but don't crash
                            MessageBox.Show($"Warning: Could not refresh axes. {ex.Message}", 
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
            // Refresh axes first, then select based on config
            RefreshAxes();
            
            // Select axes based on saved config
            try
            {
                SelectAxisFromConfig(ClutchAxisCombo, _config.ClutchHid, _config.ClutchAxis);
                SelectAxisFromConfig(BrakeAxisCombo, _config.BrakeHid, _config.BrakeAxis);
                SelectAxisFromConfig(ThrottleAxisCombo, _config.ThrottleHid, _config.ThrottleAxis);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading axis selections: {ex.Message}");
            }
        }

        private void SelectAxisFromConfig(ComboBox combo, int? deviceIndex, int axisIndex)
        {
            if (!deviceIndex.HasValue) return;
            
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag is HidReader.AxisInfo axis && 
                    axis.DeviceIndex == deviceIndex.Value && 
                    axis.AxisIndex == axisIndex)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private void RefreshAxes()
        {
            if (_hidReader == null)
            {
                MessageBox.Show("HID reader not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check if combo boxes are initialized
            if (ClutchAxisCombo == null || BrakeAxisCombo == null || ThrottleAxisCombo == null)
            {
                System.Diagnostics.Debug.WriteLine("Axis combo boxes not initialized yet");
                return;
            }

            try
            {
                // Temporarily stop HidReader to access devices
                _hidReader?.Stop();
                
                var axes = _hidReader.GetAllAvailableAxes();

                // Store current selections
                var clutchSelection = GetSelectedAxis(ClutchAxisCombo);
                var brakeSelection = GetSelectedAxis(BrakeAxisCombo);
                var throttleSelection = GetSelectedAxis(ThrottleAxisCombo);

                // Clear and populate combo boxes
                PopulateAxisCombo(ClutchAxisCombo, axes, clutchSelection);
                PopulateAxisCombo(BrakeAxisCombo, axes, brakeSelection);
                PopulateAxisCombo(ThrottleAxisCombo, axes, throttleSelection);
                
                // Restart HidReader
                _hidReader?.Start();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error refreshing axes: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                MessageBox.Show(errorMsg, "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                // Still populate with "None" option even if enumeration fails
                foreach (var combo in new[] { ClutchAxisCombo, BrakeAxisCombo, ThrottleAxisCombo })
                {
                    if (combo == null) continue;
                    combo.Items.Clear();
                    combo.Items.Add(new ComboBoxItem { Content = "None", Tag = (HidReader.AxisInfo?)null });
                }
                
                _hidReader?.Start();
            }
        }

        private void PopulateAxisCombo(ComboBox combo, List<HidReader.AxisInfo> axes, (int deviceIndex, int axisIndex)? currentSelection)
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = "None", Tag = (HidReader.AxisInfo?)null });

            foreach (var axis in axes)
            {
                var item = new ComboBoxItem 
                { 
                    Content = axis.AxisName, 
                    Tag = axis 
                };
                combo.Items.Add(item);
                
                // Select if matches current selection
                if (currentSelection.HasValue && 
                    axis.DeviceIndex == currentSelection.Value.deviceIndex && 
                    axis.AxisIndex == currentSelection.Value.axisIndex)
                {
                    combo.SelectedItem = item;
                }
            }
        }

        private (int deviceIndex, int axisIndex)? GetSelectedAxis(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is HidReader.AxisInfo axis)
            {
                return (axis.DeviceIndex, axis.AxisIndex);
            }
            return null;
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

        private void RefreshAxes_Click(object sender, RoutedEventArgs e)
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
            // Save axis selections
            var clutchAxis = GetSelectedAxis(ClutchAxisCombo);
            var brakeAxis = GetSelectedAxis(BrakeAxisCombo);
            var throttleAxis = GetSelectedAxis(ThrottleAxisCombo);

            if (clutchAxis.HasValue)
            {
                _config.ClutchHid = clutchAxis.Value.deviceIndex;
                _config.ClutchAxis = clutchAxis.Value.axisIndex;
            }
            else
            {
                _config.ClutchHid = null;
            }

            if (brakeAxis.HasValue)
            {
                _config.BrakeHid = brakeAxis.Value.deviceIndex;
                _config.BrakeAxis = brakeAxis.Value.axisIndex;
            }
            else
            {
                _config.BrakeHid = null;
            }

            if (throttleAxis.HasValue)
            {
                _config.ThrottleHid = throttleAxis.Value.deviceIndex;
                _config.ThrottleAxis = throttleAxis.Value.axisIndex;
            }
            else
            {
                _config.ThrottleHid = null;
            }

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

        private void ClutchAxisCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Selection changed - no action needed, will be saved on Save button
        }

        private void BrakeAxisCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Selection changed - no action needed, will be saved on Save button
        }

        private void ThrottleAxisCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Selection changed - no action needed, will be saved on Save button
        }


        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
