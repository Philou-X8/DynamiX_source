using DynamiX_HMI.Analysis;

namespace DynamiX_HMI.Grapher
{
    /// <summary>One speed of a run in the run list.</summary>
    public class StepEntry
    {
        public RunEntry Run;
        public StepInfo Info;
        public StepResult? Result;      // after the run's data is analysed
        public bool Visible = true;
        public Color? Custom;           // null = same colour as its run
        public Color Shown => Custom ?? Run.Shown;

        public StepEntry(RunEntry run, StepInfo info)
        {
            Run = run;
            Info = info;
        }
    }

    /// <summary>
    /// One run (a sweep of speeds) in the run list. Runs read from a file only have their header until they are
    /// shown: then the samples are loaded and analysed.
    /// </summary>
    public class RunEntry
    {
        public string Path = "";
        public string Display = "";
        public TestRun Run;
        public bool Loaded;             // the samples are in Run
        public bool InProgress;         // the ESP32 is still measuring it
        public List<StepResult> Results = new List<StepResult>();
        public List<StepEntry> Steps = new List<StepEntry>();
        public Color? Custom;
        public Color Shown;             // colour used in the graphs this time

        public RunEntry(TestRun run)
        {
            Run = run;
            Display = run.Name;
            Path = run.FilePath;
        }
    }
}
