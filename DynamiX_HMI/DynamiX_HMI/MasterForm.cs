using DynamiX_HMI.Grapher;
using DynamiX_HMI.Menus;
using DynamiX_HMI.Sequencer;
using DynamiX_HMI.Serial;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;


namespace DynamiX_HMI
{
    public partial class MasterForm : Form
    {
        //private SerialForm serialForm;
        private SerialController serialController;
        private GraphController graphController;
        private MachineController machineController;
        private Logger logger;

        private bool machineConnected; // TODO: move this to machineController like the live graph flag

        private List<DataPointUnit> pointHistory = new List<DataPointUnit>();
        private string lastMessage;

        private System.Windows.Forms.Timer myTimer;

        private DataCalibration calibrationMetadata;
        // Calibration stroke may differ from the TAR stroke by this fraction before we warn
        private const float StrokeMismatchTolerance = 0.05f;
        private float temperatureData;

        public MasterForm()
        {
            // GUI Setup
            InitializeComponent();
            mainSplitContainerLeft.SplitterMoved += (s, e) => ActiveControl = null;
            mainSplitContainerRight.SplitterMoved += (s, e) => ActiveControl = null;

            // Backend setup
            machineConnected = false;

            serialController = new SerialController();
            serialController.SetGuiHandle(HandleSerialData, HandleSerialCommand);
            serialPanel.BindSerialController(serialController);
            serialPanel.SetGuiHandle(HandleSerialCommand);

            optionPanel1.SetSerialHandle(serialController.SendGuiCommand); // failsafe and reboot go straight to the ESP

            graphController = new GraphController(runPanel1, graphPanel1);

            machineController = new MachineController(serialController);

            logger = new Logger(buttonConnection, labelDebugMessage, labelStatus);

            calibrationMetadata = new DataCalibration();

            //serialForm = new SerialForm();

            //serialForm.TopLevel = false;
            //leftGridLayout.Controls.Add(serialForm, 0, 0);
            //serialForm.Dock = DockStyle.Fill;
            ////flowLayoutPanel1.Width = serialForm.Width;
            ////serialForm.BringToFront();
            //serialForm.Show();

            myTimer = new System.Windows.Forms.Timer();
            myTimer.Interval = 100; // in ms
            myTimer.Tick += new EventHandler(MyTimer_Tick);
            //myTimer.Start(); // Start the timer



            //serialForm.SetTestDelegate(TextHandle);
        }


        /// <summary>
        /// SerialController must pass through this function to switch threading context
        /// </summary>
        public void HandleSerialData(List<DataPointRaw> points)
        {
            //graphController.AddPoints(points);
            try { this.BeginInvoke(SafeHandleSerialData, points); } catch { }
        }
        /// <summary> Must only be invoked by HandleSerialData() </summary>
        private void SafeHandleSerialData(List<DataPointRaw> points)
        {
            // mm, N and s from here on (raw counts if conversion is off in the options)
            graphController.AddPoints(optionPanel1.ConvertPoints(points));
        }


        /// <summary>
        /// SerialController must pass through this function to switch threading context
        /// </summary>
        public void HandleSerialCommand(CommandPacket command)
        {
            try { this.BeginInvoke(SafeHandleSerialCommand, command); } catch { }

        }
        /// <summary> Must only be invoked by HandleSerialCommand() </summary>
        private void SafeHandleSerialCommand(CommandPacket command)
        {

            switch (command.flag)
            {
                // RETURN cases 

                case CommandFlags.ESP_live_data:
                    SetLiveData(command);
                    return;

                case CommandFlags.GUI_force_reboot:
                    logger.SetMessage("Rebooting ESP32");
                    serialController.SendGuiCommand(command); // CommandFlags.GUI_force_reboot
                    return;

                // BREAK cases

                case CommandFlags.GUI_abort:
                    logger.SetMessage("Run Aborted");
                    serialController.SendGuiCommand(command); // CommandFlags.GUI_abort
                    break;

                case CommandFlags.ESP_tar_data:
                    SetTarData(command);
                    break;

                case CommandFlags.ESP_calibration_data:
                    SetCalibrationData(command);
                    break;

                case CommandFlags.ESP_sequence_separator:

                    calibrationMetadata.temperatureEnd = temperatureData;
                    // TODO: add the cachedCalibration to the parameters
                    if (!graphController.TryAddSequenceSeparator(command))
                    {
                        logger.SetMessage("ERROR: bad sequence separator (" + command.payload.Length + " bytes)");
                    }

                    // this end temperature is the start temperature of the next sequence
                    calibrationMetadata.temperatureStart = temperatureData;
                    break;

                default:
                    if (command.payload.Length > 0)
                    {
                        logger.SetMessage(command.payload);
                    }
                    break;
            }


            switch (machineController.CommandTransition(command))
            {
                case StateTransition.Disconnected:
                    EnterStateDisconnected();
                    break;
                case StateTransition.Connecting:
                    EnterStateConnecting();
                    break;
                case StateTransition.Connected:
                    EnterStateConnected();
                    break;
                case StateTransition.Tar:
                    EnterStateTar();
                    break;
                case StateTransition.Panic:
                    EnterStatePanic();
                    break;
                case StateTransition.Requesting:
                    EnterStateRequesting();
                    break;
                case StateTransition.Calibrating:
                    EnterStateCalibrating();
                    break;
                case StateTransition.Starting:
                    EnterStateStarting();
                    break;
                case StateTransition.Running:
                    EnterStateRunning();
                    break;
                case StateTransition.Finished:
                    EnterStateFinished();
                    break;
                case StateTransition.None:  // do nothing
                default:                    // do nothing
                    break;
            }
        }


        // --------------------------------------------------------
        // Button Clicks
        // --------------------------------------------------------

        private void ButtonConnection_Click(object sender, EventArgs e)
        {
            if (serialPanel.Visible)
            {
                serialPanel.HidePanel();

                // TODO: Redo this logic so it writes the proper text
                if (serialController.TryGetName(out string name))
                {
                    buttonConnection.Text = name;
                }
                else
                {
                    buttonConnection.Text = "Not Connected";
                }
            }
            else
            {
                serialPanel.OpenPanel();
            }
        }

        private void buttonOptions_Click(object sender, EventArgs e)
        {
            if (optionPanel1.Visible)
            {
                optionPanel1.Hide();
                rightGridLayout.RowStyles[1].Height = 0;
            }
            else
            {
                optionPanel1.Show();
                rightGridLayout.RowStyles[1].Height = optionPanel1.staringSize;
            }
        }

        private void buttonTar_Click(object sender, EventArgs e)
        {
            if (!machineConnected)
            {
                logger.SetMessage("DENIED: Machine not connected");
                return;
            }
            if (!machineController.IsIdle)
            {
                logger.SetMessage("DENIED: TAR only from Idle");
                return;
            }
            HandleSerialCommand(new CommandPacket(CommandFlags.GUI_TAR_request, "")); // Idle -> Tar
        }

        private void buttonMachineStart_Click(object sender, EventArgs e)
        {
            if (!machineConnected)
            {
                logger.SetMessage("DENIED: Machine not connected");
            }
            HandleSerialCommand(new CommandPacket(CommandFlags.GUI_click_start, ""));
            //machineController.StartMachine();
            //serialController.startcommunication...
            //serialController.HandleGuiCommand(0, "1\n");
        }

        private void buttonMachineStop_Click(object sender, EventArgs e)
        {
            HandleSerialCommand(new CommandPacket(CommandFlags.GUI_abort, ""));
            //machineController.StopMachine();
            //serialController.HandleGuiCommand(0, "0\n");
        }

        // --------------------------------------------------------
        // State transitions
        // --------------------------------------------------------

        // 
        // State transition handlers
        //
        private void EnterStateDisconnected()
        {
            // Condition: Any
            // Trigger:
            // Actions:
            // Transition:

            machineConnected = false;

            buttonConnection.Text = "Not Connected";
            logger.SetState("State Disconnected");

        }

        // 
        // State transition handlers
        //
        private void EnterStateConnecting()
        {
            // Condition: Any
            // Trigger:
            // Actions:
            // Transition:

            machineConnected = false;

            buttonConnection.Text = "Connected: Unknown";
            logger.SetState("State Connecting");
            // TODO
            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_check_connection));
        }
        private void EnterStateConnected()
        {
            // Condition: Disconnected
            // Trigger: <connection_acknowledge> recieved from ESP32
            // Actions: Display updated connection message
            // Transition: Idle state
            serialPanel.ConnectionConfirmed(); // stop timeout timer
            machineConnected = true;

            buttonConnection.Text = "Connected: Shock Dyno";
            logger.SetState("State Idle");

            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_abort)); // TODO: doublecheck
            // a lot of functions trigger the GUI_abort to return to idle
            // which already sends a abort flag
            // maybe remove the one here to avoid double abort message
            // also: to send an abort on first connection, check for a ESP_good_connection instead

            optionPanel1.SendFailsafe(); // the ESP may have rebooted with its default window
        }
        private void EnterStateTar()
        {
            // Condition: Idle
            // Trigger: [TAR] button
            // Actions: send the TAR request to the ESP
            // Transition: Idle on <ESP_tar_data> (TAR done), <ESP_run_denied> (refused) or Stop
            logger.SetState("State TAR");
            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_TAR_request));
        }

        private void EnterStatePanic()
        {
            // Condition: any connected state
            // Trigger: <ESP_emergency> (the ESP has already stopped the motor)
            // Actions: stop the live view, tell the user how to get out
            // Transition: Idle on <ESP_emergency_clear>
            logger.SetState("State PANIC");
            myTimer.Stop();
            logger.SetMessage("PANIC: motor stopped. Fix the cause, press the yellow reset button on the machine, "
                              + "then Stop if the panic doesn't clear by itself");
        }

        private void EnterStateRequesting()
        {
            // Condition: Connected
            // Trigger: [Start] button has been clicked
            // Actions: Check wave formats, send run request to ESP
            // Transition: Requesting state
            logger.SetState("State Requesting");

            // ---------- Validating Run Configuration
            // Validate Callibration 

            // TODO


            // Validating sequence
            int stepCount = 0;
            if (sequencePanel1.MakeSequence(out List<CommandPacket> sequence))
            {
                // Good sequence
                logger.SetMessage("Good configuration sequence");
                stepCount = sequence.Count;
            }
            else
            {
                // Speed: a number from 0 to 60 (Hz). Duration: a whole number of cycles, 1 to 255.
                logger.SetMessage("ERROR: Bad configuration sequence (speed 0-" + DriveCommands.MaxHz
                                  + " Hz, cycles 1-255), run not requested");
                HandleSerialCommand(new CommandPacket(CommandFlags.GUI_abort, "")); // back to Idle
                return;
            }


            // ---------- Validating Run Configuration --- end

            // The payload is the number of GUI_send_sequence steps that will follow, so the ESP
            // can ready its queue and check at the start that every step arrived.
            // (the encoder sends each char as one byte)
            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_run_request, ((char)stepCount).ToString()));
        }

        private void EnterStateCalibrating()
        {
            logger.SetState("State Calibrating");

            // The force failsafe is no longer part of it: OptionPanel sends it (GUI_set_failsafe)
            // on each connection and whenever it changes.
            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_calibrate));

        }


        private void EnterStateStarting()
        {
            // Condition: Requested
            // Trigger: <ESP_run_accepted>
            // Actions: set up point reception buffer, set graph to live mode, send wave, send start
            // Transition: AwaitingStart state
            logger.SetState("State Starting");

            // Fresh point buffer for this run
            graphController.BeginRun();

            // Send every step of the sequence (CommandFlags.GUI_send_sequence), then the start command.
            if (!sequencePanel1.MakeSequence(out List<CommandPacket> sequence))
            {
                // Don't start with a sequence the ESP can't run: tell it to drop the run
                logger.SetMessage("ERROR: Bad configuration sequence, run not started");
                serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_abort));
                return;
            }

            foreach (CommandPacket packet in sequence)
            {
                serialController.SendGuiCommand(packet);
            }

            calibrationMetadata.temperatureStart = temperatureData;

            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_run_start, ""));


        }
        private void EnterStateRunning()
        {
            // Condition: AwaitingStart
            // Trigger: <start_permition> recieved from ESP32
            // Actions: Set graphs to live view mode
            // Transition: Running state
            logger.SetState("State Running");

            myTimer.Start();
        }
        private void EnterStateFinished()
        {
            // Condition: Running 
            // Trigger: <run_finished> sent from ESP
            // Actions: Process the run data 
            // Transition: Idle State
            logger.SetState("State Finished");

            myTimer.Stop();

            graphController.FinishRun();

            HandleSerialCommand(new CommandPacket(CommandFlags.GUI_run_completed, "Run finished, graph ready"));

        }

        // --------------------------------------------------------
        // 
        // --------------------------------------------------------



        private void MyTimer_Tick(object sender, EventArgs e)
        {
            // Code to be executed every second
            //labelConnection.Text = graphController.GetPointsCount().ToString();
            //labelConnection.Text = lastMessage;
            //lastMessage = "";

            List<DataPointUnit> points = graphController.GetLatest(16);
            //label2.Text = "";
            String display_str = "";
            for (int i = 0; i < points.Count; i++)
            {
                display_str += "Point " + i.ToString() + ": "
                    + "t=" + points[i].time + " , "
                    + "f=" + points[i].force + " , "
                    + "p=" + points[i].position + "\n";
            }
            // The graphs are no longer drawn here: GraphController pushes new points to the GraphPanel as they arrive
        }


        private int DecodePayloadInt(string payload, int startByte)
        {
            // Convert 3 chars from the payload to int using the usual 24 bits encoding
            // (big-endian, two's complement). The decoder stores each wire byte as one char
            // (0-255), so the chars are the bytes. The caller checks the payload length.
            int u = ((payload[startByte] & 0xFF) << 16)
                  | ((payload[startByte + 1] & 0xFF) << 8)
                  |  (payload[startByte + 2] & 0xFF);
            return (u ^ 0x800000) - 0x800000; // sign-extend from bit 23
        }

        /// <summary>
        /// ESP_live_data, sent by the ESP every 200 ms: position, pos min, pos max, force, temperature.
        /// Each value is 24-bit two's complement, big-endian: raw counts (like the data points),
        /// except the temperature, in 0.01 degC.
        /// Min/max come from the latest TAR or calibration (0 if neither was done), the stroke is
        /// computed here. The ESP sends 0 for any value it doesn't have.
        /// </summary>
        private void SetLiveData(CommandPacket command)
        {
            if (command.payload.Length < 15)
            {
                logger.SetMessage("ERROR: bad live data (" + command.payload.Length + " bytes)");
                return;
            }
            int position = DecodePayloadInt(command.payload, 0);
            int posMin = DecodePayloadInt(command.payload, 3);
            int posMax = DecodePayloadInt(command.payload, 6);
            int force = DecodePayloadInt(command.payload, 9);
            int temperature = DecodePayloadInt(command.payload, 12);

            // mm and N (raw counts if conversion is off in the options)
            labelPosVal.Text = optionPanel1.ConvertPosition(position).ToString("0.##");
            labelForceVal.Text = optionPanel1.ConvertForce(force).ToString("0.##");
            if (posMin == 0 && posMax == 0)
            {
                // no TAR or calibration yet: converting 0 counts would show a made-up position
                labelPosMinVal.Text = "-";
                labelPosMaxVal.Text = "-";
                labelStrokeVal.Text = "-";
                sequencePanel1.UpdateStroke(0, false);
            }
            else
            {
                float min = optionPanel1.ConvertPosition(posMin);
                float max = optionPanel1.ConvertPosition(posMax);
                labelPosMinVal.Text = min.ToString("0.##");
                labelPosMaxVal.Text = max.ToString("0.##");
                labelStrokeVal.Text = Math.Abs(max - min).ToString("0.##");
            }
            // The ESP sends hundredths of a degree C (0 = no valid reading for 1 s)
            temperatureData = temperature / 100f;
            labelTempVal.Text = temperatureData.ToString("0.##");

        }

        private void SetTarData(CommandPacket command)
        {
            // ESP_tar_data payload: [pos min][pos max][empty force], 24-bit raw counts (9 bytes)
            if (command.payload.Length < 9)
            {
                logger.SetMessage("ERROR: bad TAR data (" + command.payload.Length + " bytes)");
                return;
            }

            int rawBotP = DecodePayloadInt(command.payload, 0);
            int rawTopP = DecodePayloadInt(command.payload, 3);
            int rawEmptyF = DecodePayloadInt(command.payload, 6);

            // Unit conversion (mm and N), or unchanged raw counts if it's off in the options
            float emptyF = optionPanel1.ConvertForce(rawEmptyF);
            float botP = optionPanel1.ConvertPosition(rawBotP);
            float topP = optionPanel1.ConvertPosition(rawTopP);

            calibrationMetadata.emptyForce = emptyF;
            calibrationMetadata.bottomPosition = botP;
            calibrationMetadata.topPosition = topP;
            calibrationMetadata.stroke = Math.Abs(topP - botP); // abs: a negative scale swaps the ends

            sequencePanel1.UpdateStroke(calibrationMetadata.stroke, true);
        }

        private void SetCalibrationData(CommandPacket command)
        {
            // ESP_calibration_data payload: [pos min][pos max][bottom force][top force],
            // 24-bit raw counts (12 bytes)
            if (command.payload.Length < 12)
            {
                logger.SetMessage("ERROR: bad calibration data (" + command.payload.Length + " bytes)");
                return;
            }

            float botP = optionPanel1.ConvertPosition(DecodePayloadInt(command.payload, 0));
            float topP = optionPanel1.ConvertPosition(DecodePayloadInt(command.payload, 3));
            calibrationMetadata.bottomForce = optionPanel1.ConvertForce(DecodePayloadInt(command.payload, 6));
            calibrationMetadata.topForce = optionPanel1.ConvertForce(DecodePayloadInt(command.payload, 9));

            // display calibration data
            labelForceMinVal.Text = calibrationMetadata.bottomForce.ToString("0.##");
            labelForceMaxVal.Text = calibrationMetadata.topForce.ToString("0.##");

            // send a warning if the stroke is really far from the one saved in the metadata
            // (the TAR stroke; skipped if there was no TAR, the stroke is still 0)
            float stroke = Math.Abs(topP - botP);
            float tarStroke = calibrationMetadata.stroke;
            if (tarStroke > 0 && Math.Abs(stroke - tarStroke) > StrokeMismatchTolerance * tarStroke)
            {
                logger.SetMessage("Warning: Calibration stroke missmatch from tar");
            }
        }

    }
}
