using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PedalTelemetry
{
    public partial class PedalBar : UserControl
    {
        private Rectangle? _fillRect;
        private TextBlock? _valueText;
        private TextBlock? _labelText;
        private SolidColorBrush _fillBrush;

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(PedalBar),
                new PropertyMetadata("Pedal"));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(PedalBar),
                new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty BarColorProperty =
            DependencyProperty.Register(nameof(BarColor), typeof(Brush), typeof(PedalBar),
                new PropertyMetadata(Brushes.White, OnColorChanged));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, Math.Max(0.0, Math.Min(1.0, value)));
        }

        public Brush BarColor
        {
            get => (Brush)GetValue(BarColorProperty);
            set => SetValue(BarColorProperty, value);
        }

        public PedalBar()
        {
            InitializeComponent();
            _fillBrush = new SolidColorBrush();
            Loaded += PedalBar_Loaded;
        }

        private void PedalBar_Loaded(object sender, RoutedEventArgs e)
        {
            _fillRect = FindName("FillRect") as Rectangle;
            _valueText = FindName("ValueText") as TextBlock;
            _labelText = FindName("LabelText") as TextBlock;
            if (_labelText != null)
                _labelText.Text = Label;
            UpdateFill();
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PedalBar bar)
            {
                bar.UpdateFill();
            }
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PedalBar bar)
            {
                if (e.NewValue is SolidColorBrush brush)
                {
                    bar._fillBrush = brush;
                }
                else if (e.NewValue is Color color)
                {
                    bar._fillBrush = new SolidColorBrush(color);
                }
                bar.UpdateFill();
            }
        }

        public void SetValue(float value)
        {
            Value = value;
        }

        public void SetColor(string colorHex)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            BarColor = new SolidColorBrush(color);
        }


        private void UpdateFill()
        {
            if (_fillRect == null || _valueText == null) return;

            var container = _fillRect.Parent as Grid;
            if (container == null) return;

            var containerHeight = container.ActualHeight;
            if (containerHeight > 0)
            {
                var fillHeight = containerHeight * Value;
                _fillRect.Height = fillHeight;
            }

            _valueText.Text = $"{(int)(Value * 100)}";
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateFill();
        }
    }
}
