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
    public partial class SequencePanel : UserControl
    {

        private float stroke = 0;
        private bool strokeValid = false;

        public SequencePanel()
        {
            InitializeComponent();
            strokeValid = false;

            flowLayoutPanel1.Controls.Add(new SequenceItem(this));
        }

        public void UpdateStroke(float newStroke, bool isValid)
        {
            stroke = newStroke;
            strokeValid = isValid;
            foreach (SequenceItem item in flowLayoutPanel1.Controls)
            {
                item.UpdateStroke(stroke, strokeValid);
            }
        }

        public bool ValidateSequence(out List<float> sequence)
        {
            sequence = new List<float>();

            foreach(SequenceItem item in flowLayoutPanel1.Controls)
            {
                if(item.ValidateItem(out float speed, out float duration))
                {
                    sequence.Add(speed);
                    sequence.Add(duration);
                }
                else
                {
                    // TODO: make a proper error message
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds one GUI_send_sequence command per item, in the order they are shown.
        /// Payload: [order][number of cycles][Modbus frame setting the drive frequency (8 bytes)]
        /// Speed is read as Hz and duration as a number of cycles (rounded to a whole number).
        /// </summary>
        public bool MakeSequence(out List<CommandPacket> sequence)
        {
            sequence = new List<CommandPacket>();

            if (flowLayoutPanel1.Controls.Count > 255)
            {
                return false; // the order doesn't fit in one byte
            }

            int i = 0;
            foreach (SequenceItem item in flowLayoutPanel1.Controls)
            {
                if (!item.ValidateItem(out float speed, out float duration))
                {
                    // TODO: make a proper error message
                    sequence.Clear();
                    return false;
                }

                byte[] driveCommand = DriveCommands.BuildSetFrequency(speed);

                byte[] payload = new byte[2 + driveCommand.Length];
                payload[0] = (byte)i;
                payload[1] = (byte)Math.Round(duration, MidpointRounding.AwayFromZero);
                driveCommand.CopyTo(payload, 2);

                // the encoder sends each char as one byte, so Latin1 keeps all 256 values intact
                sequence.Add(new CommandPacket(CommandFlags.GUI_send_sequence, Encoding.Latin1.GetString(payload)));
                i++;
            }

            return true;
        }


        /// <summary> The steps as a plan (target speed in mm/s, drive Hz, cycles), in the order they are sent. </summary>
        public bool GetPlan(out List<(double targetMmS, double hz, int cycles)> plan)
        {
            plan = new List<(double, double, int)>();
            foreach (SequenceItem item in flowLayoutPanel1.Controls)
            {
                if (!item.ReadPlan(out double targetMmS, out double hz, out int cycles))
                {
                    plan.Clear();
                    return false;
                }
                plan.Add((targetMmS, hz, cycles));
            }
            return true;
        }

        public void MoveUp(SequenceItem item)
        {
            if (flowLayoutPanel1.Controls.Contains(item))
            {
                int id = flowLayoutPanel1.Controls.IndexOf(item);
                if (id > 0)
                {
                    flowLayoutPanel1.Controls.SetChildIndex(item, id - 1);
                }
            }
        }

        public void MoveDown(SequenceItem item)
        {
            if (flowLayoutPanel1.Controls.Contains(item))
            {
                int id = flowLayoutPanel1.Controls.IndexOf(item);
                if (id < flowLayoutPanel1.Controls.Count - 1)
                {
                    flowLayoutPanel1.Controls.SetChildIndex(item, id + 1);
                }
            }

        }

        public void RemoveItem(SequenceItem item)
        {
            if (flowLayoutPanel1.Controls.Contains(item))
            {
                flowLayoutPanel1.Controls.Remove(item);
            }
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            //flowLayoutPanel1.Controls.Add(new SequenceItem(this));
            flowLayoutPanel1.Controls.Add(new SequenceItem(this, stroke, strokeValid));
            
        }


    }
}
