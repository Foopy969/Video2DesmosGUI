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
        int minLength = 0;

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
                label9.Text = "frame count: 0";
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
            minLength = (int)numericUpDown6.Value;
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
                if (contour.Length > minLength)
                    decimated.Add(Decimate(contour));
            }

            return decimated.ToArray();
        }

        private void UpdatePreview()
        {
            if (currentMode == Mode.Video)
            {
                video.Set(VideoCaptureProperties.PosFrames, trackBar1.Value);
            }

            Mat preview = Mat.Zeros(sSize.Height, sSize.Width, MatType.CV_8UC1);
            preview.DrawContours(ComputeCurves(), -1, new Scalar(255, 255, 0, 255));
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

        private string GenerateCommand(Point[] contours, int id)
        {
            string xlist = "calculator.setExpression({id: '" + id + "', type: 'table', columns: [{latex: 'x', values: [";
            string ylist = "]}, {latex: 'y', values: [";

            foreach (var contour in contours)
            {
                xlist += $"'{contour.X}', ";
                ylist += $"'{sSize.Height - contour.Y}', ";
            }

            return xlist + ylist + "], color: \"#000000\", points: false, lines: true}]})";
        }

        private string GenerateEmpty(int id)
        {
            return "calculator.setExpression({ id: " + id + ", type: 'table', columns: [{latex: ' '}]})";
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
                        fileName = @$"output{i}.txt";
                    }
                    OutputToFile(fileName);
                }
                else if (currentMode == Mode.Video)
                {
                    string dirName = @"output";
                    for (int i = 1; Directory.Exists(dirName); i++)
                    {
                        dirName = @$"output{i}";
                    }
                    Directory.CreateDirectory(dirName);

                    int cmax = 0;

                    video.Set(VideoCaptureProperties.PosFrames, 0);
                    for (int i = 0; i < video.FrameCount - 1; i++)
                    {
                        int c = ComputeCurves().Length;
                        if (c > cmax)
                        {
                            cmax = c;
                        }
                    }

                    video.Set(VideoCaptureProperties.PosFrames, 0);
                    for (int i = 0; i < video.FrameCount - 1; i++)
                    {
                        OutputToFile(@$"{dirName}/{i}.txt", cmax);
                    }
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

        private void OutputToFile(string fileName, int max = 0)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(fileName)))
            {
                Point[][] curves = ComputeCurves();
                for (int i = 0; i < curves.Length; i++)
                {
                    sw.WriteLine(GenerateCommand(curves[i], i));
                }

                if (max > curves.Length)
                {
                    for (int i = curves.Length; i < max; i++)
                    {
                        sw.WriteLine(GenerateEmpty(i));
                    }
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
