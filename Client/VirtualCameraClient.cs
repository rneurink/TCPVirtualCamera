using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class VirtualCameraClient
    {
        #region Private Members

        private const int DEFAULT_PORT = 1100;
        private const int MAX_PORT = 1110;

        private Socket myClientSocket;
        private string myIpAddress;

        #endregion

        #region Public Properties

        public bool IsConnected => myClientSocket?.Connected ?? false;
        public EndPoint Endpoint => myClientSocket?.LocalEndPoint;

        public Action<string> Log = s => { Debug.Write(s); };

        #endregion

        #region Constructor

        public VirtualCameraClient(string ipAddress = "127.0.0.1")
        {
            myIpAddress = ipAddress;
        }

        #endregion

        public int[] GetAvailablePorts()
        {
            IEnumerable<int> portRange = Enumerable.Range(DEFAULT_PORT, MAX_PORT - DEFAULT_PORT);

            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] endPoints = properties.GetActiveTcpListeners().Where(i => i.Address.Equals(IPAddress.Parse(myIpAddress)) && portRange.Contains(i.Port)).ToArray();
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections().Where(i => i.LocalEndPoint.Address.Equals(IPAddress.Parse(myIpAddress)) && portRange.Contains(i.LocalEndPoint.Port)).ToArray();

            int[] foundPorts = endPoints.Select(i => i.Port).ToArray();
            Log($@"Ports found {string.Join(',', foundPorts)}");
            int[] usedPorts = connections.Select(i => i.LocalEndPoint.Port).ToArray();
            Log($@"Ports currently in use {string.Join(',', usedPorts)}");
            return foundPorts.Except(usedPorts).ToArray();
        }

        public void StartClient(int port = DEFAULT_PORT)
        {
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                IPAddress ipAddress = IPAddress.Parse(myIpAddress);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                // Create a TCP/IP  socket.  
                myClientSocket = new Socket(ipAddress.AddressFamily,
                                            SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.  
                try
                {
                    myClientSocket.Connect(remoteEP);

                    Log(@$"Socket connected to {myClientSocket.RemoteEndPoint.ToString()} ");
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
            if (bitmap.Width != 1920 ||
                bitmap.Height != 1080)
            {
                // Source is not 1920x1080 resize
                var resizedbmp = new Bitmap(1920, 1080);
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
                    resizedbmp = new Bitmap(bitmap, new Size(1920, 1080));
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
            using (var bmp = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb))
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
                int bytesSend = myClientSocket.Send(data);
                //Log(@"Bytes send: {0}", bytesSend);

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

        public void CloseClient()
        {
            try
            {
                Log($@"Closing connection {myClientSocket.RemoteEndPoint.ToString()} ");
                myClientSocket.Shutdown(SocketShutdown.Both);
                myClientSocket.Close();
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
