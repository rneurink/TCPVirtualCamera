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
using Microsoft.VisualBasic.Logging;

namespace Client
{
    public partial class Form1 : Form
    {
        private Task RunTask = null;

        private VirtualCameraClient myClient = new VirtualCameraClient();
        private List<VirtualCameraClient> myClients = new List<VirtualCameraClient>();

        private CancellationTokenSource ct = new CancellationTokenSource();

        public Form1()
        {
            InitializeComponent();

            myClient.Log = LogToTextbox;



            Shown += (s, e) =>
            {
                portCB.Items.AddRange(myClient.GetAvailablePorts().Cast<object>().ToArray());
                portCB.Items.Add("Broadcast");
                portCB.SelectedIndex = 0;
            };

            Closing += (s, e) =>
            {
                if (myClient.IsConnected)
                {
                    myClients.ForEach(c => c.CloseClient());
                    myClient.CloseClient();
                }
            };
        }

        private void connectBTN_Click(object sender, EventArgs e)
        {
            if (myClient.IsConnected)
            {
                myClient.CloseClient();

                connectBTN.Text = @"Connect";
                sendImageBTN.Enabled = false;
                cycleImageBTN.Enabled = false;
                rgbTestBTN.Enabled = false;
                portCB.Enabled = true;
            }
            else
            {
                if (portCB.SelectedItem == "Broadcast")
                {
                    //TODO:
                }
                else
                {
                    myClient.StartClient((int)portCB.SelectedItem);
                }

                connectBTN.Text = @"Disconnect";
                sendImageBTN.Enabled = true;
                cycleImageBTN.Enabled = true;
                rgbTestBTN.Enabled = true;
                portCB.Enabled = false;
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
                    myClient.SendBitmap(image, aspectRatioCB.Checked);
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
                            myClient.SendBitmap(image, aspectRatioCB.Checked);
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
                                myClient.SendColorFrame(Color.Red);
                                break;
                            case 1:
                                myClient.SendColorFrame(Color.Green);
                                break;
                            case 2:
                                myClient.SendColorFrame(Color.Blue);
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

        private void portCB_DropDown(object sender, EventArgs e)
        {
            portCB.Items.Clear();
            portCB.Items.AddRange(myClient.GetAvailablePorts().Cast<object>().ToArray());
            portCB.Items.Add("Broadcast");
            portCB.SelectedIndex = 0;
        }
    }



    public class SynchronousSocketClient
    {
        private Socket sendSocket;

        public bool IsConnected => sendSocket.Connected;
        public void StartClient(int port = 1100)
        {
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                // This example uses port 11000 on the local computer.  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                // Create a TCP/IP  socket.  
                sendSocket = new Socket(ipAddress.AddressFamily,
                                         SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.  
                try
                {
                    sendSocket.Connect(remoteEP);

                    Debug.WriteLine(@"Socket connected to {0}",
                                      sendSocket.RemoteEndPoint.ToString());
                }
                catch (ArgumentNullException ane)
                {
                    Debug.WriteLine(@"ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    Debug.WriteLine(@"SocketException : {0}", se.ToString());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(@"Unexpected exception : {0}", e.ToString());
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public void SendData(byte[] data)
        {
            try
            {
                int bytesSend = sendSocket.Send(data);
                //Debug.WriteLine(@"Bytes send: {0}", bytesSend);

            }
            catch (ArgumentNullException ane)
            {
                Debug.WriteLine(@"ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                Debug.WriteLine(@"SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"Unexpected exception : {0}", e.ToString());
            }
        }

        public void CloseClient()
        {
            try
            {
                sendSocket.Shutdown(SocketShutdown.Both);
                sendSocket.Close();
            }
            catch (ArgumentNullException ane)
            {
                Debug.WriteLine(@"ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                Debug.WriteLine(@"SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"Unexpected exception : {0}", e.ToString());
            }
        }

        public static void StartClient(byte[] msg)
        {
            // Data buffer for incoming data.  
            byte[] bytes = new byte[2048];

            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                // This example uses port 11000 on the local computer.  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, 1100);

                // Create a TCP/IP  socket.  
                Socket sender = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.  
                try
                {
                    sender.Connect(remoteEP);

                    Debug.WriteLine(@"Socket connected to {0}",
                        sender.RemoteEndPoint.ToString());


                    // Send the data through the socket.  
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    //int bytesRec = sender.Receive(bytes);
                    //string x = BitConverter.ToString(bytes).Replace("-", "");
                    //Debug.WriteLine(@"Echoed test = {0}",
                    //    x);

                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();

                }
                catch (ArgumentNullException ane)
                {
                    Debug.WriteLine(@"ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    Debug.WriteLine(@"SocketException : {0}", se.ToString());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(@"Unexpected exception : {0}", e.ToString());
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }
    }
}
