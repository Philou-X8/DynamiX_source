using DynamiX_HMI.Analysis;
using DynamiX_HMI.Config;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace DynamiX_HMI.Grapher
{
    /// <summary>One block of the summary table: a run and one row per speed.</summary>
    public class SummaryBlock
    {
        public static readonly string[] Headers =
            { "Target Velocity", "Compression Velocity", "Compression Force", "Rebound Velocity", "Rebound Force" };

        public string Title = "";
        public string[] Units = Array.Empty<string>();
        public List<double[]> Rows = new List<double[]>();
    }

    /// <summary>
    /// The summary table of the final results: for each run, one row per speed with the peak velocities and the
    /// force at those peaks, as a formatted Excel file (.xlsx) or plain CSV (by the file extension).
    /// The raw data points are exported by TestRun.Save.
    /// </summary>
    public static class SummaryExport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Blocks one under the other with a blank row. Each block: run name, headers, units, a zero row, then one row
        /// per speed (force at peak velocity; rebound velocity as a positive speed, rebound force signed).
        /// </summary>
        public static void Write(string path, IEnumerable<KeyValuePair<string, List<StepResult>>> runs, UnitSystem u)
        {
            var blocks = new List<SummaryBlock>();
            bool si = u == UnitSystem.SI;
            string speedUnit = Units.SpeedUnit(u), forceUnit = Units.ForceUnit(u);
            foreach (var run in runs)
            {
                var block = new SummaryBlock { Title = run.Key, Units = new[] { speedUnit, speedUnit, forceUnit, speedUnit, forceUnit } };
                block.Rows.Add(new double[5]);
                foreach (StepResult r in run.Value.OrderBy(q => q.Info.TargetSpeedMmS))
                {
                    block.Rows.Add(new[]
                    {
                        Units.Length(r.Info.TargetSpeedMmS, u),
                        Units.Length(r.CompPeakVel, u), Units.Force(r.CompForceAtPeakVel, u),
                        Units.Length(Math.Abs(r.RebPeakVel), u), Units.Force(r.RebForceAtPeakVel, u),
                    });
                }
                blocks.Add(block);
            }

            // SI: whole mm/s and N, as on the reference sheet; imperial: 0.01 in/s and 0.1 lbf
            string[] fmt = si ? new[] { "0", "0", "0", "0", "0" } : new[] { "0.00", "0.00", "0.0", "0.00", "0.0" };
            if (string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                Xlsx.WriteSummary(path, blocks, fmt);
                return;
            }
            using var w = new StreamWriter(path, false, new UTF8Encoding(true)); // BOM: Excel then shows accents right
            for (int k = 0; k < blocks.Count; k++)
            {
                if (k > 0)
                {
                    w.WriteLine();
                }
                w.WriteLine("\"" + blocks[k].Title.Replace("\"", "'") + "\"");
                w.WriteLine(string.Join(",", SummaryBlock.Headers));
                w.WriteLine(string.Join(",", blocks[k].Units));
                foreach (double[] row in blocks[k].Rows)
                {
                    w.WriteLine(string.Join(",", row.Select((v, i) => v.ToString(fmt[i], Inv))));
                }
            }
        }
    }

    /// <summary>Minimal .xlsx writer (one sheet, inline strings, borders, centred cells, merged title): no extra packages.</summary>
    internal static class Xlsx
    {
        private const int Columns = 5;

        public static void WriteSummary(string path, List<SummaryBlock> blocks, string[] fmt)
        {
            var sheet = new StringBuilder();
            var merges = new List<string>();
            int row = 0;
            Func<int, int, string> refOf = (r, c) => (char)('A' + c) + (r + 1).ToString(CultureInfo.InvariantCulture);
            foreach (SummaryBlock b in blocks)
            {
                if (row > 0)
                {
                    row++; // blank row between runs
                }
                int titleRow = row;
                sheet.Append(RowXml(row, 30, Enumerable.Range(0, Columns).Select(c => c == 0 ? Str(refOf(titleRow, c), b.Title, 1) : Empty(refOf(titleRow, c), 1))));
                merges.Add(refOf(row, 0) + ":" + refOf(row, Columns - 1));
                row++;
                int headerRow = row;
                sheet.Append(RowXml(row, 36, Enumerable.Range(0, Columns).Select(c => Str(refOf(headerRow, c), SummaryBlock.Headers[c], 2))));
                row++;
                int unitRow = row;
                sheet.Append(RowXml(row, 0, Enumerable.Range(0, Columns).Select(c => Str(refOf(unitRow, c), b.Units[c], 1))));
                row++;
                foreach (double[] values in b.Rows)
                {
                    int valueRow = row;
                    sheet.Append(RowXml(row, 0, Enumerable.Range(0, Columns).Select(c =>
                        Num(refOf(valueRow, c), Math.Round(values[c], Decimals(fmt[c])), StyleFor(fmt[c])))));
                    row++;
                }
            }

            string sheetXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr>"
                + "<cols><col min=\"1\" max=\"5\" width=\"20\" customWidth=\"1\"/></cols>"
                + "<sheetData>" + sheet + "</sheetData>"
                + (merges.Count > 0 ? "<mergeCells count=\"" + merges.Count + "\">" + string.Concat(merges.Select(m => "<mergeCell ref=\"" + m + "\"/>")) + "</mergeCells>" : "")
                + "<pageSetup orientation=\"portrait\" fitToWidth=\"1\" fitToHeight=\"0\"/></worksheet>";
            const string styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"0.0\"/></numFmts>"
                + "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
                + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
                + "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>"
                + "<border><left style=\"thin\"/><right style=\"thin\"/><top style=\"thin\"/><bottom style=\"thin\"/><diagonal/></border></borders>"
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                + "<cellXfs count=\"6\">"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>"
                + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>"
                + "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>"
                + "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>"
                + "<xf numFmtId=\"2\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\"/></xf>"
                + "</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            using ZipArchive z = ZipFile.Open(path, ZipArchiveMode.Create);
            Add(z, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                + "</Types>");
            Add(z, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");
            Add(z, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                + "<sheets><sheet name=\"Summary\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(z, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
                + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
                + "</Relationships>");
            Add(z, "xl/worksheets/sheet1.xml", sheetXml);
            Add(z, "xl/styles.xml", styles);
        }

        private static int Decimals(string format)
        {
            return format.Length > 2 ? format.Length - 2 : 0;
        }

        private static int StyleFor(string format)
        {
            return format == "0" ? 3 : format == "0.0" ? 4 : 5;
        }

        private static string RowXml(int r, double height, IEnumerable<string> cells)
        {
            return "<row r=\"" + (r + 1) + "\"" + (height > 0 ? " ht=\"" + height.ToString(CultureInfo.InvariantCulture) + "\" customHeight=\"1\"" : "") + ">" + string.Concat(cells) + "</row>";
        }

        private static string Str(string at, string text, int style)
        {
            return "<c r=\"" + at + "\" s=\"" + style + "\" t=\"inlineStr\"><is><t>" + SecurityElement.Escape(text ?? "") + "</t></is></c>";
        }

        private static string Num(string at, double v, int style)
        {
            return "<c r=\"" + at + "\" s=\"" + style + "\"><v>" + v.ToString("R", CultureInfo.InvariantCulture) + "</v></c>";
        }

        private static string Empty(string at, int style)
        {
            return "<c r=\"" + at + "\" s=\"" + style + "\"/>";
        }

        private static void Add(ZipArchive z, string name, string xml)
        {
            using var w = new StreamWriter(z.CreateEntry(name).Open(), new UTF8Encoding(false));
            w.Write(xml);
        }
    }
}
