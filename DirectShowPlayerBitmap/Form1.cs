using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DirectShowLib;

namespace DirectShowPlayerBitmap
{
    public partial class Form1 : Form
    {
        const int VIDEODEVICE = 0; // zero based index of video capture device to use
        const int FRAMERATE = 0;  // Depends on video device caps.  Generally 4-30.
        const int VIDEOWIDTH = 1280; // Depends on video device caps
        const int VIDEOHEIGHT = 720; // Depends on video device caps

        private volatile bool busyShutdown = false;
        private Thread CaptureThread;

        public Form1()
        {
            InitializeComponent();

            this.Shown += (s, e) =>
            {
                ThreadStart starter = new ThreadStart(StartCamera);
                CaptureThread = new Thread(starter);
                CaptureThread.Start();
            };

            this.Closing += (s, e) =>
            {
                busyShutdown = true;
                for (int i = 0; i < 3; i++)
                {
                    if (CaptureThread.IsAlive)
                        Thread.Sleep(500);
                }

                if (CaptureThread.IsAlive)
                {
                    CaptureThread.Join(5000);
                }
            };
        }

        public void StartCamera()
        {
            DSCamera cam = null;

            try
            {
                cam = new DSCamera(VIDEODEVICE, FRAMERATE, VIDEOWIDTH, VIDEOHEIGHT);
                CaptureImages(cam);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            finally
            {
                if (cam != null)
                {
                    cam.Dispose();
                }
            }
        }

        private void CaptureImages(DSCamera cam)
        {
            Bitmap image = null;
            IntPtr ip = IntPtr.Zero;

            do
            {
                cam.Start();

                while (!busyShutdown)
                {
                    try
                    {
                        ip = cam.GetBitMap();
                        image = new Bitmap(cam.Width, cam.Height, cam.Stride, PixelFormat.Format24bppRgb, ip);
                        image.RotateFlip(RotateFlipType.RotateNoneFlipY);

                        pictureBox1.Invoke(new Action(() =>
                        {
                            using (pictureBox1.Image)
                            {
                                var oldimage = pictureBox1.Image;
                                pictureBox1.Image = (Bitmap) image.Clone();
                                oldimage?.Dispose();
                            }
                        }));

                        image.Dispose();
                        image = null;

                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                    finally
                    {
                        if (ip != IntPtr.Zero)
                        {
                            Marshal.FreeCoTaskMem(ip);
                            ip = IntPtr.Zero;
                        }
                    }
                    cam.Pause();
                }

            } while (!busyShutdown);
        }
    }
}
