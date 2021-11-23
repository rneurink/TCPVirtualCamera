using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DirectShowLib;

namespace DirectShowPlayer
{
    public partial class Form1 : Form
    {
        const int DEVICE_INDEX = 1; // Change this to select the correct videocapture device

        public Form1()
        {
            InitializeComponent();
            this.Resize += Form1_Resize;
            CaptureVideo();
        }

        private void Form1_Resize(object sender, System.EventArgs e)
        {
            // Stop graph when Form is iconic
            if (this.WindowState == FormWindowState.Minimized)
                ChangePreviewState(false);

            // Restart Graph when window come back to normal state
            if (this.WindowState == FormWindowState.Normal)
                ChangePreviewState(true);

            ResizeVideoWindow();
        }


        #region Directshowlib

        public const int WM_GRAPHNOTIFY = 0x8000 + 1;

        IVideoWindow videoWindow = null;
        IMediaControl mediaControl = null;
        IMediaEventEx mediaEventEx = null;
        IGraphBuilder graphBuilder = null;
        ICaptureGraphBuilder2 captureGraphBuilder = null;
        bool running = false;

        DsROTEntry rot = null;

        public void CaptureVideo()
        {
            int hr = 0;
            IBaseFilter sourceFilter = null;

            try
            {
                // Get DirectShow interfaces
                GetInterfaces();

                // Attach the filter graph to the capture graph
                hr = this.captureGraphBuilder.SetFiltergraph(this.graphBuilder);
                DsError.ThrowExceptionForHR(hr);

                // Use the system device enumerator and class enumerator to find
                // a video capture/preview device, such as a desktop USB video camera.
                sourceFilter = FindCaptureDevice();

                // Add Capture filter to our graph.
                hr = this.graphBuilder.AddFilter(sourceFilter, @"Video Capture");
                DsError.ThrowExceptionForHR(hr);

                // Render the preview pin on the video capture filter
                // Use this instead of this.graphBuilder.RenderFile
                hr = this.captureGraphBuilder.RenderStream(PinCategory.Preview, MediaType.Video, sourceFilter, null, null);
                DsError.ThrowExceptionForHR(hr);

                // Now that the filter has been added to the graph and we have
                // rendered its stream, we can release this reference to the filter.
                Marshal.ReleaseComObject(sourceFilter);

                // Set video window style and position
                SetupVideoWindow();

                // Add our graph to the running object table, which will allow
                // the GraphEdit application to "spy" on our graph
                rot = new DsROTEntry(this.graphBuilder);

                // Start previewing video data
                hr = this.mediaControl.Run();
                DsError.ThrowExceptionForHR(hr);

                // Remember current state
                running = true;
            }
            catch
            {
                MessageBox.Show(@"An unrecoverable error has occurred.");
            }
        }

        public IBaseFilter FindCaptureDevice()
        {
            DsDevice[] devices;
            object source;

            // Get all video input devices
            devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

            // Take the first device
            DsDevice device = (DsDevice)devices[DEVICE_INDEX];

            // Bind Moniker to a filter object
            Guid iid = typeof(IBaseFilter).GUID;
            device.Mon.BindToObject(null, null, ref iid, out source);

            // An exception is thrown if cast fail
            return (IBaseFilter)source;
        }

        public void GetInterfaces()
        {
            int hr = 0;

            // An exception is thrown if cast fail
            this.graphBuilder = (IGraphBuilder)new FilterGraph();
            this.captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            this.mediaControl = (IMediaControl)this.graphBuilder;
            this.videoWindow = (IVideoWindow)this.graphBuilder;
            this.mediaEventEx = (IMediaEventEx)this.graphBuilder;

            hr = this.mediaEventEx.SetNotifyWindow(this.Handle, WM_GRAPHNOTIFY, IntPtr.Zero);
            DsError.ThrowExceptionForHR(hr);
        }

        public void CloseInterfaces()
        {
            // Stop previewing data
            if (this.mediaControl != null)
                this.mediaControl.StopWhenReady();

            running = false;

            // Stop receiving events
            if (this.mediaEventEx != null)
                this.mediaEventEx.SetNotifyWindow(IntPtr.Zero, WM_GRAPHNOTIFY, IntPtr.Zero);

            // Relinquish ownership (IMPORTANT!) of the video window.
            // Failing to call put_Owner can lead to assert failures within
            // the video renderer, as it still assumes that it has a valid
            // parent window.
            if (this.videoWindow != null)
            {
                this.videoWindow.put_Visible(OABool.False);
                this.videoWindow.put_Owner(IntPtr.Zero);
            }

            // Remove filter graph from the running object table
            if (rot != null)
            {
                rot.Dispose();
                rot = null;
            }

            // Release DirectShow interfaces
            Marshal.ReleaseComObject(this.mediaControl); this.mediaControl = null;
            Marshal.ReleaseComObject(this.mediaEventEx); this.mediaEventEx = null;
            Marshal.ReleaseComObject(this.videoWindow); this.videoWindow = null;
            Marshal.ReleaseComObject(this.graphBuilder); this.graphBuilder = null;
            Marshal.ReleaseComObject(this.captureGraphBuilder); this.captureGraphBuilder = null;
        }

        public void SetupVideoWindow()
        {
            int hr = 0;

            // Set the video window to be a child of the main window
            hr = this.videoWindow.put_Owner(this.Handle);
            DsError.ThrowExceptionForHR(hr);

            hr = this.videoWindow.put_WindowStyle(WindowStyle.Child | WindowStyle.ClipChildren);
            DsError.ThrowExceptionForHR(hr);

            // Use helper function to position video window in client rect 
            // of main application window
            ResizeVideoWindow();

            // Make the video window visible, now that it is properly positioned
            hr = this.videoWindow.put_Visible(OABool.True);
            DsError.ThrowExceptionForHR(hr);
        }

        public void ResizeVideoWindow()
        {
            // Resize the video preview window to match owner window size
            if (this.videoWindow != null)
            {
                this.videoWindow.SetWindowPosition(0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }
        }

        public void ChangePreviewState(bool showVideo)
        {
            int hr = 0;

            // If the media control interface isn't ready, don't call it
            if (this.mediaControl == null)
                return;

            if (showVideo)
            {
                if (!running)
                {
                    // Start previewing video data
                    hr = this.mediaControl.Run();
                    running = true;
                }
            }
            else
            {
                // Stop previewing video data
                hr = this.mediaControl.StopWhenReady();
                running = false;
            }
        }

        public void HandleGraphEvent()
        {
            int hr = 0;
            EventCode evCode;
            IntPtr evParam1, evParam2;

            if (this.mediaEventEx == null)
                return;

            while (this.mediaEventEx.GetEvent(out evCode, out evParam1, out evParam2, 0) == 0)
            {
                // Free event parameters to prevent memory leaks associated with
                // event parameter data.  While this application is not interested
                // in the received events, applications should always process them.
                hr = this.mediaEventEx.FreeEventParams(evCode, evParam1, evParam2);
                DsError.ThrowExceptionForHR(hr);

                // Insert event processing code here, if desired
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_GRAPHNOTIFY:
                    {
                        HandleGraphEvent();
                        break;
                    }
            }

            // Pass this message to the video window for notification of system changes
            if (this.videoWindow != null)
                this.videoWindow.NotifyOwnerMessage(m.HWnd, m.Msg, m.WParam, m.LParam);

            base.WndProc(ref m);
        }

        #endregion
    }
}
