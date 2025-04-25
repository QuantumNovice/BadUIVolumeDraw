using NAudio.CoreAudioApi;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Transforms.Onnx;
using System.Windows.Controls.Primitives;
using Tesseract;


//using System.Windows.Forms; // for NotifyIcon
//using System.Drawing;       // for Icon

public class MnistImage
{
    [VectorType(1, 1, 28, 28)]
    [ColumnName("Input3")]
    public float[] PixelValues;
    private void Shutdown()
    {
        Application.Current.Shutdown();
    }
}

public class MnistPrediction
{
    [ColumnName("Plus214_Output_0")]
    public float[] PredictedLabels;
}

namespace BadUIVolumeDraw
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _drawing = false;
        private List<Point> _points = new List<Point>();
        private readonly string modelPath = "Models/mnist-12.onnx";
        private readonly MLContext mlContext = new();
        private PredictionEngine<MnistImage, MnistPrediction> predictionEngine;
        private System.Windows.Forms.NotifyIcon trayIcon;


        public MainWindow()
        {
            InitializeComponent();


            

            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Text = "Volume UI";
            trayIcon.Icon = new System.Drawing.Icon("icon.ico"); // Put an icon file in your project and set "Copy to Output Directory"
            trayIcon.Visible = true;
            var popup = this;
            // Right-click menu
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("About", null, (s, ev) => MessageBox.Show("We were trying to create bad UI/UX "));
            menu.Items.Add("Exit", null, (s, ev) => Application.Current.Shutdown());
            trayIcon.ContextMenuStrip = menu;

            var workArea = SystemParameters.WorkArea;
            popup.Left = workArea.Right - popup.Width - 10;
            popup.Top = workArea.Bottom - popup.Height - 10;

            trayIcon.MouseClick += (s, args) =>
            {
                if (args.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    

                    if (!popup.IsVisible)
                    {
                        var workArea = SystemParameters.WorkArea;
                        popup.Left = workArea.Right - popup.Width - 10;
                        popup.Top = workArea.Bottom - popup.Height - 10;
                        popup.Show();
                    }
                    else
                    {
                        popup.Hide();
                    }
                }
                // Right-click shows context menu automatically
            };

            try
            {
                var pipeline = mlContext.Transforms.ApplyOnnxModel(
                    modelFile: modelPath,
                    outputColumnNames: new[] { "Plus214_Output_0" },
                    inputColumnNames: new[] { "Input3" });

                var emptyData = mlContext.Data.LoadFromEnumerable(new List<MnistImage>());
                var model = pipeline.Fit(emptyData);
                predictionEngine = mlContext.Model.CreatePredictionEngine<MnistImage, MnistPrediction>(model);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading ONNX model: " + ex.Message);
            }

        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _drawing = true;
            _points.Clear();
            DrawCanvas.Children.Clear();
        }

       

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_drawing && e.LeftButton == MouseButtonState.Pressed)
            {
                Point p = e.GetPosition(DrawCanvas);
                _points.Add(p);

                Ellipse dot = new Ellipse
                {
                    Fill = Brushes.Black,
                    Width = 12,
                    Height = 12
                };
                Canvas.SetLeft(dot, p.X);
                Canvas.SetTop(dot, p.Y);
                DrawCanvas.Children.Add(dot);
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _drawing = false;
            RecognizeDigitAndSetVolume();
        }

        private void SetVolume_Click(object sender, RoutedEventArgs e)
        {
            if (_points.Count == 0) return;

            double avgY = 0;
            foreach (var point in _points)
                avgY += point.Y;
            avgY /= _points.Count;

            double normalized = 1.0 - (avgY / DrawCanvas.ActualHeight); // 0 = bottom, 1 = top
            float volume = (float)(normalized * 1.0); // volume from 0.0 to 1.0

            SetSystemVolume(volume);
            MessageBox.Show($"Volume set to {(int)(volume * 100)}%");
        }

        private void SetSystemVolume(float volume)
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = volume;
        }

        private float[] GetFormattedInputFromCanvas()
        {
            int size = 28;
            int renderSize = 200; // high-res temp image
            RenderTargetBitmap highRes = new(renderSize, renderSize, 96, 96, PixelFormats.Pbgra32);
            highRes.Render(DrawCanvas);

            // Draw to high-res to keep stroke data
            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(highRes, new Rect(0, 0, size, size)); // scale down
            }

            RenderTargetBitmap scaled = new(size, size, 96, 96, PixelFormats.Pbgra32);
            scaled.Render(visual);

            // Convert to grayscale
            FormatConvertedBitmap gray = new(scaled, PixelFormats.Gray8, null, 0);
            byte[] pixels = new byte[size * size];
            gray.CopyPixels(pixels, size, 0);

            // Invert and normalize
            return pixels.Select(p => (255 - p) / 255f).ToArray();
        }

        private void RecognizeDigitAndSetVolume()
        {
            float[] input = GetFormattedInputFromCanvas();

            var prediction = predictionEngine.Predict(new MnistImage { PixelValues = input });

            if (prediction?.PredictedLabels == null)
            {
                MessageBox.Show("Prediction failed. Check model input/output names.");
                return;
            }

            int digit = prediction.PredictedLabels
                .ToList()
                .IndexOf(prediction.PredictedLabels.Max());


            DigitLabel.Content = $"Digit: {digit}";
            SetSystemVolume(digit / 10f);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // Or use Close() if you want to destroy it
        }
    }
}
