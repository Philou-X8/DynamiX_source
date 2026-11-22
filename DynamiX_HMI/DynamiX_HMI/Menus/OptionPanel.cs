using DynamiX_HMI.Grapher;
using DynamiX_HMI.Sequencer;
using DynamiX_HMI.Serial;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DynamiX_HMI.Menus
{
    /// <summary>
    /// Options page: the sensor calibrations (two points each, X = raw counts, Y = mm or N),
    /// the force failsafe and the ESP reboot.
    ///
    /// - Incoming data (points, live data) is converted with the calibrations only while
    ///   checkBoxConvert is checked; unchecked, the raw counts go through unchanged (debugging).
    /// - The force failsafe always uses the force calibration, whatever the check box says.
    ///   It is sent to the ESP as a raw window: the counts for -failsafe N and +failsafe N.
    ///
    /// A text box that doesn't hold a valid number turns red and the previous value is kept.
    /// Everything runs on the UI thread.
    /// </summary>
    public partial class OptionPanel : UserControl
    {
        private const int Int24Min = -8388608;
        private const int Int24Max = 8388607;

        private Action<CommandPacket>? serialCommandHandle;

        // From the text boxes (the last valid values)
        private LinearCalibration positionCalibration = LinearCalibration.Identity;
        private LinearCalibration forceCalibration = LinearCalibration.Identity;
        private double failsafeNewton = 3000.0;

        // What incoming data goes through: the calibrations, or Y = X when conversion is off
        private LinearCalibration positionConverter = LinearCalibration.Identity;
        private LinearCalibration forceConverter = LinearCalibration.Identity;

        private (int min, int max)? lastSentFailsafe;

        public int staringSize = 0;

        public OptionPanel()
        {
            InitializeComponent();
            staringSize = groupBoxOptions.Size.Height + 4;

            TextBox[] calibrationBoxes =
            {
                textBoxCalibPosX1, textBoxCalibPosY1, textBoxCalibPosX2, textBoxCalibPosY2,
                textBoxCalibForceX1, textBoxCalibForceY1, textBoxCalibForceX2, textBoxCalibForceY2,
                textBoxFailsafe,
            };
            foreach (TextBox box in calibrationBoxes)
            {
                box.Leave += (s, e) => UpdateCalibration();
                box.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true; // no "ding"
                        UpdateCalibration();
                    }
                };
            }
            checkBoxConvert.CheckedChanged += (s, e) => UpdateCalibration();

            UpdateCalibration(); // the default values work from the start
        }

        /// <summary> Where the panel's commands go (the serial link to the ESP). </summary>
        public void SetSerialHandle(Action<CommandPacket> commandDelegate)
        {
            serialCommandHandle = commandDelegate;
        }

        /// <summary>
        /// Reads every text box and updates the conversions. Called when a box is left or Enter
        /// is pressed, and when the check box changes. Sends the failsafe if its window changed.
        /// </summary>
        public void UpdateCalibration()
        {
            if (TryReadCalibration(textBoxCalibPosX1, textBoxCalibPosY1, textBoxCalibPosX2, textBoxCalibPosY2,
                                   out LinearCalibration position))
            {
                positionCalibration = position;
            }
            if (TryReadCalibration(textBoxCalibForceX1, textBoxCalibForceY1, textBoxCalibForceX2, textBoxCalibForceY2,
                                   out LinearCalibration force))
            {
                forceCalibration = force;
            }
            bool failsafeOk = TryReadNumber(textBoxFailsafe, out double failsafe) && failsafe > 0;
            MarkValid(textBoxFailsafe, failsafeOk);
            if (failsafeOk)
            {
                failsafeNewton = failsafe;
            }

            positionConverter = checkBoxConvert.Checked ? positionCalibration : LinearCalibration.Identity;
            forceConverter = checkBoxConvert.Checked ? forceCalibration : LinearCalibration.Identity;

            if (GetRawForceFailsafe(out int minForce, out int maxForce) && lastSentFailsafe != (minForce, maxForce))
            {
                SendFailsafe();
            }
        }

        /// <summary>
        /// The failsafe as a raw window: the counts that read -failsafe N and +failsafe N on the
        /// force calibration (not affected by checkBoxConvert). Clamped to the 24-bit range.
        /// </summary>
        public bool GetRawForceFailsafe(out int minForce, out int maxForce)
        {
            double a = forceCalibration.ToRaw(-failsafeNewton);
            double b = forceCalibration.ToRaw(failsafeNewton);
            minForce = ClampInt24(Math.Min(a, b)); // a negative slope swaps the ends
            maxForce = ClampInt24(Math.Max(a, b));
            return minForce < maxForce;
        }

        /// <summary> Sends GUI_set_failsafe: [min][max], int24 big-endian. Also call it on each connection. </summary>
        public void SendFailsafe()
        {
            if (!GetRawForceFailsafe(out int minForce, out int maxForce))
            {
                return;
            }
            byte[] payload =
            {
                (byte)(minForce >> 16), (byte)(minForce >> 8), (byte)minForce,
                (byte)(maxForce >> 16), (byte)(maxForce >> 8), (byte)maxForce,
            };
            // The encoder sends each char as one byte, so Latin1 keeps all 256 values intact.
            serialCommandHandle?.Invoke(new CommandPacket(CommandFlags.GUI_set_failsafe, Encoding.Latin1.GetString(payload)));
            lastSentFailsafe = (minForce, maxForce);
        }

        /// <summary> Raw position (counts) to mm, or unchanged when conversion is off. </summary>
        public float ConvertPosition(int raw)
        {
            return positionConverter.ToUnits(raw);
        }

        /// <summary> Raw force (counts) to N, or unchanged when conversion is off. </summary>
        public float ConvertForce(int raw)
        {
            return forceConverter.ToUnits(raw);
        }

        /// <summary> Time is always converted: microseconds to seconds. </summary>
        public DataPointUnit ConvertPoint(DataPointRaw rawPoint)
        {
            return new DataPointUnit(DataPointUnit.MicrosToSeconds(rawPoint.time),
                                     forceConverter.ToUnits(rawPoint.force),
                                     positionConverter.ToUnits(rawPoint.position));
        }

        public List<DataPointUnit> ConvertPoints(List<DataPointRaw> rawPoints)
        {
            List<DataPointUnit> points = new List<DataPointUnit>(rawPoints.Count);
            foreach (DataPointRaw p in rawPoints)
            {
                points.Add(ConvertPoint(p));
            }
            return points;
        }

        private static bool TryReadCalibration(TextBox x1Box, TextBox y1Box, TextBox x2Box, TextBox y2Box,
                                               out LinearCalibration calibration)
        {
            bool x1Ok = TryReadNumber(x1Box, out double x1);
            bool y1Ok = TryReadNumber(y1Box, out double y1);
            bool x2Ok = TryReadNumber(x2Box, out double x2);
            bool y2Ok = TryReadNumber(y2Box, out double y2);
            calibration = LinearCalibration.Identity;
            bool allNumbers = x1Ok && y1Ok && x2Ok && y2Ok;
            bool lineOk = allNumbers && LinearCalibration.TryFromPoints(x1, y1, x2, y2, out calibration);

            // four valid numbers that don't make a line (same X or same Y): all four turn red
            bool noLine = allNumbers && !lineOk;
            MarkValid(x1Box, x1Ok && !noLine);
            MarkValid(y1Box, y1Ok && !noLine);
            MarkValid(x2Box, x2Ok && !noLine);
            MarkValid(y2Box, y2Ok && !noLine);
            return lineOk;
        }

        /// <summary> A number with a decimal point or a decimal comma. </summary>
        private static bool TryReadNumber(TextBox box, out double value)
        {
            string text = box.Text.Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && double.IsFinite(value);
        }

        private static void MarkValid(TextBox box, bool valid)
        {
            box.BackColor = valid ? SystemColors.Window : Color.MistyRose;
        }

        private static int ClampInt24(double value)
        {
            return (int)Math.Round(Math.Clamp(value, Int24Min, Int24Max));
        }

        private void buttonReboot_Click(object sender, EventArgs e)
        {
            serialCommandHandle?.Invoke(new CommandPacket(CommandFlags.GUI_force_reboot));
        }
    }
}
