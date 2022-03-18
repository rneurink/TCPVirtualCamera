using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public class VirtualCameraServer
    {
        #region Private Members

        private const int DEFAULT_PORT = 1100;
        private const int MAX_CONNECTION_BACKLOG = 10;

        private Socket myServerSocket;
        private Thread myServerThread;
        private List<Socket> myClientSockets = new List<Socket>();
        private bool myServerStop = false;
        public static ManualResetEvent myServerReadyForConnection = new ManualResetEvent(false);
        private string myIpAddress;

        #endregion

        #region Public Properties

        public Size CameraImageSize { get; set; } = new Size(1280, 720);

        public bool IsConnected => myServerSocket?.Connected ?? false;
        public bool IsBound => myServerSocket?.IsBound ?? false;
        public int Connections => myClientSockets.Count;
        public EndPoint Endpoint => myServerSocket?.LocalEndPoint;

        public Action<string> Log = s => { Debug.Write(s); };

        #endregion

        #region Constructor

        public VirtualCameraServer(string ipAddress = "127.0.0.1")
        {
            myIpAddress = ipAddress;
        }
        
        #endregion

        public void StartServer(int port = DEFAULT_PORT)
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse(myIpAddress);
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

                myServerSocket = new Socket(ipAddress.AddressFamily, 
                                            SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    myServerSocket.Bind(localEndPoint);
                    myServerSocket.Listen(MAX_CONNECTION_BACKLOG);
                    myServerStop = false;
                    myServerThread = new Thread(new ThreadStart(ServerThread)) {IsBackground = true, Name = "ServerThread", Priority = ThreadPriority.Normal};
                    myServerThread.Start();
                }
                catch (ArgumentNullException ane)
                {
                    Log(@$"ArgumentNullException : {ane.ToString()}");
                }
                catch (SocketException se)
                {
                    Log(@$"SocketException : {se.ToString()}");
                }
                catch (Exception e)
                {
                    Log(@$"Unexpected exception : {e.ToString()}");
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
                throw;
            }
        }

        public void ServerThread()
        {
            while (!myServerStop)
            {
                // Set the event to the reset state
                myServerReadyForConnection.Reset();

                // Start the async socket to listen for connections
                myServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), myServerSocket);

                // Wait for a connection to be made before starting to listen for another connection
                myServerReadyForConnection.WaitOne();
            }
        }

        public void AcceptCallback(IAsyncResult result)
        {
            if (myServerStop) return;
            // Indicate a connection has been made
            myServerReadyForConnection.Set();

            // Get the client socket
            Socket serverSocket = (Socket) result.AsyncState;
            
            Socket clientSocket = serverSocket.EndAccept(result);
            myClientSockets.Add(clientSocket);
            Log($@"Client connected {clientSocket.RemoteEndPoint}");
            Log($@"{myClientSockets.Count} clients connected");
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket clientSocket = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = clientSocket.EndSend(ar);
                //Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                //clientSocket.Shutdown(SocketShutdown.Both);
                //clientSocket.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Sends a bitmap to the virtual camera. This will resize the image to 1920x1080 if the source is a different resolution
        /// Speed comp: image is already the right size: 15ms, resize not keeping aspect ratio: 95ms, resize keeping aspect ratio: 160ms
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="keepAspectRatio"></param>
        /// <param name="disposeImage"></param>
        public void SendBitmap(Bitmap bitmap, bool keepAspectRatio = false, bool disposeImage = true)
        {
            byte[] rgbBytes;

            // Check if the image is already the right size
            if (bitmap.Width != CameraImageSize.Width ||
                bitmap.Height != CameraImageSize.Height)
            {
                // Source is not 1920x1080 resize
                var resizedbmp = new Bitmap(CameraImageSize.Width, CameraImageSize.Height);
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                if (keepAspectRatio)
                {
                    // Resize keeping aspect ratio this is rather slow using the graphics class of .net
                    using (Graphics g = Graphics.FromImage(resizedbmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;

                        // Figure out the ratio
                        float ratioX = (float)resizedbmp.Width / (float)bitmap.Width;
                        float ratioY = (float)resizedbmp.Height / (float)bitmap.Height;
                        // use whichever multiplier is smaller
                        double ratio = ratioX < ratioY ? ratioX : ratioY;

                        // now we can get the new height and width
                        int newHeight = Convert.ToInt32(bitmap.Height * ratio);
                        int newWidth = Convert.ToInt32(bitmap.Width * ratio);

                        // Now calculate the X,Y position of the upper-left corner 
                        // (one of these will always be zero)
                        int posX = Convert.ToInt32((resizedbmp.Width - (bitmap.Width * ratio)) / 2);
                        int posY = Convert.ToInt32((resizedbmp.Height - (bitmap.Height * ratio)) / 2);

                        g.Clear(Color.White); // white padding
                        g.DrawImage(bitmap, posX, posY, newWidth, newHeight);
                    }
                }
                else
                {
                    // Resize not keeping aspect ratio. This is about 
                    resizedbmp = new Bitmap(bitmap, CameraImageSize);
                }

                var rect = new Rectangle(0, 0, resizedbmp.Width, resizedbmp.Height);
                BitmapData data = resizedbmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                int bytes = Math.Abs(data.Stride) * resizedbmp.Height;
                rgbBytes = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, rgbBytes, 0, bytes);
                resizedbmp.UnlockBits(data);
                resizedbmp.Dispose();
            }
            else
            {
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                int bytes = Math.Abs(data.Stride) * bitmap.Height;
                rgbBytes = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, rgbBytes, 0, bytes);
                bitmap.UnlockBits(data);
            }

            if (disposeImage)
                bitmap.Dispose();

            SendData(rgbBytes);
        }

        public void SendColorFrame(Color color)
        {
            using (var bmp = new Bitmap(CameraImageSize.Width, CameraImageSize.Height, PixelFormat.Format24bppRgb))
            {
                Graphics g = Graphics.FromImage(bmp);
                var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                g.Clear(color);
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                int bytes = Math.Abs(data.Stride) * bmp.Height;
                var dataBytes = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, dataBytes, 0, bytes);
                bmp.UnlockBits(data);
                SendData(dataBytes);
            }
        }

        public void SendData(byte[] data)
        {
            try
            {
                myClientSockets.ForEach(i => i.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendCallback), i));

            }
            catch (ArgumentNullException ane)
            {
                Log(@$"ArgumentNullException : {ane.ToString()}");
            }
            catch (SocketException se)
            {
                Log(@$"SocketException : {se.ToString()}");
            }
            catch (Exception e)
            {
                Log(@$"Unexpected exception : {e.ToString()}");
            }
        }

        public void StopServer()
        {
            try
            {
                if (myServerThread != null)
                {
                    myServerStop = true;
                    myServerReadyForConnection.Set();
                    myServerThread.Join();
                    myServerThread = null;
                }

                //Log($@"Closing connection {myServerSocket.RemoteEndPoint.ToString()} ");
                if (myServerSocket == null) return;
                if (myServerSocket.Connected)
                    myServerSocket.Shutdown(SocketShutdown.Both);
                myServerSocket.Close();
                myServerSocket = null;

                myClientSockets.ForEach(i => i.Close());
                myClientSockets.Clear();
            }
            catch (ArgumentNullException ane)
            {
                Log(@$"ArgumentNullException : {ane.ToString()}");
            }
            catch (SocketException se)
            {
                Log(@$"SocketException : {se.ToString()}");
            }
            catch (Exception e)
            {
                Log(@$"Unexpected exception : {e.ToString()}");
            }
        }
    }
}
