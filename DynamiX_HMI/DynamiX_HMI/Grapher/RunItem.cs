using DynamiX_HMI.Sequencer;
using DynamiX_HMI.Serial;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace DynamiX_HMI.Grapher
{
    

    public partial class RunItem : UserControl
    {
        private RunPanel parent;
        private Action UpdateGraphDelegate;

        private string runName;
        private DataCalibration metadata;
        private List<DataPointUnit> livePoints;
        private List<float> velocities; // computed once, the points of a run don't change

        private Color color;

        private List<string> colorList;


        public RunItem(RunPanel parentList, string displayName, List<DataPointUnit> rawPoints, Action updateGraphRef) 
            : this(parentList, displayName, rawPoints, new DataCalibration(), updateGraphRef)
        {
            
        }

        public RunItem(RunPanel parentList, string displayName, List<DataPointUnit> rawPoints, DataCalibration meta, Action updateGraphRef)
        {
            InitializeComponent();
            parent = parentList;
            UpdateGraphDelegate = updateGraphRef;
            groupBox1.Text = displayName;

            colorList = new List<string>
            {
                "Red",
                "Green",
                "Blue",
            };

            comboBoxColor.Items.AddRange(colorList.ToArray());
            comboBoxColor.SelectedIndex = comboBoxColor.Items.IndexOf("Red");
            comboBoxColor.SelectedIndexChanged += (s, e) => UpdateGraphDelegate?.Invoke();

            runName = displayName;
            metadata = meta;
            buttonSaveRun.Click += buttonSaveRun_Click;

            livePoints = rawPoints;
            velocities = Velocity.Compute(livePoints);
            // probably do some filtering and stuff over the recieved points
            // Or have the GraphController do the processing and send the good points here
            // This will put the difference between machine or file loaded points upstream
        }

        /// <summary>
        /// All the points of this run with the selected color, or null when the run
        /// is hidden (Show unchecked) or empty.
        /// </summary>
        public RunCurve? GetCurve()
        {
            if (!checkBoxVisible.Checked || livePoints.Count == 0)
            {
                return null;
            }
            return new RunCurve(livePoints, velocities, Color.FromName(comboBoxColor.Text));
        }

        public List<DataPointUnit> GetLatest(int count)
        {
            if (!checkBoxVisible.Checked)
            {
                return new List<DataPointUnit>();
            }

            if (livePoints.Count == 0)
            {
                //return Enumerable.Repeat(new DataPoint(0, 0, 0), count).ToList();
                return new List<DataPointUnit>();
            }

            if (livePoints.Count < count)
            {
                int n_filler = count - livePoints.Count;
                List<DataPointUnit> ret = Enumerable.Repeat(livePoints[0], n_filler).ToList();
                ret.AddRange(livePoints);
                return ret;
            }

            return livePoints.GetRange(livePoints.Count - count, count);
        }

        /// <summary> Folder the runs are saved in: next to the executable. </summary>
        public static string SaveFolder => Path.Combine(AppContext.BaseDirectory, "SavedRuns");

        /// <summary>
        /// Writes this run to SaveFolder as a CSV and returns the file path.
        /// Layout: "# key,value" metadata lines, then a "time_s,position,force" header,
        /// then one line per point. Numbers use '.' and no thousands separator, whatever
        /// the PC's regional settings are. Throws on I/O errors.
        /// </summary>
        public string SaveToFile()
        {
            Directory.CreateDirectory(SaveFolder);

            // run name -> safe file name, plus a timestamp so saving twice doesn't overwrite
            string safeName = string.Concat(runName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))
                                    .Replace(' ', '_');
            string path = Path.Combine(SaveFolder, safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            using (StreamWriter w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                w.WriteLine("# run," + runName);
                w.WriteLine("# saved," + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv));
                w.WriteLine("# units,mm and N if unit conversion is enabled in the options; otherwise raw ADC counts (time is always seconds)");
                w.WriteLine("# emptyForce," + metadata.emptyForce.ToString("R", inv));
                w.WriteLine("# bottomForce," + metadata.bottomForce.ToString("R", inv));
                w.WriteLine("# topForce," + metadata.topForce.ToString("R", inv));
                w.WriteLine("# bottomPosition," + metadata.bottomPosition.ToString("R", inv));
                w.WriteLine("# topPosition," + metadata.topPosition.ToString("R", inv));
                w.WriteLine("# stroke," + metadata.stroke.ToString("R", inv));
                w.WriteLine("# temperatureStart," + metadata.temperatureStart.ToString("R", inv));
                w.WriteLine("# temperatureEnd," + metadata.temperatureEnd.ToString("R", inv));
                w.WriteLine("# points," + livePoints.Count);

                w.WriteLine("time_s,position,force");
                foreach (DataPointUnit p in livePoints)
                {
                    w.WriteLine(p.time.ToString("R", inv) + "," + p.position.ToString("R", inv) + "," + p.force.ToString("R", inv));
                }
            }
            return path;
        }

        private void buttonSaveRun_Click(object? sender, EventArgs e)
        {
            try
            {
                SaveToFile();
                buttonSaveRun.Text = "Saved";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the run to\n" + SaveFolder + "\n\n" + ex.Message,
                                "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            parent.RemoveItem(this);
            UpdateGraphDelegate();
        }

        private void checkBoxVisible_CheckedChanged(object sender, EventArgs e)
        {
            UpdateGraphDelegate();
        }
    }
}
