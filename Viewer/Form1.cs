﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using AForge.Video;
using AForge.Video.DirectShow;

namespace Viewer
{
    public partial class Form1 : Form
    {
        private Stopwatch stopWatch = null;
        private Timer timer = new Timer() {Interval =  1000};
        private IVideoSource mySource;

        public Form1()
        {
            InitializeComponent();

            timer.Tick += Timer_Tick;

            this.Closing += (s, e) => { CloseCurrentVideoSource(); };
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
                    fpsLabel.Text = fps.ToString("F2") + @" fps";

                    stopWatch.Reset();
                    stopWatch.Start();
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            VideoCaptureDeviceForm form = new VideoCaptureDeviceForm();

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                // create video source
                VideoCaptureDevice videoSource = form.VideoDevice;
                videoSource.NewFrame += VideoSourceOnNewFrame;

                // open it
                OpenVideoSource(videoSource);
            }
        }

        private void VideoSourceOnNewFrame(object sender, NewFrameEventArgs eventargs)
        {
            Bitmap bmp = new Bitmap(eventargs.Frame);
            pictureBox1.Invoke(new Action(() =>
            {
                using (pictureBox1.Image)
                {
                    Image old = pictureBox1.Image;
                    pictureBox1.Image = bmp;
                    old?.Dispose();
                }
            }));
        }

        // Open video source
        private void OpenVideoSource(IVideoSource source)
        {
            // set busy cursor
            this.Cursor = Cursors.WaitCursor;

            // stop current video source
            CloseCurrentVideoSource();

            mySource = source;

            // start new video source
            mySource.Start();

            // reset stop watch
            stopWatch = null;

            // start timer
            timer.Start();

            this.Cursor = Cursors.Default;
        }

        // Close video source if it is running
        private void CloseCurrentVideoSource()
        {
            if (mySource != null)
            {
                mySource.SignalToStop();

                // wait ~ 3 seconds
                for (int i = 0; i < 30; i++)
                {
                    if (!mySource.IsRunning)
                        break;
                    System.Threading.Thread.Sleep(100);
                }

                if (mySource.IsRunning)
                {
                    mySource.WaitForStop();
                    mySource.Stop();
                }

                mySource = null;
            }
        }
    }
}
