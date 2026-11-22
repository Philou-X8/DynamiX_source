using DynamiX_HMI.Serial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamiX_HMI.Grapher
{
    /// <summary> The points and color of one visible run, as handed to the graphs. </summary>
    public class RunCurve
    {
        public List<DataPointUnit> Points;
        public List<float> Velocities; // one per point (Velocity.Compute), for the Force/Speed graph
        public Color Color;

        public RunCurve(List<DataPointUnit> points, List<float> velocities, Color color)
        {
            Points = points;
            Velocities = velocities;
            Color = color;
        }
    }

    public struct DataPointUnit
    {
        public DataPointUnit(float t, float f, float p)
        {
            time = t;
            force = f;
            position = p;
        }
        public float time; // in second
        public float force; // in Neuton
        public float position; // in mm

        /// <summary>
        /// Point and separator timestamps (microseconds since the run started) to seconds.
        /// Both go through this, so the same timestamp always gives the same float.
        /// </summary>
        public static float MicrosToSeconds(int micros)
        {
            return micros * 1e-6f;
        }
    }

    public struct DataCalibration
    {
        public DataCalibration()
        {
            emptyForce = 0;
            bottomForce = 0;
            topForce = 0;
            bottomPosition = 0;
            topPosition = 0;
            stroke = 0;
            temperatureStart = 0;
            temperatureEnd = 0;
        }
        public DataCalibration(float emptyF, float botF, float topF, float botP, float topP)
        {
            emptyForce = emptyF;
            bottomForce = botF;
            topForce = topF;
            bottomPosition = botP;
            topPosition = topP;
            stroke = topPosition - bottomPosition;
            temperatureStart = 0;
            temperatureEnd = 0;
        }
        public DataCalibration(float emptyF, float botF, float topF, float botP, float topP, float tempS, float tempE)
        {
            emptyForce = emptyF;
            bottomForce = botF;
            topForce = topF;
            bottomPosition = botP;
            topPosition = topP;
            stroke = topPosition - bottomPosition;
            temperatureStart = tempS;
            temperatureEnd = tempE;
        }

        public float emptyForce;
        public float bottomForce;
        public float topForce;
        public float bottomPosition;
        public float topPosition;
        public float stroke;
        public float temperatureStart;
        public float temperatureEnd;
    };

    /// <summary>
    /// Owns the received points and decides what becomes a run. It drives the two views:
    /// the GraphPanel (raw view, Force/Pos view, debug text) and the RunPanel (list of runs).
    /// Everything runs on the UI thread.
    /// </summary>
    internal class GraphController
    {
        private const int RecentCapacity = 256;       // points kept for the raw and debug views
        private const int MaxBufferedPoints = 50000;  // safety cap if separators never arrive

        private readonly RunPanel runPanel;
        private readonly GraphPanel graphPanel;

        // Points received during the current run that haven't been cut into a section yet
        private List<DataPointUnit> buffer = new List<DataPointUnit>();

        // The last points received, kept apart from 'buffer' so the live views don't
        // go blank when a section is cut out of it.
        private List<DataPointUnit> recent = new List<DataPointUnit>();

        // Sections announced by the ESP (start and end time, s) that are waiting for their points
        private List<(float start, float end)> pendingSections = new List<(float start, float end)>();
        private List<DataCalibration> pendingMetadata = new List<DataCalibration>();

        private int runNumber = 0;
        private int sectionCount = 0;    // separators received this run
        private int sectionsHandled = 0; // of those, the ones already cut out of the buffer


        public GraphController(RunPanel sidePanel, GraphPanel graph)
        {
            runPanel = sidePanel;
            graphPanel = graph;
        }

        /// <summary> Start of a run: forget the points and sections of the previous one. </summary>
        public void BeginRun()
        {
            buffer = new List<DataPointUnit>();
            pendingSections.Clear();
            pendingMetadata.Clear();
            sectionCount = 0;
            sectionsHandled = 0;
            runNumber++;
        }

        /// <summary> Clears the live points (Reset button). Finished runs are kept. </summary>
        public void ResetPoints()
        {
            buffer = new List<DataPointUnit>();
            recent = new List<DataPointUnit>();
            pendingSections.Clear();
            pendingMetadata.Clear();
            sectionCount = 0;
            sectionsHandled = 0;
            graphPanel.SetRecentPoints(recent);
        }

        public void AddPoints(List<DataPointUnit> dataPoints)
        {
            buffer.AddRange(dataPoints);
            if (buffer.Count > MaxBufferedPoints)
            {
                buffer.RemoveRange(0, buffer.Count - MaxBufferedPoints);
            }

            recent.AddRange(dataPoints);
            if (recent.Count > RecentCapacity)
            {
                recent.RemoveRange(0, recent.Count - RecentCapacity);
            }
            graphPanel.SetRecentPoints(recent);

            EmitReadySections(false);
        }

        /// <summary>
        /// Handles ESP_sequence_separator: payload is the start timestamp (4 bytes) and the
        /// end timestamp (4 bytes) of a finished section, big-endian, in microseconds like the raw points.
        /// Returns false if the payload is too short.
        /// </summary>
        public bool TryAddSequenceSeparator(CommandPacket command)
        {
            if (command.payload.Length < 8)
            {
                return false;
            }
            byte[] b = Encoding.Latin1.GetBytes(command.payload);
            float start = DataPointUnit.MicrosToSeconds((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
            float end = DataPointUnit.MicrosToSeconds((b[4] << 24) | (b[5] << 16) | (b[6] << 8) | b[7]);

            sectionCount++;
            pendingSections.Add((start, end));
            EmitReadySections(false);
            return true;
        }

        public bool TryAddSequenceSeparator(CommandPacket command, DataCalibration metadata)
        {
            if (command.payload.Length < 8)
            {
                return false;
            }
            byte[] b = Encoding.Latin1.GetBytes(command.payload);
            float start = DataPointUnit.MicrosToSeconds((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
            float end = DataPointUnit.MicrosToSeconds((b[4] << 24) | (b[5] << 16) | (b[6] << 8) | b[7]);

            sectionCount++;
            pendingSections.Add((start, end));
            pendingMetadata.Add(metadata);
            EmitReadySections(false);
            return true;
        }

        /// <summary>
        /// Cuts out every announced section whose points have arrived. The separator travels
        /// on a faster path than the data and can show up before the last points of its
        /// section, so a section waits until a point at or after its end timestamp is buffered.
        /// force: cut out what is there regardless (end of run).
        /// </summary>
        private void EmitReadySections(bool force)
        {
            bool added = false;

            while (pendingSections.Count > 0)
            {
                (float start, float end) = pendingSections[0];
                bool dataArrived = buffer.Count > 0 && buffer[buffer.Count - 1].time >= end;
                if (!dataArrived && !force)
                {
                    break; // later sections end later, so none of them can be ready either
                }

                DataCalibration meta = new DataCalibration();
                if (pendingMetadata.Count > 0)
                {
                    meta = pendingMetadata[0];
                }

                // sections are numbered in the order their separators arrived
                sectionsHandled++;
                List<DataPointUnit> points = buffer.Where(p => p.time >= start && p.time <= end).ToList();
                if (points.Count > 0)
                {
                    string name = "Run " + runNumber + " step " + sectionsHandled;
                    //runPanel.AddItem(new RunItem(runPanel, name, points, UpdateDisplayRuns));
                    runPanel.AddItem(new RunItem(runPanel, name, points, meta, UpdateDisplayRuns));
                    added = true;
                }

                // everything before the end of this section is no longer needed
                buffer.RemoveAll(p => p.time < end);
                pendingSections.RemoveAt(0);
                if (pendingMetadata.Count > 0)
                {
                    pendingMetadata.RemoveAt(0);
                }
            }

            if (added)
            {
                UpdateDisplayRuns();
            }
        }

        public List<DataPointUnit> GetLatest(int count)
        {
            if (recent.Count == 0)
            {
                return Enumerable.Repeat(new DataPointUnit(0, 0, 0), count).ToList();
            }

            if (recent.Count < count)
            {
                int n_filler = count - recent.Count;
                List<DataPointUnit> ret = Enumerable.Repeat(recent[0], n_filler).ToList();
                ret.AddRange(recent);
                return ret;
            }

            return recent.GetRange(recent.Count - count, count);
        }

        public int GetPointsCount()
        {
            return buffer.Count;
        }

        /// <summary>
        /// End of a run: cut out the sections still waiting for their points. If the ESP
        /// sent no separators at all, the whole run becomes a single item instead.
        /// </summary>
        public void FinishRun()
        {
            EmitReadySections(true);

            if (sectionCount == 0 && buffer.Count > 0)
            {
                string displayName = "Run " + runNumber + " result";
                runPanel.AddItem(new RunItem(runPanel, displayName, buffer.ToList(), UpdateDisplayRuns));
            }

            UpdateDisplayRuns();
        }

        /// <summary> Redraws the Force/Pos view from the runs that are currently shown. </summary>
        public void UpdateDisplayRuns()
        {
            graphPanel.SetRuns(runPanel.GetVisibleCurves());
        }
    }
}
