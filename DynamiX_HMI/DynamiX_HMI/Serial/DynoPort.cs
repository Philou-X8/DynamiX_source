using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamiX_HMI.Serial
{
    /// <summary> The simulator can't be used (its DLL is missing, or built for another platform). </summary>
    public sealed class SimulatorUnavailableException : Exception
    {
        public SimulatorUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// The port the HMI talks to the machine through. It has the members of System.IO.Ports.SerialPort that
    /// SerialController uses, and does exactly what SerialPort does, except for one port name: "Simulator" is not a
    /// COM port but the simulated machine in DynamiX_Simulator.dll (the ESP32's firmware running on the PC).
    /// Everything above this class (the decoder, the state machines) can't tell the difference.
    /// </summary>
    public sealed class DynoPort
    {
        public const string SimulatorName = "Simulator";

        private readonly SerialPort serial;
        private SimulatorLink? simulator;
        private string portName;

        /// <summary> Bytes are waiting to be read. Raised on a thread of the pool (or the simulator's own thread). </summary>
        public event EventHandler? DataReceived;

        public DynoPort(string name, int baud)
        {
            portName = name;
            serial = new SerialPort(name == SimulatorName ? "COM1" : name, baud);
            serial.DataReceived += (s, e) => DataReceived?.Invoke(this, EventArgs.Empty);
        }

        /// <summary> A COM port that exists on this PC, or the simulator. </summary>
        public static bool PortExists(string name)
        {
            return name == SimulatorName || SerialPort.GetPortNames().Contains(name);
        }

        public bool IsOpen => simulator != null ? simulator.IsOpen : serial.IsOpen;

        public string PortName
        {
            get => simulator != null ? SimulatorName : serial.PortName;
            set
            {
                portName = value;
                if (value != SimulatorName)
                {
                    serial.PortName = value;
                }
            }
        }

        public int BaudRate
        {
            get => serial.BaudRate;
            set => serial.BaudRate = value; // the simulator has no baud rate: it is only kept
        }

        public int WriteTimeout
        {
            get => serial.WriteTimeout;
            set => serial.WriteTimeout = value;
        }

        public int ReadTimeout
        {
            get => serial.ReadTimeout;
            set => serial.ReadTimeout = value;
        }

        public void Open()
        {
            if (portName == SimulatorName)
            {
                var link = new SimulatorLink();
                link.DataAvailable += () => DataReceived?.Invoke(this, EventArgs.Empty);
                link.Open(); // throws SimulatorUnavailableException
                simulator = link;
            }
            else
            {
                serial.Open();
            }
        }

        public void Close()
        {
            if (simulator != null)
            {
                SimulatorLink link = simulator;
                simulator = null;
                link.Close();
            }
            else
            {
                serial.Close();
            }
        }

        public int BytesToRead => simulator != null ? simulator.Available : serial.BytesToRead;

        public int Read(byte[] buffer, int offset, int count)
        {
            return simulator != null ? simulator.Read(buffer, offset, count) : serial.Read(buffer, offset, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (simulator != null)
            {
                simulator.Write(buffer, offset, count);
            }
            else
            {
                serial.Write(buffer, offset, count);
            }
        }

        public void Write(string text)
        {
            if (simulator != null)
            {
                byte[] bytes = Encoding.Latin1.GetBytes(text);
                simulator.Write(bytes, 0, bytes.Length);
            }
            else
            {
                serial.Write(text);
            }
        }

        public string ReadExisting()
        {
            if (simulator == null)
            {
                return serial.ReadExisting();
            }
            byte[] bytes = new byte[Math.Max(0, simulator.Available)];
            int n = simulator.Read(bytes, 0, bytes.Length);
            return Encoding.Latin1.GetString(bytes, 0, n);
        }
    }

    /// <summary>
    /// The simulated machine: DynamiX_Simulator.dll runs the ESP32's firmware logic and a model of the dyno, and
    /// moves bytes in and out like a serial port does. A thread polls it and raises DataAvailable.
    /// </summary>
    internal sealed class SimulatorLink
    {
        private const string Dll = "DynamiX_Simulator";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int dynamix_sim_open();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void dynamix_sim_close();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int dynamix_sim_write([In] byte[] data, int length);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int dynamix_sim_read([Out] byte[] buffer, int capacity);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int dynamix_sim_available();

        private Thread? pump;
        private volatile bool stop;

        public bool IsOpen { get; private set; }

        public event Action? DataAvailable;

        public void Open()
        {
            try
            {
                if (dynamix_sim_open() != 1)
                {
                    throw new SimulatorUnavailableException("The simulator did not start");
                }
            }
            catch (DllNotFoundException ex)
            {
                throw new SimulatorUnavailableException("Simulator DLL not found: build DynamiX_Simulator (build.bat) and rebuild the HMI", ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new SimulatorUnavailableException("Simulator DLL is for another platform (64-bit needed)", ex);
            }

            IsOpen = true;
            stop = false;
            pump = new Thread(Pump) { IsBackground = true, Name = "Simulator link" };
            pump.Start();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            stop = true;
            pump?.Join(500);
            dynamix_sim_close();
        }

        private void Pump()
        {
            while (!stop)
            {
                try
                {
                    if (dynamix_sim_available() > 0)
                    {
                        DataAvailable?.Invoke();
                    }
                }
                catch (Exception)
                {
                    // a handler's problem must not stop the link
                }
                Thread.Sleep(5);
            }
        }

        public int Available => IsOpen ? dynamix_sim_available() : 0;

        public int Read(byte[] buffer, int offset, int count)
        {
            if (!IsOpen || count <= 0)
            {
                return 0;
            }
            byte[] temp = new byte[count];
            int n = dynamix_sim_read(temp, count);
            Array.Copy(temp, 0, buffer, offset, n);
            return n;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (!IsOpen || count <= 0)
            {
                return;
            }
            byte[] temp = new byte[count];
            Array.Copy(buffer, offset, temp, 0, count);
            dynamix_sim_write(temp, count);
        }
    }
}
