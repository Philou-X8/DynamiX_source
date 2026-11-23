using DynamiX_HMI.Serial;
using System.Globalization;

namespace DynamiX_HMI.Sequencer
{
    public enum SpeedUnit { MmPerS, MPerS, DriveHz, YokeRpm }

    public enum DurationUnit { Cycles, Seconds }

    /// <summary>One step of a sequence, in the units the machine works with.</summary>
    /// <param name="TargetMmS">Target peak speed in mm/s (0 if the stroke is unknown and the speed was given in Hz or rpm).</param>
    /// <param name="DriveHz">Frequency sent to the drive.</param>
    /// <param name="Cycles">Whole cycles to count, 1 to 255.</param>
    /// <param name="YokeHz">Yoke revolutions per second.</param>
    public readonly record struct PlanStep(double TargetMmS, double DriveHz, int Cycles, double YokeHz);

    /// <summary>
    /// What a row of the sequence table means: speed in mm/s, m/s, drive Hz or yoke rpm, and a duration in cycles or
    /// seconds, turned into what the ESP32 is sent (a drive frequency and a whole number of cycles).
    /// Pure functions, no controls: the table only reads the text of its cells and calls TryResolve.
    /// </summary>
    public static class SequenceMath
    {
        /// <summary>
        /// Overall reduction from the drive's output frequency to the yoke's rotation:
        /// 2:1 drive-to-motor, then 5:1 belt. The yoke turns at (drive Hz / 10) rev/s.
        /// </summary>
        public const double DriveToYokeRatio = 2.0 * 5.0;

        public const int MaxCycles = 255;          // the cycle count travels in one byte
        public const int MaxSteps = 32;            // MAX_SEQUENCE_STEPS in the firmware's state_machine.h
        private const double DriveResolutionHz = 0.01;

        public static readonly string[] SpeedUnitNames = { "mm/s", "m/s", "drive Hz", "yoke RPM" };
        public static readonly string[] DurationUnitNames = { "cycles", "seconds" };

        public static bool TryParseSpeedUnit(string? name, out SpeedUnit unit)
        {
            int i = Array.FindIndex(SpeedUnitNames, n => string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase));
            unit = (SpeedUnit)Math.Max(i, 0);
            return i >= 0;
        }

        public static bool TryParseDurationUnit(string? name, out DurationUnit unit)
        {
            int i = Array.FindIndex(DurationUnitNames, n => string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase));
            unit = (DurationUnit)Math.Max(i, 0);
            return i >= 0;
        }

        /// <summary> A number with a decimal point or a decimal comma, whatever the PC's regional settings are. </summary>
        public static bool TryParseNumber(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && double.IsFinite(value);
        }

        /// <summary>
        /// Resolves a row. strokeMm is the stroke from the last TAR (strokeKnown false before one): speeds in mm/s or
        /// m/s need it, and so does the target speed that is recorded with the run. On failure 'error' says what to fix.
        /// </summary>
        public static bool TryResolve(string? speedText, string? speedUnitText, string? durationText, string? durationUnitText,
                                      double strokeMm, bool strokeKnown, out PlanStep step, out string error)
        {
            step = default;
            error = "";

            if (!TryParseNumber(speedText, out double speed))
            {
                error = "the speed is not a number";
                return false;
            }
            if (speed <= 0)
            {
                error = "the speed must be above 0";
                return false;
            }
            if (!TryParseSpeedUnit(speedUnitText, out SpeedUnit speedUnit))
            {
                error = "pick a speed unit";
                return false;
            }
            bool haveStroke = strokeKnown && strokeMm > 0;

            // yoke revolutions per second; the speed of the crank pin (peak speed of the stroke) is pi x stroke x that
            double yokeHz;
            switch (speedUnit)
            {
                case SpeedUnit.DriveHz:
                    yokeHz = speed / DriveToYokeRatio;
                    break;
                case SpeedUnit.YokeRpm:
                    yokeHz = speed / 60.0;
                    break;
                default:
                    if (!haveStroke)
                    {
                        error = "a speed in " + SpeedUnitNames[(int)speedUnit] + " needs the stroke: run a TAR first, or use drive Hz or yoke RPM";
                        return false;
                    }
                    double metersPerSecond = speedUnit == SpeedUnit.MmPerS ? speed / 1000.0 : speed;
                    yokeHz = metersPerSecond / (Math.PI * strokeMm / 1000.0);
                    break;
            }

            double driveHz = yokeHz * DriveToYokeRatio;
            if (driveHz > DriveCommands.MaxHz + 1e-9)
            {
                error = "needs " + driveHz.ToString("0.0", CultureInfo.InvariantCulture) + " Hz at the drive, its maximum is " + DriveCommands.MaxHz + " Hz";
                return false;
            }
            if (driveHz < DriveResolutionHz)
            {
                error = "too slow: " + driveHz.ToString("0.000", CultureInfo.InvariantCulture) + " Hz is below the drive's resolution";
                return false;
            }

            if (!TryParseNumber(durationText, out double duration) || duration <= 0)
            {
                error = "the duration must be a number above 0";
                return false;
            }
            if (!TryParseDurationUnit(durationUnitText, out DurationUnit durationUnit))
            {
                error = "pick a duration unit";
                return false;
            }
            double cyclesExact = durationUnit == DurationUnit.Seconds ? duration * yokeHz : duration;
            int cycles = (int)Math.Round(cyclesExact, MidpointRounding.AwayFromZero);
            if (cycles < 1 || cycles > MaxCycles)
            {
                error = durationUnit == DurationUnit.Seconds
                    ? duration.ToString("0.###", CultureInfo.InvariantCulture) + " s is " + cyclesExact.ToString("0.##", CultureInfo.InvariantCulture)
                      + " cycles at this speed, it must be 1 to " + MaxCycles
                    : "the cycles must be 1 to " + MaxCycles;
                return false;
            }

            double targetMmS = haveStroke ? Math.PI * strokeMm * yokeHz : 0.0;
            step = new PlanStep(targetMmS, driveHz, cycles, yokeHz);
            return true;
        }

        /// <summary> The same step in every unit, for the tooltip of a row. </summary>
        public static string Describe(PlanStep step)
        {
            var inv = CultureInfo.InvariantCulture;
            string text = step.DriveHz.ToString("0.0#", inv) + " Hz drive, " + (step.YokeHz * 60.0).ToString("0.#", inv) + " yoke RPM";
            if (step.TargetMmS > 0)
            {
                text = step.TargetMmS.ToString("0.#", inv) + " mm/s, " + text;
            }
            return text + ", " + step.Cycles + (step.Cycles == 1 ? " cycle" : " cycles")
                   + " (" + (step.Cycles / step.YokeHz).ToString("0.#", inv) + " s)";
        }
    }
}
