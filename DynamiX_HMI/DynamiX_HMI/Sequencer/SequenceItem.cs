using DynamiX_HMI.Serial;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DynamiX_HMI.Sequencer
{
    public partial class SequenceItem : UserControl
    {
        SequencePanel parent;

        private float stroke = 0;
        private bool strokeValid = false;

        public SequenceItem(SequencePanel parentPanel)
        {
            InitializeComponent();
            parent = parentPanel;
            stroke = 0;
            strokeValid = false;

            textBoxSpeed.Leave += (s, e) => UpdateSpeedDisplay();
            textBoxSpeed.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // no "ding"
                    UpdateSpeedDisplay();
                }
            };
        }

        public SequenceItem(SequencePanel parentPanel, float newStroke, bool isValid)
        {
            InitializeComponent();
            parent = parentPanel;
            stroke = newStroke;
            strokeValid = isValid;

            textBoxSpeed.Leave += (s, e) => UpdateSpeedDisplay();
            textBoxSpeed.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // no "ding"
                    UpdateSpeedDisplay();
                }
            };
        }

        public void UpdateStroke(float newStroke, bool isValid)
        {
            stroke = newStroke;
            strokeValid = isValid;
            UpdateSpeedDisplay();
        }

        public void UpdateSpeedDisplay()
        {
            try
            {
                float speedMps = ParseNumber(textBoxSpeed.Text);

                if (strokeValid && TangentSpeedToDrive(speedMps, stroke, out float speedRPM, out float speedDrive))
                {
                    labelSpdRPM.Text = "Speed RPM: " + speedRPM.ToString("0.##") + " rpm";
                    labelSpdDrive.Text = "Speed Drive: " + speedDrive.ToString("0.##") + " Hz";
                }
                else
                {
                    labelSpdRPM.Text = "Speed RPM: ?";
                    labelSpdDrive.Text = "Speed Drive: ?";
                }
            }
            catch
            {
                labelSpdRPM.Text = "Speed RPM: ??";
                labelSpdDrive.Text = "Speed Drive: ??";
            }


            
        }

        public bool ValidateItem(out float speed, out float duration)
        {
            speed = 0f;
            duration = 0f;
            bool isValid = true;

            try
            {
                // Always read numbers with a '.' decimal point (a ',' is accepted too), whatever
                // the PC's regional settings are: "10.0" fails to parse on a decimal-comma locale.
                duration = ParseNumber(textBoxDuration.Text);
                float speedRaw = ParseNumber(textBoxSpeed.Text);

                if (!strokeValid || !TangentSpeedToDrive(speedRaw, stroke, out _, out speed))
                {
                    return false;
                }

                // For now: speed is a drive frequency in Hz, duration is a number of
                // cycles (sent as one byte, so a whole number from 1 to 255 after rounding)
                if (speed < 0f || speed > (float)DriveCommands.MaxHz)
                {
                    isValid = false;
                }
                if (Math.Round(duration, MidpointRounding.AwayFromZero) < 1 || Math.Round(duration, MidpointRounding.AwayFromZero) > 255)
                {
                    isValid = false;
                }
            }
            catch
            {
                return false;
            }
            

            return isValid;
        }

        private static float ParseNumber(string text)
        {
            return float.Parse(text.Trim().Replace(',', '.'),
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture);
        }

        // Overall reduction from the drive's output frequency to the yoke's rotation:
        // 2:1 drive-to-motor, then 5:1 belt. The yoke turns at (drive Hz / 10) rev/s.
        private const double DriveToYokeRatio = 2.0 * 5.0;

        /// <summary>
        /// Tangent speed of the yoke crank pin (m/s) to yoke rpm and the drive frequency (Hz).
        /// The crank radius is half the stroke, so v = w * (stroke / 2), which gives
        /// yoke rev/s = v / (pi * stroke), and drive Hz = yoke rev/s * 10.
        /// strokeMm is the stroke in mm (what the GUI shows); the speed is in m/s.
        /// Returns false if the stroke isn't usable (zero, negative, NaN).
        /// </summary>
        private static bool TangentSpeedToDrive(float speedMps, float strokeMm, out float yokeRpm, out float driveHz)
        {
            yokeRpm = 0;
            driveHz = 0;
            if (!(strokeMm > 0f))
            {
                return false;
            }
            double strokeM = strokeMm / 1000.0;
            double yokeHz = speedMps / (Math.PI * strokeM);
            yokeRpm = (float)(yokeHz * 60.0);
            driveHz = (float)(yokeHz * DriveToYokeRatio);
            return true;
        }

        private void buttonUp_Click(object sender, EventArgs e)
        {
            parent.MoveUp(this);
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            parent.MoveDown(this);
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            parent.RemoveItem(this);
        }
    }
}
