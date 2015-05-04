using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using System.Drawing.Imaging;
using System.Reflection;
using Vlc.DotNet.Forms;
using Vlc.DotNet.Core;

namespace DeteksiKendaraan
{
    public partial class Form1 : Form
    {
        /* variable untuk menyimpan object bitmap dari setiap proses */
        Bitmap originalImage;
        Bitmap segementImage;
        Bitmap cannyImage;
        Bitmap roiImage;
        Bitmap morfologi;
        
        /* Object line untuk menentukan garis tepi jalan yg digunakan pada proses hough transform(hough line) 
         * Class : PointLine.cs
         */
        PointLine leftLine = null;
        PointLine rightLine = null;

        int TotalPixel = 0;
        int SleepTime = 0;
        System.Threading.Thread ThreadProcess;
        bool isAlive = false;

        /* variable filter Connected Component Labeling */
        ConnectedComponentsLabeling cclFilter;

        // binarization filtering sequence
        // filter untuk grayscale
        private FiltersSequence filter = new FiltersSequence(
            Grayscale.CommonAlgorithms.BT709,
            new Threshold(64)
        );

        // filter untuk morfology
        private FiltersSequence morfologiFilter = new FiltersSequence(
            new Opening(),
            new Closing()
        );

        public Form1()
        {
            InitializeComponent();

            // inisialisasi VLC Component pada form aplikasi sebagai video player
            Assembly assembly = this.GetType().Assembly;
            Vlc.DotNet.Core.Medias.MediaBase media = new Vlc.DotNet.Core.Medias.PathMedia(System.IO.Path.GetDirectoryName(assembly.Location) + @"\data\20140822_083239.mp4");
            videoPlayer.Media = media;
            videoPlayer.Stop();

            // inisialisasi thread jika ingin melakukan proses secara automatis.
            ThreadProcess = new System.Threading.Thread(new System.Threading.ThreadStart(DeteksiKendaraanThread));
            ThreadProcess.IsBackground = true;
        }

        // Form load event
        private void Form1_Load(object sender, EventArgs e)
        {
            // inisialisasi ROI image
            Assembly assembly = this.GetType().Assembly;
            Bitmap image = new Bitmap(System.IO.Path.GetDirectoryName(assembly.Location) + "\\data\\ROI-Video.png");
            DeteksiJalan(image); // method untuk melakukan deteksi jalan dari image ROI
            if (videoPlayer.IsPlaying)
                videoPlayer.Stop();

            // menghapus temporary file pada proses simulasi sebelumnya
            string path = System.IO.Path.GetDirectoryName(assembly.Location) + "\\temp\\";
            DirectoryInfo dic = new DirectoryInfo(path);
            if (!dic.Exists)
            {
                dic.Create();
            }
            else
            {
                foreach (FileInfo file in dic.GetFiles())
                {
                    file.Delete();
                }
            }
        }

        private void DeteksiJalan(Bitmap roi)
        {
            originalImage = roi; // original bitmap dari ROI image
            segementImage = Segmentasi(originalImage); // melakukan segmentasi dan menyimpannya dalam bitmap variabel

            /* Hasil sementasi kemudian akan mengalami proses deteksi tepi canny
             * Inisialisasi filter canny 
             * Pertama hasil segementasi di grayscale kemudian di filter dengan filter canny
             */
            AForge.Imaging.Filters.CannyEdgeDetector cannyFilter = new AForge.Imaging.Filters.CannyEdgeDetector();
            cannyImage = cannyFilter.Apply(AForge.Imaging.Filters.Grayscale.CommonAlgorithms.BT709.Apply(segementImage)); // hasil canny disimpan kedalam variable bitmap

            /* Inisialisasi HoughLineTransform untuk proses Hough Transform
             * Hasil dari canny akan diproses oleh fungsi HoughTransformation
             */
            HoughLineTransformation linetransform = new HoughLineTransformation();
            roiImage = HoughTransformation(cannyImage, originalImage);

            // menampilkan semua bitmap hasil proses deteksi tepi jalan
            pictureBox1.Image = originalImage;
            pictureBox2.Image = segementImage;
            pictureBox3.Image = cannyImage;
            pictureBox5.Image = roiImage;
        }

        /* fungsi untuk melakukan segemetasi bitmap object
         * batas keabu-abuan pixel jalan 
         * 65 <= RED <= 190
         * 70 <= GREEN <= 190
         * 80 <= BLUE <= 190
         * Didalam batas pixel diatas akan diubah menjadi pixel black, selainnya menjadi pixel white
         */
        private Bitmap Segmentasi(Bitmap original)
        {
            Bitmap temp = (Bitmap)original.Clone();
            Color c;

            for (int i = 0; i < temp.Width; i++)
            {
                for (int j = 0; j < temp.Height; j++)
                {
                    c = temp.GetPixel(i, j);
                    if ((c.R >= 65 && c.R <= 190) && (c.G >= 70 && c.G <= 190) && (c.B >= 80 && c.B <= 190))
                    {
                        temp.SetPixel(i, j, Color.Black);
                    }
                    else
                    {
                        temp.SetPixel(i, j, Color.White);
                    }
                }
            }

            return temp;
        }

        /*
         * Fungsi untuk melakukan proses hough transform menggunakan hough filter pada AForge.NET library
         */
        private Bitmap HoughTransformation(Bitmap image, Bitmap originalImage)
        {
            HoughLineTransformation houghLineTransform = new HoughLineTransformation();

            Bitmap temp = AForge.Imaging.Image.Clone(image, PixelFormat.Format24bppRgb);
            Bitmap clone = (Bitmap)temp.Clone();
            /// lock the source image
            BitmapData sourceData = temp.LockBits(
                new Rectangle(0, 0, temp.Width, temp.Height),
                ImageLockMode.ReadOnly, temp.PixelFormat);
            // binarize the image
            UnmanagedImage binarySource = filter.Apply(new UnmanagedImage(sourceData));
            BitmapData cloneSourceData = clone.LockBits(
                new Rectangle(0, 0, clone.Width, clone.Height),
                ImageLockMode.ReadOnly, clone.PixelFormat);
            // apply Hough line transofrm
            houghLineTransform.ProcessImage(binarySource);

            // get lines using relative intensity
            HoughLine[] lines = houghLineTransform.GetLinesByRelativeIntensity(0.5);

            foreach (HoughLine line in lines)
            {

                if (line.Theta > 50 && line.Theta < 130)
                {
                    continue;
                }

                string s = string.Format("Theta = {0}, R = {1}, I = {2} ({3})", line.Theta, line.Radius, line.Intensity, line.RelativeIntensity);
                System.Diagnostics.Debug.WriteLine(s);

                // uncomment to highlight detected lines

                // get line's radius and theta values
                int r = line.Radius;
                double t = line.Theta;

                // check if line is in lower part of the image
                if (r < 0)
                {
                    t += 180;
                    r = -r;
                }

                // convert degrees to radians
                t = (t / 180) * Math.PI;

                // get image centers (all coordinate are measured relative
                // to center)
                int w2 = image.Width / 2;
                int h2 = image.Height / 2;

                double x0 = 0, x1 = 0, y0 = 0, y1 = 0;

                if (line.Theta != 0)
                {
                    // none vertical line
                    x0 = -w2; // most left point
                    x1 = w2;  // most right point

                    // calculate corresponding y values
                    y0 = (-Math.Cos(t) * x0 + r) / Math.Sin(t);
                    y1 = (-Math.Cos(t) * x1 + r) / Math.Sin(t);
                }
                else
                {
                    // vertical line
                    x0 = line.Radius;
                    x1 = line.Radius;

                    y0 = h2;
                    y1 = -h2;
                }

                // draw line on the image
                if (leftLine == null || rightLine == null)
                {
                    // menentukan garis tepi yang digunakan pada jalan
                    if (line.Theta >= 25 && line.Theta < 36) // garis tepi kiri jalan
                    {
                        if (leftLine != null) continue;
                        leftLine = new PointLine();
                        leftLine.Point1 = new IntPoint((int)x0 + w2, h2 - (int)y0);
                        leftLine.Point2 = new IntPoint((int)x1 + w2, h2 - (int)y1);

                        drawLine(ref sourceData, x0, x1, y0, y1, h2, w2);
                    }
                    else if (line.Theta > 130) // garis tepi kanan jalan
                    {
                        if (rightLine != null) continue;
                        rightLine = new PointLine();
                        rightLine.Point1 = new IntPoint((int)x0 + w2, h2 - (int)y0 + 10);
                        rightLine.Point2 = new IntPoint((int)x1 + w2, h2 - (int)y1);

                        drawLine(ref sourceData, x0, x1, y0, y1, h2, w2);
                    }
                }
                drawLine(ref cloneSourceData, x0, x1, y0, y1, h2, w2);
            }

            System.Diagnostics.Debug.WriteLine("Found lines: " + houghLineTransform.LinesCount);
            System.Diagnostics.Debug.WriteLine("Max intensity: " + houghLineTransform.MaxIntensity);
            System.Diagnostics.Debug.WriteLine("Houghline: " + lines.Length);

            // menghitung banyaknya pixel yang manjadi daerah jalan dan mengextraki daerah jalan yang digunakan
            Bitmap roi = (Bitmap)originalImage.Clone();
            for (int x = 0; x < roi.Width; x++)
            {
                for (int y = 0; y < roi.Height; y++)
                {
                    if (((y * (leftLine.Point2.X - leftLine.Point1.X) - x * (leftLine.Point2.Y - leftLine.Point1.Y)) >
                        (leftLine.Point1.Y * (leftLine.Point2.X - leftLine.Point1.X) - leftLine.Point1.X * (leftLine.Point2.Y - leftLine.Point1.Y))) &&
                        (y * (rightLine.Point2.X - rightLine.Point1.X) - x * (rightLine.Point2.Y - rightLine.Point1.Y)) >
                        (rightLine.Point1.Y * (rightLine.Point2.X - rightLine.Point1.X) - rightLine.Point1.X * (rightLine.Point2.Y - rightLine.Point1.Y)))
                    {
                        // nothing
                        TotalPixel++;
                    }
                    else
                    {
                        roi.SetPixel(x, y, Color.Black);
                    }
                }
            }

            // unlock source image
            temp.UnlockBits(sourceData);
            // dispose temporary binary source image
            binarySource.Dispose();
            pictureBox4.Image = temp;

            clone.UnlockBits(cloneSourceData);
            pictureBox11.Image = clone;


            return roi;
        }

        private void drawLine(ref BitmapData sourceData, double x0, double x1, double y0, double y1, int h2, int w2)
        {
            Drawing.Line(sourceData,
                        new IntPoint((int)x0 + w2, h2 - (int)y0),
                        new IntPoint((int)x1 + w2, h2 - (int)y1),
                        Color.Red);
            System.Diagnostics.Debug.WriteLine(String.Format("Point ({0},{1}),({2},{3})", (int)x0 + w2, h2 - (int)y0, (int)x1 + w2, h2 - (int)y1));
        }

        /*
         * Click event pada button Mulai simulasi
         * Video player akan diputar
         */
        private void btnDeteksi_Click(object sender, EventArgs e)
        {
            if (rdbManual.Checked) //Deteksi kendaraan dengan pengambilan gambar secara manual
            {
                btnBerhenti.Enabled = true;
                btnCapture.Enabled = true;
                btnDeteksi.Enabled = false;
                videoPlayer.Play();
                SleepTime = 0;
            }
            else if (rdoAutomatis.Checked) //Deteksi kendaraan dengan pengambilan gambar secara automatis
            {
                btnBerhenti.Enabled = true;
                btnDeteksi.Enabled = false;
                videoPlayer.Play();

                // melakukan inisialisasi Thread untuk melakukan automatis pengambilan gambar
                // pengambilan gambar dilakukan setiap 3 detik
                SleepTime = 3000;
                isAlive = true;
                if (!ThreadProcess.IsAlive)
                    ThreadProcess.Start();
            }
            openFileToolStripMenuItem.Enabled = false;
        }

        /*
         * Click event pada button Ambil Gambar
         * Proses manual
         */
        private void btnCapture_Click(object sender, EventArgs e)
        {
            DeteksiKendaraan();
        }

        /*
         * Fungsi yang menjadi Thread Process automatis pengambilan gambar
         */
        private void DeteksiKendaraanThread()
        {
            while (true)
            {
                if (isAlive)
                {
                    DeteksiKendaraan();
                }
            }
        }

        /*
         * Fungsi untuk melakukan Deteksi kendaraan
         * akan dilakukan pengambilan gambar dari video yang diputar oleh player.
         * Gambar tersebut akan dijadikan bitmap yang kemudian akan diproses.
         */
        private void DeteksiKendaraan(string filename = "")
        {
            if (filename == "")
            {
                System.Threading.Thread.Sleep(SleepTime);
                Assembly assembly = this.GetType().Assembly;
                string path = System.IO.Path.GetDirectoryName(assembly.Location) + "\\temp\\";

                // melakukan pengambilan gambar pada video yang diputar
                // file akan disimpan pada folder temp
                filename = path + DateTime.Now.ToString("yyyy-MM-dd HH mm ss") + ".png";
                videoPlayer.TakeSnapshot(filename, 450, 300);
                System.Threading.Thread.Sleep(1000);
            }

            /* citra uji */
            Bitmap citraUji = (Bitmap)Bitmap.FromFile(filename);
            // melelakukan proses subtraksi antara bitmap deteksi jalan 
            // dengan citra hasil pengambilan dari video
            Bitmap subtraksi = Substraksi(roiImage, citraUji); 
            Bitmap filterGT = filter.Apply(subtraksi);

            morfologi = new Opening().Apply(new Closing().Apply(filterGT));

            // melakukan proses ConnectedComponentsLabeling
            cclFilter = new ConnectedComponentsLabeling();
            cclFilter.MinHeight = 50000;
            cclFilter.MinWidth = 50000;
            Bitmap ccl = cclFilter.Apply(morfologi);

            Console.WriteLine(cclFilter.ObjectCount);

            // menampilkan citra hasil deteksi
            pictureBox6.Image = citraUji;
            pictureBox7.Image = subtraksi;
            pictureBox8.Image = filterGT;
            pictureBox9.Image = morfologi;
            pictureBox10.Image = ccl;
            // dilakukan penghitungan dan deteksi kendaraan 
            // pada saat event Paint Component PictureBox
            pctResult.Image = citraUji; 
        }

        /*
         * Fungsi untuk melakukan subtraksi hasil bitmap deteksi jalan dengan bitmap 
         * yang akan diuji/diproses untuk mendeteksi kendaraan
         * Pada proses subtraksi, yang dilakukan adalah proses pengurangan antara dua citra
         * yaitu citra ROI hasil deteksi jalan dengan citra uji
         */
        public Bitmap Substraksi(Bitmap roiImage, Bitmap ujiImage)
        {
            Bitmap temp = (Bitmap)ujiImage.Clone();
            for (int y = 0; y < roiImage.Height; y++)
            {
                for (int x = 0; x < roiImage.Width; x++)
                {
                    Color roiPixel = roiImage.GetPixel(x, y);
                    Color ujiPixel = ujiImage.GetPixel(x, y);
                    Color newColor;
                    // pengurangan pixel RGB
                    int red = roiPixel.R - ujiPixel.R;
                    int green = roiPixel.G - ujiPixel.G;
                    int blue = roiPixel.B - ujiPixel.B;

                    // pengurangan citra hanya akan dilakukan jika posisi pixel pada daerah jalan hasil deteksi jalan
                    if (((y * (leftLine.Point2.X - leftLine.Point1.X) - x * (leftLine.Point2.Y - leftLine.Point1.Y)) >
                        (leftLine.Point1.Y * (leftLine.Point2.X - leftLine.Point1.X) - leftLine.Point1.X * (leftLine.Point2.Y - leftLine.Point1.Y))) &&
                        (y * (rightLine.Point2.X - rightLine.Point1.X) - x * (rightLine.Point2.Y - rightLine.Point1.Y)) >
                        (rightLine.Point1.Y * (rightLine.Point2.X - rightLine.Point1.X) - rightLine.Point1.X * (rightLine.Point2.Y - rightLine.Point1.Y)))
                    {
                        if (red < 0 && red >= -50)
                        {
                            red = 0;
                        }
                        else
                        {
                            red = Math.Abs(red) + 30;
                            red = (red > 255) ? 255 : red;
                        }

                        if (green < 0 && green >= -50)
                        {
                            green = 0;
                        }
                        else
                        {
                            green = Math.Abs(green) + 30;
                            green = (green > 255) ? 255 : green;
                        }

                        if (blue < 0 && blue >= -50)
                        {
                            blue = 0;
                        }
                        else
                        {
                            blue = Math.Abs(blue) + 30;
                            blue = (blue > 255) ? 255 : blue;
                        }

                        newColor = Color.FromArgb(red, green, blue);

                        temp.SetPixel(x, y, newColor);
                    }
                    else // jika tidak pada daerah jalan, pixel diset black
                    {
                        temp.SetPixel(x, y, Color.Black);
                    }
                }
            }

            return temp;
        }

        /*
         * Event untuk menampilkan hasil deteksi kendaraan
         * Pada saat menampilkan citra akan dilakukan proses evaluasi dan penghitugan kendaraan
         */
        private void pctResult_Paint(object sender, PaintEventArgs e)
        {
            if (morfologi != null)
            {
                BlobCounter bc = (BlobCounter)cclFilter.BlobCounter;
                //bc.ProcessImage(morfologi);
                Rectangle[] rects = bc.GetObjectsRectangles();
                List<Rectangle> evaluation = new List<Rectangle>();
                int jumlah = 0;

                /*
                 * Evaluasi pertama hasil deteksi
                 * menghapus hasil deteksi yang saling bertindih
                 */
                foreach (Rectangle rect in rects)
                {
                    if (rect.Width > txtMinWidth.Value && rect.Height > txtMinHeight.Value)
                    {
                        bool cek1 = false;
                        bool cek2 = true;
                        int index = 0;
                        foreach (Rectangle recEva in evaluation)
                        {
                            if (recEva.Contains(rect))
                            {
                                cek2 = false;
                                break;
                            }
                            if (rect.Contains(recEva))
                            {
                                cek1 = true;
                                cek2 = false;
                                break;
                            }
                            index++;
                        }

                        if (cek1)
                        {
                            evaluation[index] = rect;
                        }
                        else if (cek2)
                        {
                            evaluation.Add(rect);
                        }
                    }
                    //evaluation.Add(rect);
                }

                /*
                 * Evaluasi kedua hasil deteksi
                 * Menggabungkan hasil deteksi yang berdekatan
                 */
                evaluation = EvaluationNear(evaluation);

                // menampilkan dan menghitung kendaraan hasil deteksi
                int pixelKendaraan = 0;
                foreach (Rectangle rect in evaluation)
                {
                    Pen pen = new Pen(Color.Red, 2);
                    float X = (float)pctResult.Width / 450;
                    float Y = (float)pctResult.Height / 300;
                    Rectangle newRec = new Rectangle((int)Math.Ceiling(rect.X * X), (int)Math.Ceiling(rect.Y * Y),
                        (int)Math.Ceiling(rect.Width * X), (int)Math.Ceiling(rect.Height * Y));
                    e.Graphics.DrawRectangle(pen, newRec);
                    pixelKendaraan += (newRec.Width * newRec.Height);
                    jumlah++;
                }
                lblJumlahKendaraan.Text = jumlah.ToString();
                Console.WriteLine(TotalPixel);
                Console.WriteLine(pixelKendaraan);
                float percentage = float.Parse(Math.Round(((decimal)pixelKendaraan / TotalPixel) * 100, 2).ToString());
                lblKepadatan.Text = String.Format("{0} %",percentage);

                // melakukan perhitungan fuzzy dari hasil deteksi kendaraan
                FuzzyObject fuzzy = new FuzzyObject();
                lblSepi.Text = Math.Round(fuzzy.lvKepadatanJalan.GetLabelMembership("Sepi", percentage), 2).ToString();
                lblSedang.Text = Math.Round(fuzzy.lvKepadatanJalan.GetLabelMembership("Sedang", percentage),2).ToString();
                lblPadat.Text = Math.Round(fuzzy.lvKepadatanJalan.GetLabelMembership("Padat", percentage), 2).ToString();
            }
        }

        /*
         * Fungsi untuk melakukan evaluasi hasil deteksi
         * Hasil deteksi yang saling berdekatan dengan jarak citra tertentu (5 pixel)
         * akan digabungkan menjadi hasil hasil deteksi
         */
        private List<Rectangle> EvaluationNear(List<Rectangle> rect)
        {
            List<Rectangle> result = new List<Rectangle>();
            int c = 5;

            foreach (Rectangle item1 in rect)
            {
                bool add = true;
                bool check1 = false;
                int index = 0;

                foreach (Rectangle item2 in result)
                {

                    //check kanan dari object yg sudah disimpan
                    if (item2.X < item1.X)
                    {
                        if (new Rectangle(item2.X - c, item2.Y - c, item2.Width + 2*c, item2.Height + 2*c).Contains(item1.X, item1.Y))
                        {
                            add = false;
                            check1 = true;
                            break;
                        }
                    }
                    else
                    {
                        if( new Rectangle(item1.X - c, item1.Y - c, item1.Width + c, item1.Height+c).Contains(item2.X, item2.Y + item2.Height))
                        {
                            add = false;
                            check1 = true;
                            break;
                        }
                    }
                    index++;
                }

                if (check1)
                {
                    Rectangle temp = new Rectangle(result[index].X, result[index].Y, (item1.X - result[index].X) + item1.Width, (item1.Y - result[index].Y) + item1.Height);
                    result[index] = temp;
                }
                else if (add)
                {
                    result.Add(item1);
                }

            }

            return result;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AboutForm about = new AboutForm();
            about.ShowDialog();
        }

        private void btnBerhenti_Click(object sender, EventArgs e)
        {
            btnDeteksi.Enabled = true;
            btnCapture.Enabled = false;
            btnBerhenti.Enabled = false;
            isAlive = false;
            openFileToolStripMenuItem.Enabled = true;
            videoPlayer.Pause();
        }

        private void fuzzyMembershipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FuzzyForm fuzzyForm = new FuzzyForm();
            fuzzyForm.ShowDialog();
        }


        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            DeteksiKendaraan(openFileDialog1.FileName);
        }

        private void btnSaveFile_Click(object sender, EventArgs e)
        {
            //string path = "D:\\Kerjaan\\Skripsi\\Ari Usman\\Bahan Penelitian\\Gatot Subroto\\compress\\true\\";
            //FileInfo fileInfo = new FileInfo(openFileDialog1.FileName);
            //if (!File.Exists(path + fileInfo.Name))
            //{
            //    System.Drawing.Image img = System.Drawing.Image.FromFile(fileInfo.FullName);
            //    Bitmap result = new Bitmap(450, 253);
            //    result.SetResolution(img.HorizontalResolution, img.VerticalResolution);
            //    using (Graphics g = Graphics.FromImage(result))
            //    {
            //        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            //        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            //        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            //        g.DrawImage(img, 0, 0, result.Width, result.Height);
            //    }

            //    result.Save(path + fileInfo.Name, ImageFormat.Jpeg);
            //}
            //MessageBox.Show("Success", "Information", MessageBoxButtons.OK);
        }

        private void changeResolution()
        {
            string path = "D:\\Kerjaan\\Skripsi\\Ari Usman\\Bahan Penelitian\\Gatot Subroto";
            List<string> filename = new List<string>();
            DirectoryInfo decInfo = new DirectoryInfo(path);
            foreach (FileInfo file in decInfo.GetFiles())
            {
                if (file.Extension == ".jpg")
                {
                    if (File.Exists(path + "\\compress\\" + file.Name))
                        continue;

                    System.Drawing.Image img = System.Drawing.Image.FromFile(file.FullName);
                    Bitmap result = new Bitmap(450, 253);
                    result.SetResolution(img.HorizontalResolution, img.VerticalResolution);
                    using (Graphics g = Graphics.FromImage(result))
                    {
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.DrawImage(img, 0, 0, result.Width, result.Height);
                    }

                    result.Save(path + "\\compress\\" + file.Name, ImageFormat.Jpeg);
                }
            }
        }
    }
}
