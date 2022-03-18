using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using Microsoft.VisualBasic.Logging;

namespace Server
{
    public partial class Form1 : Form
    {
        private Task RunTask = null;

        private VirtualCameraServer myServer = new VirtualCameraServer();

        private CancellationTokenSource ct = new CancellationTokenSource();

        private Stopwatch stopWatch = null;
        private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer() { Interval = 1000 };
        private IVideoSource mySource;
        private bool requestedStop = false;
        private Size[] sizes = new[] {new Size(1920, 1080), new Size(1280, 720), new Size(480, 360), new Size(320, 240)};

        public Form1()
        {
            InitializeComponent();

            myServer.Log = LogToTextbox;

            timer.Tick += Timer_Tick;

            Shown += (s, e) =>
            {
                comboBox1.Items.AddRange(new []{"1920x1080 30", "1280x720 30", "480x360 30", "320x240 30"});
                comboBox1.SelectedIndex = 1;
                comboBox1.SelectedIndexChanged += (se, ev) => { myServer.CameraImageSize = sizes[comboBox1.SelectedIndex]; };
            };

            Closing += (s, e) =>
            {
                CloseCurrentVideoSource();
                myServer.StopServer();
            };
        }

        private void connectBTN_Click(object sender, EventArgs e)
        {
            if (myServer.IsBound)
            {
                myServer.StopServer();

                serverBTN.Text = @"StartServer";
                sendImageBTN.Enabled = false;
                cycleImageBTN.Enabled = false;
                rgbTestBTN.Enabled = false;
                sendCameraCB.Enabled = false;
                sendCameraCB.Checked = false;
            }
            else
            {
                myServer.StartServer();

                serverBTN.Text = @"StopServer";
                sendImageBTN.Enabled = true;
                cycleImageBTN.Enabled = true;
                rgbTestBTN.Enabled = true;
                sendCameraCB.Enabled = true;
            }
        }

        private void sendImageBTN_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = @"Image files|*.bmp;*.png;*.jpg;*.jpeg|All files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                using (var image = new Bitmap(ofd.FileName))
                {
                    myServer.SendBitmap(image, aspectRatioCB.Checked);
                }
            }
        }

        private async void cycleImageBTN_Click(object sender, EventArgs e)
        {
            if (RunTask == null)
            {
                LogToTextbox($@"Starting cycle images task");
                ct = new CancellationTokenSource();
                string[] files = new string[0];

                FolderBrowserDialog fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog() != DialogResult.OK)
                    return;

                var extensions = new string[] { "*.bmp", "*.png", "*.jpg", "*.jpeg" };
                files = extensions.SelectMany(e => Directory.GetFiles(fbd.SelectedPath, e, subfolderCB.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)).ToArray();

                RunTask = new Task(new Action(() =>
                {
                    int i = 0;
                    for (; ; )
                    {
                        using (var image = new Bitmap(files[i]))
                        {
                            myServer.SendBitmap(image, aspectRatioCB.Checked);
                        }

                        i++;
                        if (i >= files.Length) i = 0;

                        Task.Delay((int)interImageDelayNUM.Value).Wait();

                        if (ct.Token.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }));
                RunTask.Start();
                cycleImageBTN.Text = @"Stop image cycle";
                sendImageBTN.Enabled = false;
                rgbTestBTN.Enabled = false;
                subfolderCB.Enabled = false;
                interImageDelayNUM.Enabled = false;
            }
            else
            {
                LogToTextbox($@"Shutting down cycle images task ");
                try
                {
                    ct.Cancel();
                    await RunTask;
                }
                catch (OperationCanceledException exp)
                {
                    LogToTextbox($"{nameof(OperationCanceledException)} thrown with message: {exp.Message}");
                }
                RunTask.Dispose();
                ct.Dispose();
                RunTask = null;
                cycleImageBTN.Text = @"Cycle images";
                sendImageBTN.Enabled = true;
                rgbTestBTN.Enabled = true;
                subfolderCB.Enabled = true;
                interImageDelayNUM.Enabled = true;
            }
        }

        private async void rgbTestBTN_Click(object sender, EventArgs e)
        {
            if (RunTask == null)
            {
                LogToTextbox($@"Starting RGB test frame task ");
                ct = new CancellationTokenSource();
                RunTask = new Task(new Action(() =>
                {
                    int i = 0;
                    for (; ; )
                    {
                        switch (i)
                        {
                            case 0:
                                myServer.SendColorFrame(Color.Red);
                                break;
                            case 1:
                                myServer.SendColorFrame(Color.Green);
                                break;
                            case 2:
                                myServer.SendColorFrame(Color.Blue);
                                break;
                        }

                        i++;
                        if (i >= 3) i = 0;

                        Task.Delay((int)interImageDelayNUM.Value).Wait();

                        if (ct.Token.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }));
                RunTask.Start();
                rgbTestBTN.Text = @"Stop RGB test frames";
                sendImageBTN.Enabled = false;
                cycleImageBTN.Enabled = false;
                interImageDelayNUM.Enabled = false;
            }
            else
            {
                LogToTextbox($@"Shutting down RGB test frame task ");
                try
                {
                    ct.Cancel();
                    await RunTask;
                }
                catch (OperationCanceledException exp)
                {
                    LogToTextbox($"{nameof(OperationCanceledException)} thrown with message: {exp.Message}");
                }
                RunTask.Dispose();
                ct.Dispose();
                RunTask = null;
                rgbTestBTN.Text = @"Start RGB test frames";
                sendImageBTN.Enabled = true;
                cycleImageBTN.Enabled = true;
                interImageDelayNUM.Enabled = true;
            }
        }

        public void LogToTextbox(string message)
        {
            logTextBox.Invoke(new Action(() =>
            {
                logTextBox.AppendText(message);
                logTextBox.AppendText(Environment.NewLine);
            }));
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mySource != null)
            {
                // get number of frames since the last timer tick
                int framesReceived = mySource.FramesReceived;

                if (stopWatch == null)
                {
                    stopWatch = new Stopwatch();
                    stopWatch.Start();
                }
                else
                {
                    stopWatch.Stop();

                    float fps = 1000.0f * framesReceived / stopWatch.ElapsedMilliseconds;
                    fpsLabel.Text = $@"FPS:{fps:##}";

                    stopWatch.Reset();
                    stopWatch.Start();
                }
            }
        }

        private void cameraBTN_Click(object sender, EventArgs e)
        {
            if (mySource == null)
            {
                VideoCaptureDeviceForm form = new VideoCaptureDeviceForm();
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    // create video source
                    VideoCaptureDevice videoSource = form.VideoDevice;
                    videoSource.NewFrame += VideoSourceOnNewFrame;

                    requestedStop = false;

                    // open it
                    OpenVideoSource(videoSource);
                }

                cameraBTN.Text = @"Close source";
                sendImageBTN.Enabled = false;
                cycleImageBTN.Enabled = false;
                rgbTestBTN.Enabled = false;
            }
            else
            {
                CloseCurrentVideoSource();
                cameraBTN.Text = @"Select source";
                sendImageBTN.Enabled = true;
                cycleImageBTN.Enabled = true;
                rgbTestBTN.Enabled = true;
            }
        }

        private void VideoSourceOnNewFrame(object sender, NewFrameEventArgs eventargs)
        {
            if (requestedStop) return;
            Bitmap bmp = new Bitmap(eventargs.Frame);
            try
            {
                cameraPB?.Invoke(new Action(() =>
                {
                    using (cameraPB.Image)
                    {
                        Image old = cameraPB.Image;
                        cameraPB.Image = (Bitmap)bmp.Clone();
                        old?.Dispose();
                    }
                }));

                if (sendCameraCB.Checked)
                    myServer.SendBitmap(bmp, aspectRatioCB.Checked, true);
                else 
                    bmp.Dispose();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void OpenVideoSource(IVideoSource source)
        {
            // set busy cursor
            this.Cursor = Cursors.WaitCursor;

            // stop current video source
            CloseCurrentVideoSource();

            mySource = source;

            // start new video source
            mySource.Start();

            // start timer
            timer.Start();
        }

        // Close video source if it is running
        private void CloseCurrentVideoSource()
        {
            if (mySource != null)
            {
                requestedStop = true;
                mySource.NewFrame -= VideoSourceOnNewFrame;
                mySource.SignalToStop();

                mySource = null;
            }

            timer.Stop();
        }
    }
}
