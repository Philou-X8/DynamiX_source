using DynamiX_HMI.Grapher;
using DynamiX_HMI.Analysis;
using DynamiX_HMI.Config;
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

        // Heartbeat, both ways, from one timer on the UI thread (so a frozen GUI stops pinging):
        //  - GUI -> ESP: GUI_heartbeat every HeartbeatPeriodMs. The ESP stops the motor after 300 ms of
        //    silence (GUI_TIMEOUT_MS in config.h): a late tick is a stopped run.
        //  - ESP -> GUI: the ESP sends live data every 200 ms, in every state. Nothing from it for
        //    EspTimeoutMs and the port is closed and the machine goes to Disconnected.
        private const int HeartbeatPeriodMs = 200;
        private const int EspTimeoutMs = 1000;
        private System.Windows.Forms.Timer heartbeatTimer;

        // What the ESP32 measured, kept for the next run: the TAR (empty load, stroke) and the calibration cycle
        private RunCalibration runCalibration = new RunCalibration();
        private double tarStrokeMm; // 0 = no TAR yet
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

            logger = new Logger(buttonConnection, labelDebugMessage, labelStatus);

            graphController = new GraphController(runPanel1, graphPanel1, logger.SetMessage);

            machineController = new MachineController(serialController);

            // The options page takes the place of the graphs: the Options button swaps them
            rightGridLayout.Controls.Remove(optionPanel1);
            centerGridLayout.Controls.Add(optionPanel1, 0, 1);
            optionPanel1.Visible = false;
            rightGridLayout.RowStyles[1].Height = 0;
            optionPanel1.SettingsChanged += OnSettingsChanged;
            mainSplitContainerRight.SplitterDistance = Math.Max(200, mainSplitContainerRight.Width - 340); // room for the run list
            mainSplitContainerLeft.SplitterDistance = Math.Max(mainSplitContainerLeft.Panel1MinSize, 345); // room for the sequence table



            heartbeatTimer = new System.Windows.Forms.Timer();
            heartbeatTimer.Interval = HeartbeatPeriodMs;
            heartbeatTimer.Tick += new EventHandler(HeartbeatTimer_Tick);
            heartbeatTimer.Start(); // always on: it only does something while the machine is connected


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
            // converted to mm, N and s inside (raw counts if conversion is off in the options)
            graphController.AddRawPoints(points);
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

                case CommandFlags.ESP_not_connected:
                    // the state machine below sends the GUI back to Connecting (new handshake)
                    logger.SetMessage("ESP32 dropped the link (restarted, or it lost the GUI): reconnecting");
                    break;

                case CommandFlags.ESP_tar_data:
                    SetTarData(command);
                    break;

                case CommandFlags.ESP_calibration_data:
                    SetCalibrationData(command);
                    break;

                case CommandFlags.ESP_sequence_separator:
                    if (!graphController.TryAddSequenceSeparator(command, temperatureData))
                    {
                        logger.SetMessage("ERROR: bad sequence separator (" + command.payload.Length + " bytes)");
                    }
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
            // the options page and the graphs share the same place
            bool showOptions = !optionPanel1.Visible;
            optionPanel1.Visible = showOptions;
            graphPanel1.Visible = !showOptions;
            if (showOptions)
            {
                optionPanel1.BringToFront();
            }
            buttonOptions.Text = showOptions ? "Graphs" : "Options";
        }

        /// <summary> A setting changed on the options page (already saved): analysis, graphs and units follow. </summary>
        private void OnSettingsChanged()
        {
            graphController.SettingsChanged();
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

        /// <summary>
        /// The link dropped during a run: keep the points received so far, if any, as the run's result.
        /// </summary>
        private void FinishRunIfInProgress()
        {
            if (graphController.RunInProgress)
            {
                graphController.FinishRun();
            }
        }

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

            FinishRunIfInProgress();

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

            FinishRunIfInProgress(); // the ESP lost the link in the middle of a run

            buttonConnection.Text = "Connected: Unknown";
            logger.SetState("State Connecting");
            serialPanel.StartConnectionTimeout(); // no answer in 2 s: the panel closes the port
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
            // Actions: tell the user how to get out (the heartbeat goes on: the ESP still needs it)
            // Transition: Idle on <ESP_emergency_clear>
            logger.SetState("State PANIC");
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
                logger.SetMessage("ERROR: " + sequencePanel1.ErrorText + ". Run not requested");
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

            // A new run in the run list: the plan is what is sent to the ESP, the calibration what it just measured
            if (!sequencePanel1.GetPlan(out List<(double targetMmS, double hz, int cycles)> plan))
            {
                plan = new List<(double, double, int)>();
            }
            graphController.BeginRun(plan, runCalibration.Clone(), temperatureData);

            // Send every step of the sequence (CommandFlags.GUI_send_sequence), then the start command.
            if (!sequencePanel1.MakeSequence(out List<CommandPacket> sequence))
            {
                // Don't start with a sequence the ESP can't run: tell it to drop the run
                logger.SetMessage("ERROR: " + sequencePanel1.ErrorText + ". Run not started");
                serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_abort));
                return;
            }

            foreach (CommandPacket packet in sequence)
            {
                serialController.SendGuiCommand(packet);
            }

            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_run_start, ""));


        }
        private void EnterStateRunning()
        {
            // Condition: AwaitingStart
            // Trigger: <start_permition> recieved from ESP32
            // Actions: Set graphs to live view mode
            // Transition: Running state
            logger.SetState("State Running");
        }
        private void EnterStateFinished()
        {
            // Condition: Running 
            // Trigger: <run_finished> sent from ESP
            // Actions: Process the run data 
            // Transition: Idle State
            logger.SetState("State Finished");

            graphController.FinishRun();

            HandleSerialCommand(new CommandPacket(CommandFlags.GUI_run_completed, "Run finished, graph ready"));

        }

        // --------------------------------------------------------
        // Heartbeat
        // --------------------------------------------------------

        private void HeartbeatTimer_Tick(object? sender, EventArgs e)
        {
            // Only once the ESP answered the handshake. Before that the connection panel's own
            // timeout decides, and the ESP may still be starting up.
            if (!machineConnected || !serialController.IsConnected())
            {
                return;
            }

            if (serialController.SilenceMs > EspTimeoutMs)
            {
                EspLost();
                return;
            }

            serialController.SendGuiCommand(new CommandPacket(CommandFlags.GUI_heartbeat));
        }

        /// <summary>
        /// Nothing came from the ESP for EspTimeoutMs (unplugged, crashed, restarting): close the port
        /// and send the machine to Disconnected. The drive has its own timeout to stop the motor.
        /// </summary>
        private void EspLost()
        {
            const string message = "ESP32 stopped responding";
            serialController.Disconnect(out _);
            serialPanel.ConnectionLost(message);
            SafeHandleSerialCommand(new CommandPacket(CommandFlags.GUI_disconnected, message + ": disconnected"));
        }

        // --------------------------------------------------------
        // 
        // --------------------------------------------------------

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
            AppSettings set = AppSettings.Current;
            labelPosVal.Text = set.PositionMm(position).ToString("0.##");
            labelForceVal.Text = set.ForceN(force).ToString("0.##");
            optionPanel1.SetLiveRaw(position, force);
            graphController.AddLiveData(position, force);
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
                float min = (float)set.PositionMm(posMin);
                float max = (float)set.PositionMm(posMax);
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
            AppSettings set = AppSettings.Current;
            runCalibration.EmptyForceN = set.ForceN(rawEmptyF);
            tarStrokeMm = Math.Abs(set.PositionMm(rawTopP) - set.PositionMm(rawBotP)); // abs: a negative scale swaps the ends

            sequencePanel1.UpdateStroke((float)tarStrokeMm, true);
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

            AppSettings set = AppSettings.Current;
            int rawMin = DecodePayloadInt(command.payload, 0);
            int rawMax = DecodePayloadInt(command.payload, 3);
            int rawBottom = set.BottomAtHighPosition ? rawMax : rawMin; // which end of the sensor range is the bottom
            int rawTop = set.BottomAtHighPosition ? rawMin : rawMax;
            runCalibration.BottomMm = set.PositionMm(rawBottom);
            runCalibration.TopMm = set.PositionMm(rawTop);
            runCalibration.BottomForceN = set.ForceN(DecodePayloadInt(command.payload, 6));
            runCalibration.TopForceN = set.ForceN(DecodePayloadInt(command.payload, 9));

            // display calibration data
            labelForceMinVal.Text = runCalibration.BottomForceN.ToString("0.##");
            labelForceMaxVal.Text = runCalibration.TopForceN.ToString("0.##");

            // send a warning if the stroke is really far from the one saved in the metadata
            // (the TAR stroke; skipped if there was no TAR, the stroke is still 0)
            double stroke = runCalibration.StrokeMm;
            double tarStroke = tarStrokeMm;
            if (tarStroke > 0 && Math.Abs(stroke - tarStroke) > StrokeMismatchTolerance * tarStroke)
            {
                logger.SetMessage("Warning: Calibration stroke missmatch from tar");
            }
        }

    }
}
