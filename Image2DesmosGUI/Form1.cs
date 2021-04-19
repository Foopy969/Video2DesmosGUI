using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Video2DesmosGUI;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Image2DesmosGUI
{
    public partial class Form1 : Form
    {
        readonly string[] SUPPORTED_IMAGE_FORMATS = new string[] { ".bmp", ".dib", ".jpeg", ".jpg", ".jpe", ".jp2", ".png", ".webp", ".pbm", ".pgm", ".ppm", ".pxm", ".pnm", ".pfm", ".sr", ".ras", ".tiff", ".tif", ".exr", ".hdr", ".pic" };
        readonly string[] SUPPORTED_VIDEO_FORMATS = new string[] { ".avi", ".mp4"}; //I can't find the list ;-;

        //options
        Size sSize = new Size(0, 0);
        Size kSize = new Size(5, 5);
        double sigmaX = 0;
        double threshold1 = 177;
        double threshold2 = 255;
        double epsilon = 0.1;
        int clumpSize = 0;

        Mat image;
        VideoCapture video;
        Mode currentMode = Mode.None;

        public Form1()
        {
            InitializeComponent();
        }

        private void DropReceiver_DragDrop(object sender, DragEventArgs e)
        {
            string path = (e.Data.GetData(DataFormats.FileDrop) as string[])[0];
            DropReceiver.Text = path.Truncate(50);

            if (SUPPORTED_IMAGE_FORMATS.Any(path.EndsWith))
            {
                image = Cv2.ImRead(path, ImreadModes.Grayscale);
                trackBar1.Maximum = 0;
                textBox1.Text = image.Width.ToString();
                textBox2.Text = image.Height.ToString();
                sSize = new Size(image.Width, image.Height);
                currentMode = Mode.Image;
                label9.Text = "frame count: 1";
            }
            else if (SUPPORTED_VIDEO_FORMATS.Any(path.EndsWith))
            {
                image = new();
                video = VideoCapture.FromFile(path);
                trackBar1.Maximum = video.FrameCount - 1;
                textBox1.Text = video.FrameWidth.ToString();
                textBox2.Text = video.FrameHeight.ToString();
                sSize = new Size(video.FrameWidth, video.FrameHeight);
                currentMode = Mode.Video;
                label9.Text = "frame count: " + video.FrameCount;
            }
            else
            {
                MessageBox.Show("Format Not Supported", "Error");
                currentMode = Mode.None;
                image = Mat.Zeros(sSize.Height, sSize.Width, MatType.CV_8UC1);
                label9.Text = "frame count: 0";
                label11.Text = "line count: 0";
                label12.Text = "point count: 0";
                return;
            }
            UpdatePreview();
        }

        private void DropReceiver_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Options_Changed(object sender, EventArgs e)
        {
            if (currentMode == Mode.None) return;
            sSize = new Size(Convert.ToInt32(textBox1.Text), Convert.ToInt32(textBox2.Text));
            kSize = new Size((int)numericUpDown1.Value, (int)numericUpDown1.Value);
            sigmaX = (int)numericUpDown2.Value;
            threshold1 = (int)numericUpDown3.Value;
            threshold2 = (int)numericUpDown4.Value;
            epsilon = (double)numericUpDown5.Value;
            clumpSize = (int)numericUpDown6.Value;
            UpdatePreview();
        }

        private Point[][] ComputeCurves()
        {
            if (currentMode == Mode.Video)
            {
                video.Read(image);
                image.CvtColor(ColorConversionCodes.BGR2GRAY);
            }

            Point[][] contours = new Point[][] { };
            Mat resize = image.Resize(sSize);
            Mat blur = resize.GaussianBlur(kSize, sigmaX);
            Mat canny = blur.Canny(threshold1, threshold2);

            canny.FindContours(out contours, out var hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            List<Point[]> decimated = new();

            foreach (var contour in contours)
            {
                Point[] temp = Decimate(contour);
                Point center = new Point(temp.Sum(x => x.X) / temp.Length, temp.Sum(x => x.Y) / temp.Length);

                if (temp.Any(x => x.DistanceTo(center) > clumpSize))
                {
                    decimated.Add(temp);
                }
            }

            return decimated.ToArray();
        }

        private void UpdatePreview()
        {
            if (currentMode == Mode.Video)
            {
                video.Set(VideoCaptureProperties.PosFrames, trackBar1.Value);
            }

            Point[][] curves = ComputeCurves();

            Mat preview = Mat.Zeros(sSize.Height, sSize.Width, MatType.CV_8UC1);
            preview.DrawContours(curves, -1, new Scalar(255, 255, 0, 255));

            label11.Text = "line count: " + curves.Length;
            label12.Text = "point count: " + curves.Sum(x => x.Length);
            Preview.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(preview);
        }

        private Point[] Decimate(Point[] curve) //RDP Algorithm
        {
            double dmax = 0;
            int index = 0;

            for (int i = curve.Length - 1; i > 0; i--)
            {
                double d = PerpendicularDistance(curve[i], curve[0], curve[^1]);
                if (d > dmax)
                {
                    index = i;
                    dmax = d;
                }
            }

            if (dmax > epsilon)
            {
                return Decimate(curve[..index]).Concat(Decimate(curve[index..])).ToArray();
            }
            else
            {
                return new Point[] { curve[0], curve[^1] };
            }
        }

        private double PerpendicularDistance(Point a, Point b, Point c)
        {
            double A = b.X - c.X;
            double B = b.Y - c.Y;
            return Math.Abs(A * (b.Y - a.Y) + B * (a.X - b.X)) / Math.Sqrt(A * A + B * B);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (currentMode == Mode.None) return;
            try
            {
                if (currentMode == Mode.Image)
                {
                    string fileName = @"output.txt";

                    for (int i = 1; File.Exists(fileName); i++)
                    {
                        fileName = @$"output ({i}).txt";
                    }

                    OutputImage(fileName);
                }
                else if (currentMode == Mode.Video)
                {
                    string dirName = @"output";

                    for (int i = 1; Directory.Exists(dirName); i++)
                    {
                        dirName = @$"output ({i})";
                    }

                    OutputVideo(dirName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed");
            }
            finally
            {
                MessageBox.Show("OK", "Success");
            }
        }

        private void OutputImage(string fileName)
        {
            Point[][] curves = ComputeCurves();

            using (StreamWriter sw = File.CreateText(fileName))
            {
                sw.WriteLine("calculator.setExpressions([");

                foreach (var curve in curves)
                {
                    string xValues = "";
                    string yValues = "";

                    foreach (var point in curve)
                    {
                        xValues += $"'{point.X}', ";
                        yValues += $"'{sSize.Height - point.Y}', ";
                    }

                    sw.WriteLine($"{{type: 'table', columns: [{{latex: 'x', values: [{xValues}]}}, {{latex: 'y', values: [{yValues}], color: '#000000', points: false, lines: true}}]}}, ");
                }

                sw.WriteLine("])");
            }
        }

        private void OutputVideo(string dirName)
        {
            video.Set(VideoCaptureProperties.PosFrames, 0);
            Directory.CreateDirectory(dirName);
            List<int> counts = new();

            for (int i = 1; i < video.FrameCount; i++)
            {
                Point[][] curves = ComputeCurves();
                counts.Add(curves.Length);

                using (StreamWriter sw = File.CreateText(@$"{dirName}/{i}.txt"))
                {
                    sw.WriteLine("calculator.setExpressions([");

                    for (int j = 0; j < counts[^1]; j++)
                    {
                        string xValues = "";
                        string yValues = "";

                        foreach (var point in curves[j])
                        {
                            xValues += $"'{point.X}', ";
                            yValues += $"'{sSize.Height - point.Y}', ";
                        }
                        sw.WriteLine($"{{id: {j}, type: 'table', columns: [{{latex: 'x', values: [{xValues}]}}, {{latex: 'y', values: [{yValues}]}}]}}, ");
                    }
                }
            }

            int max = counts.Max();

            using (StreamWriter sw = File.CreateText(@$"{dirName}/0.txt"))
            {
                sw.WriteLine("calculator.setExpressions([");

                for (int i = 0; i < max; i++)
                {
                    sw.WriteLine($"{{id: {i}, type: 'table', columns: [{{latex: 'x', values: []}}, {{latex: 'y', values: [], color: '#000000', points: false, lines: true}}]}}, ");
                }

                sw.WriteLine("])");
            }

            for (int i = 1; i < video.FrameCount; i++)
            {
                using (StreamWriter sw = File.AppendText(@$"{dirName}/{i}.txt"))
                {
                    if (counts[i - 1] < max)
                    {
                        for (int j = counts[i - 1]; j < max; j++)
                        {
                            sw.WriteLine($"{{id: {j}, type: 'table', columns: [{{latex: 'x', values: []}}, {{latex: 'y', values: []}}]}}, ");
                        }
                    }

                    sw.WriteLine("])");
                }
            }
        }

        enum Mode
        {
            Video,
            Image,
            None
        }
    }
}
