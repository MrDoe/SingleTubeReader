using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Media;
using Touchless.Vision.Camera;
using DataMatrix.net;
using System.Drawing.Imaging;
using System.Threading;
using System.Reflection;
//testtest
namespace SingleTubeReader
{
    public partial class MainForm : Form
    {
        Thread t;
        long isRun = 0L;
        string lastBarcode = "";
        object lastBC_lock = new object();
        bool init = false;

        public MainForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (!DesignMode)
            {
                // Refresh the list of available cameras
                comboBoxCameras.Items.Clear();
                int i = 0;
                int nScanCam = 0;
                foreach (Camera cam in CameraService.AvailableCameras)
                {
                    comboBoxCameras.Items.Add(cam);
                    if (cam.Name == "USB2.0 PC CAMERA")
                        nScanCam = i;
                    ++i;
                }

                if (comboBoxCameras.Items.Count > 0)
                {
                    comboBoxCameras.SelectedIndex = nScanCam;
                    btnStart_Click(sender, e);
                    StartStopThread();
                }
                init = true;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StartStopThread();
            thrashOldCamera();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StartStopThread();
            thrashOldCamera();
        }

        private CameraFrameSource _frameSource;
        private static Bitmap _latestFrame;
        
        private Camera CurrentCamera
        {
            get
            {
                return comboBoxCameras.SelectedItem as Camera;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            // Early return if we've selected the current camera
            if (_frameSource != null && _frameSource.Camera == comboBoxCameras.SelectedItem)
                return;
            thrashOldCamera();
            startCapturing();
        }

        private void startCapturing()
        {
            try
            {
                Camera c = (Camera)comboBoxCameras.SelectedItem;
                setFrameSource(new CameraFrameSource(c));
                _frameSource.Camera.CaptureWidth = 640;
                _frameSource.Camera.CaptureHeight = 480;
                _frameSource.Camera.Fps = 50;
                _frameSource.NewFrame += OnImageCaptured;

                if(chkPreview.Checked)
                    pictureBoxDisplay.Paint += new PaintEventHandler(drawLatestImage);

                _frameSource.StartFrameCapture();
            }
            catch (Exception ex)
            {
                comboBoxCameras.Text = "Select A Camera";
                MessageBox.Show(ex.Message);
            }
        }

        private void drawLatestImage(object sender, PaintEventArgs e)
        {
            if (_latestFrame != null)
            {
                // Draw the latest image from the active camera
                //_latestFrame = ResizeImage(_latestFrame, new Size(320, 240));
                e.Graphics.DrawImage(_latestFrame, 0, 0, _latestFrame.Width, _latestFrame.Height);
            }
        }

        public void OnImageCaptured(Touchless.Vision.Contracts.IFrameSource frameSource, Touchless.Vision.Contracts.Frame frame, double fps)
        {
            _latestFrame = frame.Image;

            if (chkPreview.Checked)
                pictureBoxDisplay.Invalidate();
        }

        private void setFrameSource(CameraFrameSource cameraFrameSource)
        {
            if (_frameSource == cameraFrameSource)
                return;

            _frameSource = cameraFrameSource;
        }

        //

        private void thrashOldCamera()
        {
            // Trash the old camera
            if (_frameSource != null)
            {
                _frameSource.NewFrame -= OnImageCaptured;
                _frameSource.Camera.Dispose();
                setFrameSource(null);

                if (chkPreview.Checked)
                    pictureBoxDisplay.Paint -= new PaintEventHandler(drawLatestImage);
            }
        }

        //

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (_frameSource == null)
                return;

            Bitmap current = (Bitmap)_latestFrame.Clone();
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "*.png|*.png";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    current.Save(sfd.FileName);
                }
            }

            current.Dispose();
        }

        private void btnConfig_Click(object sender, EventArgs e)
        {
            // snap camera
            if (_frameSource != null)
                _frameSource.Camera.ShowPropertiesDialog();
        }

        #region Camera Property Controls
        private IDictionary<String, CameraProperty> displayPropertyValues;

        private IDictionary<String, CameraProperty> DisplayPropertyValues
        {
            get
            {
                if (displayPropertyValues == null)
                    displayPropertyValues = new Dictionary<String, CameraProperty>()
                 {
                    { "Pan (Degrees)", CameraProperty.Pan_degrees },
                    { "Tilt (Degrees)", CameraProperty.Tilt_degrees },
                    { "Roll (Degrees)", CameraProperty.Roll_degrees },
                    { "Zoom (mm)", CameraProperty.Zoom_mm },
                    { "Exposure (log2(seconds))", CameraProperty.Exposure_lgSec },
                    { "Iris (10f)", CameraProperty.Iris_10f },
                    { "Focal Length (mm)", CameraProperty.FocalLength_mm },
                    { "Flash", CameraProperty.Flash },
                    { "Brightness", CameraProperty.Brightness },
                    { "Contrast", CameraProperty.Contrast },
                    { "Hue", CameraProperty.Hue },
                    { "Saturation", CameraProperty.Saturation },
                    { "Sharpness", CameraProperty.Sharpness },
                    { "Gamma", CameraProperty.Gamma },
                    { "Color Enable", CameraProperty.ColorEnable },
                    { "White Balance", CameraProperty.WhiteBalance },
                    { "Backlight Compensation", CameraProperty.BacklightCompensation },
                    { "Gain", CameraProperty.Gain },
                 };

                return displayPropertyValues;
            }
        }

        private IDictionary<CameraProperty, CameraPropertyCapabilities> CurrentCameraPropertyCapabilities
        {
            get;
            set;
        }

        private IDictionary<CameraProperty, CameraPropertyRange> CurrentCameraPropertyRanges
        {
            get;
            set;
        }

        private Boolean IsSelectedCameraPropertySupported
        {
            get;
            set;
        }

        private Boolean SuppressCameraPropertyValueValueChangedEvent
        {
            get;
            set;
        }

        private Boolean CameraPropertyControlInitializationComplete
        {
            get;
            set;
        }



        #endregion

        private void SystemBell()
        {
            SystemSounds.Asterisk.Play();
        }

        private delegate void SetControlPropertyThreadSafeDelegate(
           Control control,
           string propertyName,
           object propertyValue);

        public static void SetControlPropertyThreadSafe(
            Control control,
            string propertyName,
            object propertyValue)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new SetControlPropertyThreadSafeDelegate
                (SetControlPropertyThreadSafe),
                new object[] { control, propertyName, propertyValue });
            }
            else
            {
                control.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.SetProperty,
                    null,
                    control,
                    new object[] { propertyValue });
            }
        }

        //public static Bitmap ResizeImage(Bitmap imgToResize, Size size)
        //{
        //    try
        //    {
        //        Bitmap b = new Bitmap(size.Width, size.Height);
        //        using (Graphics g = Graphics.FromImage((Image)b))
        //        {
        //            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
        //            g.DrawImage(imgToResize,
        //                        new Rectangle(0, 0, size.Width, size.Height),
        //                        new Rectangle(64, 48, 448, 336),
        //                        GraphicsUnit.Pixel);
        //        }
        //        return b;
        //    }
        //    catch
        //    {
        //        Console.WriteLine("Bitmap could not be resized");
        //        return imgToResize;
        //    }
        //}

        private string Decode()
        {
            DmtxImageDecoder decoder = new DmtxImageDecoder();
         
            string code = "";

            try
            {
                //Bitmap current = ResizeImage((Bitmap)_latestFrame.Clone(), new Size(320, 240));
                Bitmap current = (Bitmap)_latestFrame.Clone();
                List<string> codes = decoder.DecodeImage(current, 1, new TimeSpan(0, 0, 0, 0, 800));

                if (codes.Count > 0)
                    code = codes[0] + "\r\n";
            }
            catch { }
 
            return code;
        }

        private void DecodeThread()
        {
            string _out = "";

            do
            {
                _out = Decode();

                if (lastBarcode != _out && _out.Length > 1)
                {
                    Monitor.Enter(lastBC_lock);
                    lastBarcode = _out;
                    Monitor.Exit(lastBC_lock);

                    // thread-safe equivalent of myLabel.Text = status;
                    SetControlPropertyThreadSafe(txtDecode, "Text", lastBarcode);
                    SystemBell();
                    SendKeys.SendWait(lastBarcode);
                }

                Thread.Sleep(500);
            }
            while (Interlocked.Read(ref isRun) == 1L);
        }

        private void StartStopThread()
        {
            if (Interlocked.Read(ref isRun) == 0L)
            {
                Interlocked.Increment(ref isRun);
                t = new Thread(DecodeThread);
                t.IsBackground = true;
                t.Start();
            }
            else
            {
                Interlocked.Exchange(ref isRun, 0L); // stop thread
                t.Join(); // wait for end
                //MessageBox.Show("Decoding stopped");
            }
        }
        
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == this.WindowState)
            {
                myNotifyIcon.Visible = true;
                myNotifyIcon.ShowBalloonTip(500);
                this.Hide();
            }

            else if (FormWindowState.Normal == this.WindowState)
            {
                myNotifyIcon.Visible = false;
            }
        }

        private void myNotifyIcon_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void comboBoxCameras_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (init)
            {
                StartStopThread();
                thrashOldCamera();
                btnStart_Click(sender, e);
                StartStopThread();
            }
        }

        private void chkPreview_CheckedChanged(object sender, EventArgs e)
        {
            thrashOldCamera();
            startCapturing();
        }
    }
}
