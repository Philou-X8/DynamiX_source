using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace CSharpProto1.Serial
{
    public partial class SerialPanel : UserControl
    {
        private SerialController? serialPort;
        private Action<CommandPacket>? guiCommandHandle;

        private List<string> listCom;
        private List<string> listBaud;

        private int sizeX;
        private int sizeY;

        private System.Windows.Forms.Timer connectionTimer;


        public SerialPanel()
        {
            InitializeComponent();
            messageSatus.Text = "";

            listCom = SerialPort.GetPortNames().ToList();
            listBaud = new List<string>()
            {
                "9600",
                "38400",
                "57600",
                "115200",
            };

            //sizeX = this.MinimumSize.Width;
            //sizeY = this.MinimumSize.Height;

            sizeX = 200;
            sizeY = 220;

            connectionTimer = new System.Windows.Forms.Timer();
            connectionTimer.Interval = 2000; // in ms
            connectionTimer.Tick += new EventHandler(ConnectionTimeout);
        }

        public void BindSerialController(SerialController controller)
        {
            serialPort = controller;
            RefreshUI();
        }
        public void SetGuiHandle(Action<CommandPacket> commandDelegate)
        {
            guiCommandHandle = commandDelegate;
        }
        private void CallGuiHandle(CommandPacket command)
        {
            // the ?.Invoke lets me do a null check, it does not switch thread
            guiCommandHandle?.Invoke(command);
        }


        public void OpenPanel()
        {
            this.MinimumSize = new Size(sizeX, sizeY);
            this.Size = new Size(sizeX, sizeY);
            //layoutGrid.Show();
            //layoutGrid.Size = new Size(sizeX, sizeY);
            RefreshUI();
            this.Show();
        }
        public void HidePanel()
        {
            //layoutGrid.Hide();
            this.MinimumSize = new Size(0, 0);
            this.Size = new Size(0, 0);
            this.Hide();
        }

        private void RefreshUI()
        {
            try
            {
                listCom = SerialPort.GetPortNames().ToList();

                comboBoxCom.Items.Clear();
                comboBoxCom.Items.AddRange(SerialPort.GetPortNames());
                string name = "";
                bool isConnected = serialPort?.TryGetName(out name) ?? false;
                comboBoxCom.SelectedIndex = comboBoxCom.Items.IndexOf(name);

                comboBoxBaud.Items.Clear();
                comboBoxBaud.Items.AddRange(listBaud.ToArray());
                comboBoxBaud.SelectedIndex = comboBoxBaud.Items.IndexOf("115200"); // matches the ESP firmware
            } 
            catch
            {

            }

            

            //if (isConnected)
            //{
            //    messageSatus.Text = "Connected";
            //}
            //else
            //{
            //    messageSatus.Text = "Not Connected";
            //}
        }


        private void ButtonConnect_Click(object sender, EventArgs e)
        {
            string comPort = "COM4";
            int baudRate = 115200;

            comPort = comboBoxCom.Text;
            if(comboBoxCom.Text.Length <= 0 || comboBoxCom.Text == "")
            {
                // no COM port selected
                return;
            }

            try
            {
                baudRate = int.Parse(comboBoxBaud.Text);
            }
            catch
            {
                baudRate = 115200;
            }


            if (serialPort == null)
            {
                messageSatus.Text = "no SerialController";
            }
            else if (serialPort.Connect(comPort, baudRate, out string msg))
            {
                // serial port connected, send validation ping
                messageSatus.Text = msg;
                CallGuiHandle(new CommandPacket(CommandFlags.GUI_check_connection, msg));
                connectionTimer.Start();
            }
            else
            {
                // failed to connect
                messageSatus.Text = msg;
                CallGuiHandle(new CommandPacket(CommandFlags.GUI_disconnected, "Connection failed: " + msg));
            }

            /*
            string comPort = "COM4";
            int baudRate = 115200;

            comPort = comboBoxCom.Text;
            try
            {
                baudRate = int.Parse(comboBoxBaud.Text);
            }
            catch
            {
                baudRate = 115200;
            }

            string ret = messageSatus.Text;
            ret = serialPort?.Connect(comPort, baudRate) ?? "no Controller";
            messageSatus.Text = ret;
            */
        }

        private void ButtonReconnect_Click(object sender, EventArgs e)
        {
            connectionTimer.Stop();

            serialPort?.Reconnect();
        }

        private void ButtonDisconnect_Click(object sender, EventArgs e)
        {
            connectionTimer.Stop();

            string ret = messageSatus.Text;
            ret = serialPort?.Disconnect() ?? ret;
            messageSatus.Text = ret;
        }

        private void ConnectionTimeout(object sender, EventArgs e)
        {
            connectionTimer.Stop();

            if (serialPort?.TryGetName(out string portName) ?? false)
            {
                serialPort?.Disconnect();
                messageSatus.Text = portName + " Bad device";
            }
            else
            {
                serialPort?.Disconnect();
                messageSatus.Text = "Bad device";
            }

            //CallGuiHandle(new CommandPacket(CommandFlags.GUI_disconnected, "Connection handshake failed"));
        }
        public void ConnectionConfirmed()
        {
            connectionTimer.Stop();

            // TODO:
            // set internal value to know we're connected to a serial device
            // but also to know it's actually the ESP32
        }
    }
}
