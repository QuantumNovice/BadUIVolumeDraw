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
using System.IO;
using Rect = System.Windows.Rect;
using System;
using System.Drawing;



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
        private List<System.Windows.Point> _points = new List<System.Windows.Point>();
        private readonly string modelPath = "Models/mnist-12.onnx";
        private readonly MLContext mlContext = new();
        private PredictionEngine<MnistImage, MnistPrediction> predictionEngine;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private readonly TesseractEngine _engine;


        public MainWindow()
        {
            InitializeComponent();


            var tessData = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            _engine = new TesseractEngine(tessData, "eng", EngineMode.LstmOnly);
            _engine.SetVariable("tessedit_char_whitelist", "0123456789");
            _engine.SetVariable("classify_bln_numeric_mode", "1");


            trayIcon = new System.Windows.Forms.NotifyIcon();
            trayIcon.Text = "Volume UI";
            trayIcon.Icon = new System.Drawing.Icon("icon.ico"); // Put an icon file in your project and set "Copy to Output Directory"
            trayIcon.Visible = true;
            var popup = this;
            // Right-click menu
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("About", null, (s, ev) => MessageBox.Show("We were trying to create bad UI/UX so we said how about this idea. "));
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


        private Bitmap GetBinarizedBitmapFromCanvas()
        {
            const int RENDER_SIZE = 200;

            // 1) Render WPF Canvas to bitmap
            var rtb = new RenderTargetBitmap(RENDER_SIZE, RENDER_SIZE, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(DrawCanvas);

            // 2) Encode to a MemoryStream as BMP
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // 3) Load into System.Drawing.Bitmap
            var bmp = new Bitmap(ms);

            // 4) Binarize + invert: black strokes on white bg
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var px = bmp.GetPixel(x, y);
                    var gray = (px.R + px.G + px.B) / 3;
                    var binary = gray < 128 ? 0 : 255;
                    bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(binary, binary, binary));
                }
            }

            return bmp;
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
                System.Windows.Point p = e.GetPosition(DrawCanvas);
                _points.Add(p);

                Ellipse dot = new Ellipse
                {
                    Fill = System.Windows.Media.Brushes.Black,
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
            int? digit = null;

            // 1) Try Tesseract OCR
            try
            {
                using var bmp = GetBinarizedBitmapFromCanvas();
                using var engine = new TesseractEngine(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"),
                    "eng",
                    EngineMode.LstmOnly);
                engine.SetVariable("tessedit_char_whitelist", "0123456789");
                engine.SetVariable("classify_bln_numeric_mode", "1");

                using var pix = PixConverter.ToPix(bmp);
                using var page = engine.Process(pix, PageSegMode.SingleChar);

                var text = page.GetText().Trim();
                if (int.TryParse(text, out int ocrDigit) && ocrDigit is >= 0 and <= 9)
                {
                    digit = ocrDigit;
                }
            }
            catch
            {
                // swallow; we'll fall back
            }

            // 2) Fallback to MNIST ONNX if OCR didn't yield a valid digit
            if (digit == null)
            {
                try
                {
                    // Get your 28×28 float[] input
                    float[] input = GetFormattedInputFromCanvas();
                    var prediction = predictionEngine.Predict(new MnistImage { PixelValues = input });

                    if (prediction?.PredictedLabels != null)
                    {
                        digit = prediction.PredictedLabels
                                  .Select((score, idx) => (score, idx))
                                  .OrderByDescending(p => p.score)
                                  .First().idx;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("MNIST fallback failed: " + ex.Message);
                }
            }

            // 3) Apply result (or report failure)
            if (digit != null)
            {
                DigitLabel.Content = $"Digit: {digit}";
                SetSystemVolume(digit.Value / 10f);
            }
            else
            {
                DigitLabel.Content = "Unable to recognize digit.";
            }
        }



        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // Or use Close() if you want to destroy it
        }
    }
}
