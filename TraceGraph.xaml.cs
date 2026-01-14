using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PedalTelemetry
{
    public partial class TraceGraph : UserControl
    {
        private readonly List<(double time, float clutch, float brake, float throttle)> _traceData = new();
        private int _traceSeconds = 10;
        private readonly DispatcherTimer _updateTimer;
        private readonly Polyline _clutchLine;
        private readonly Polyline _brakeLine;
        private readonly Polyline _throttleLine;

        public TraceGraph()
        {
            InitializeComponent();

            // Create polylines for traces
            _clutchLine = new Polyline
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B")),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            _brakeLine = new Polyline
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ECDC4")),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            _throttleLine = new Polyline
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95E1D3")),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            var canvas = new System.Windows.Controls.Canvas();
            canvas.Children.Add(_clutchLine);
            canvas.Children.Add(_brakeLine);
            canvas.Children.Add(_throttleLine);
            Content = canvas;

            // Update timer for smooth scrolling
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _updateTimer.Tick += (s, e) => InvalidateVisual();
            _updateTimer.Start();
        }

        public void AddSample(float clutch, float brake, float throttle)
        {
            var time = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
            _traceData.Add((time, clutch, brake, throttle));

            // Remove old data
            var cutoffTime = time - _traceSeconds;
            _traceData.RemoveAll(d => d.time < cutoffTime);
        }

        public void SetTraceSeconds(int seconds)
        {
            _traceSeconds = seconds;
            var currentTime = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
            var cutoffTime = currentTime - _traceSeconds;
            _traceData.RemoveAll(d => d.time < cutoffTime);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var width = ActualWidth;
            var height = ActualHeight;

            if (width <= 0 || height <= 0 || _traceData.Count == 0)
                return;

            // Draw grid
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1);
            for (int i = 0; i <= 4; i++)
            {
                var y = height * i / 4.0;
                dc.DrawLine(gridPen, new Point(0, y), new Point(width, y));
            }

            // Get time range
            var currentTime = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
            var cutoffTime = currentTime - _traceSeconds;

            // Draw traces
            DrawTrace(dc, _traceData, d => d.clutch, _clutchLine.Stroke, width, height, currentTime, cutoffTime);
            DrawTrace(dc, _traceData, d => d.brake, _brakeLine.Stroke, width, height, currentTime, cutoffTime);
            DrawTrace(dc, _traceData, d => d.throttle, _throttleLine.Stroke, width, height, currentTime, cutoffTime);
        }

        private void DrawTrace(DrawingContext dc, List<(double time, float clutch, float brake, float throttle)> data,
            Func<(double time, float clutch, float brake, float throttle), float> valueSelector,
            Brush brush, double width, double height, double currentTime, double cutoffTime)
        {
            var points = new List<Point>();

            foreach (var sample in data)
            {
                if (sample.time < cutoffTime)
                    continue;

                var timeOffset = currentTime - sample.time;
                var x = (_traceSeconds - timeOffset) / _traceSeconds * width;
                x = Math.Max(0, Math.Min(width, x));

                var value = valueSelector(sample);
                var y = height - (value * height);

                points.Add(new Point(x, y));
            }

            if (points.Count < 2)
                return;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], false, false);
                for (int i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(points[i], true, false);
                }
            }

            var pen = new Pen(brush, 2);
            dc.DrawGeometry(null, pen, geometry);
        }

        public void UpdateColors(string clutchColor, string brakeColor, string throttleColor)
        {
            _clutchLine.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(clutchColor));
            _brakeLine.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(brakeColor));
            _throttleLine.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(throttleColor));
        }
    }
}
