using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CSharpProto1.Serial
{
    public class SerialController
    {
        private SerialPort sPort;
        private SerialDecoder serialDecoder;

        private string port;
        private int baud;

        private Action<List<DataPointRaw>>? guiPointsHandle;
        private Action<CommandPacket>? guiCommandHandle;

        private Action<string>? guiDelegate; // old
        private Action<List<int>>? guiDelegateList; // old

        public SerialController()
        {
            port = "COM4";
            baud = 115200;

            sPort = new SerialPort(port, baud);
            sPort.WriteTimeout = 500;
            sPort.ReadTimeout = 500;
            //sPort.DataReceived += SerialDataHandle;
            sPort.DataReceived += HandleSerialData;

            serialDecoder = new SerialDecoder();

        }

        public bool IsConnected()
        {
            return sPort.IsOpen;
        }

        public bool TryGetName(out string name)
        {
            if (sPort.IsOpen)
            {
                name = sPort.PortName;
                return true;
            }
            name = "";
            return false;
        }

        public bool Connect(string a_port, int a_baud, out string msg)
        {
            msg = "";
            bool ret = false;

            try
            {
                if (!SerialPort.GetPortNames().ToList().Contains(a_port))
                {
                    // TODO: Check emp

                    msg = "port '" + a_port + "' doesn't exist";
                    ret = false;
                    return ret;
                }
                if (sPort.IsOpen)
                {
                    sPort.Close();
                }

                sPort.PortName = a_port;
                sPort.BaudRate = a_baud;

                sPort.Open();
                if (sPort.IsOpen)
                {
                    sPort.ReadExisting(); // flush buffer
                }
                //CallGuiHandle(new CommandPacket(CommandFlags.GUI_check_connection, ""));
                //SendGuiCommand(new CommandPacket(CommandFlags.GUI_check_connection, ""));
                //sPort.ReadExisting(); // flush buffer
            }
            catch (UnauthorizedAccessException ex)
            {
                msg = "Access denied";
                ret = false;
            }
            catch (Exception ex)
            {
                msg = "Exception caugh";
                ret = false;
            }

            if (sPort.IsOpen)
            {
                msg = a_port + " Connected";
                ret = true;
            }

            return ret;
        }

        public bool Disconnect(out string msg)
        {
            msg = "";
            try
            {
                if (sPort.IsOpen)
                {
                    sPort.ReadExisting(); // flush buffer
                    sPort.Close();
                    msg = "Disconnected";
                }
                else
                {
                    msg = "Nothing connected";
                }
                return true;
            }
            catch
            {
                msg = "Exception caugh";
                return false;
            }
        }

        // -------------- BAD CODE ------------- OLD CODE ------------
        public string Connect(string a_port, int a_baud)
        {
            port = a_port;
            baud = a_baud;

            try
            {
                if (sPort.IsOpen)
                {
                    sPort.Close();
                }
                if( ! SerialPort.GetPortNames().ToList().Contains(port) )
                {
                    return "port '" + port + "' not available";
                }

                sPort.PortName = port;
                sPort.BaudRate = baud;

                sPort.Open();
                CallGuiHandle(new CommandPacket(CommandFlags.GUI_check_connection, ""));
                SendGuiCommand(new CommandPacket(CommandFlags.GUI_check_connection, ""));
                //sPort.ReadExisting(); // flush buffer
            }
            catch(UnauthorizedAccessException ex)
            {
                return "Access denied";
            }
            catch (Exception ex)
            {
                return "Exception caugh";
            }

            if (sPort.IsOpen)
            {
                return port + " Connected";
            }

            return "Failed to connect";
        }

        public void Reconnect()
        {
            try
            {
                if (sPort.IsOpen)
                {
                    sPort.Close();
                }
                sPort.PortName = port;
                sPort.BaudRate = baud;

                sPort.Open();
                sPort.ReadExisting(); // flush buffer
            }
            catch { }            
        }

        public string Disconnect()
        {
            try
            {
                if (sPort.IsOpen)
                {
                    sPort.ReadExisting(); // flush buffer
                    sPort.Close();
                    CallGuiHandle(new CommandPacket(CommandFlags.GUI_disconnected, "Machine Disconnected"));
                }
                return "Disconnected";
            }
            catch
            {
                return "Exception caugh";
            }
        }



        //---------------------------------------------------
        // UI interface and communication
        //---------------------------------------------------
        public void SetGuiHandle(Action<List<DataPointRaw>> pointDelegate)
        {
            guiPointsHandle = pointDelegate;
        }
        public void SetGuiHandle(Action<List<DataPointRaw>> pointDelegate, Action<CommandPacket> commandDelegate)
        {
            guiPointsHandle = pointDelegate;
            guiCommandHandle = commandDelegate;
        }
        private void CallGuiHandle(List<DataPointRaw> points)
        {
            // the Invoke DOESN'T switch the context to the delegate's thread
            guiPointsHandle?.Invoke(points);
            // the ?.Invoke lets me do a null check, it does not switch thread
        }
        private void CallGuiHandle(CommandPacket command)
        {
            // the ?.Invoke lets me do a null check, it does not switch thread
            guiCommandHandle?.Invoke(command);
        }
        public void SendGuiCommand(CommandPacket command)
        {
            // TODO: process the command types (flag)
            CallSerialHandle(command);
        }
        //---------------------------------------------------
        // Serial interface and communication
        //---------------------------------------------------
        private void CallSerialHandle(string payload)
        {
            if (sPort.IsOpen)
            {
                try
                {
                    //byte[] = serialDecoder.EncodeCommand();
                    sPort.Write(payload);
                }
                catch { }
            }
        }
        private void CallSerialHandle(CommandPacket command)
        {
            if (sPort.IsOpen)
            {
                try
                {
                    byte[] payload = serialDecoder.EncodeCommand(command);
                    sPort.Write(payload, 0, payload.Length);
                }
                catch { }
            }
        }
        public void HandleSerialData(object sender, SerialDataReceivedEventArgs e)
        {
            if(!sPort.IsOpen)
            {
                // idk how we got here but something went wrong
                CallGuiHandle( new CommandPacket(
                    CommandFlags.ESP_error, 
                    "HandleSerialData called but no serial port"));
                return;
            }

            try
            {
                int readSize = sPort.BytesToRead;
                if (readSize <= 0)
                {
                    return; // wait until there's more to read
                }
                byte[] arr = new byte[readSize];
                sPort.Read(arr, 0, readSize);

                serialDecoder.Decode(arr);
                if (serialDecoder.TryGetPoints(out List<DataPointRaw> points))
                {
                    CallGuiHandle(points);
                }
                // one read can contain several commands, hand them over in order
                while (serialDecoder.TryGetCommand(out CommandPacket command))
                {
                    CallGuiHandle(command);
                }
            }
            catch (Exception ex)
            {
                CallGuiHandle(new CommandPacket(
                    CommandFlags.ESP_error,
                    "HandleSerialData error: " + ex.Message));
            }
            

            // Testing code
            //if (arr[0] == '\n')
            //if (arr.ToList().Contains((byte)'\n'))
            //{
            //    CallGuiHandle(new List<DataPoint>{new DataPoint(testCounter, readSize, readSize) } );
            //    testCounter++;
            //}
        }
        //private int testCounter = 0;



        /////////////////////////////////////
        ///            OLD CODE :         ///
        /////////////////////////////////////
        public void LinkHandler(Action<string> refDelegate)
        {
            guiDelegate = refDelegate;
        }

        public void LinkListHandler(Action<List<int>> refDelegate)
        {
            guiDelegateList = refDelegate;
        }

        //---------------------------------------------------
        // UI interface and communication
        //---------------------------------------------------
        private void GuiDataSend(string payload)
        {
            if(guiDelegate != null)
            {
                guiDelegate(payload);
            }
        }
        private void GuiDataSend(List<int> payload)
        {
            if(guiDelegateList != null)
            {
                guiDelegateList(payload);
            }
        }

        /// <summary>
        /// Delegate fuction to be passed then used by the GUI
        /// </summary>
        /// <param name="payload"></param>
        public void GuiDataHandle(string payload)
        {
            //Invoke(this.GuiDataParser, payload);
            GuiDataParser(payload);
        }

        public void GuiReconnectHandle(string portName, int baudRate)
        {
            Reconnect();
        }

        //---------------------------------------------------
        // Serial interface and communication
        //---------------------------------------------------
        /// <summary>
        /// Send to serial
        /// </summary>
        private void SerialDataSender(string data)
        {
            if (sPort.IsOpen)
            {
                sPort.Write(data);
            }
            else
            {
                // attempt auto-connect
            }
        }

        /// <summary>
        /// Recieve from serial
        /// </summary>
        private void SerialDataHandle(object sender, SerialDataReceivedEventArgs e)
        {
            sender.GetType();

            int readSize = sPort.BytesToRead;
            if (readSize == -1)
            {
                // TODO: handle the case
            }
            if (readSize < 9)
            {
                return; // wait until there's more to read
            }
            byte[] arr = new byte[readSize];
            sPort.Read(arr, 0, readSize);
            //serialDecoder.Decode(arr);

            // code for testing:
            List<int> intArr = arr.Select(n => (int)n).ToList(); // convert bytes to int
            if(intArr.Contains('\n'))
            {
                serialDecoder.Decode(serialDecoder.MockTransmission());
                if (serialDecoder.TryGetPoints(out List<DataPointRaw> points)) {
                    //TestUpdateText("tested " + points[readSize % points.Count].force.ToString() + " " + readSize.ToString());
                }
                //TestUpdateText("tested " + readSize.ToString());
            }
            SerialByteAccumulator(intArr);
        }

        //---------------------------------------------------
        // Parsers
        //---------------------------------------------------
        private void GuiDataParser(string payload)
        {
            // just forward the info for now
            SerialDataSender(payload);
        }

        List<int> accumulator = new List<int>();
        private void SerialByteAccumulator(List<int> data)
        {
            foreach (int b in data)
            {
                if ((char)b == '\n')
                {
                    // we've hit the splitter character, send data to GUI
                    //GuiDataSend("payload size = " + parsingBuff.Count.ToString());
                    SerialDataParser(accumulator);
                    accumulator = new List<int>();
                    continue;
                }

                accumulator.Add(b);
            }
        }

        //List<int> parsingBuff = new List<int>();

        private void SerialDataParser(List<int> payload)
        {
            if(payload.Count != 64)
            {
                //GuiDataSend("payload size = " + payload.Count.ToString());
                return;
            }
            //GuiDataSend(payload);
            //return;

            List<int> parsed = new List<int>();

            string ret = "";
            for (int i = 0; i < 16; i++)
            {
                int val = 0;
                for (int j = 0; j < 4; j++)
                {
                    // convert 4 bytes to 1 int
                    val |= payload[i * 4 + j] << (3 - j) * 8;
                }
                parsed.Add(val);

                ret += val.ToString() + " ";
                // transform the value in a series of space to make a cheap graph
                //for (int s = 0; s < val; s += 10)
                //{
                //    ret += " ";
                //}
                //ret += "|\n";
            }
            ret += "\n";

            GuiDataSend(parsed);
            GuiDataSend(ret);

            // just forward the info for now
            //string str = "payload size = " + payload.Count.ToString();
            //GuiDataSend(str);
        }

    }
}
