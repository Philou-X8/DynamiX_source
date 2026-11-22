using CSharpProto1.Sequencer;
using CSharpProto1.Serial;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSharpProto1.Grapher
{
    public partial class RunPanel : UserControl
    {


        public RunPanel()
        {
            InitializeComponent();
            //flowLayoutPanel1.Controls.Add(new RunItem(this, new List<DataPoint>() ));
        }

        public void AddItem(RunItem item)
        {
            flowLayoutPanel1.Controls.Add(item);
        }

        public void RemoveItem(RunItem item)
        {
            if (flowLayoutPanel1.Controls.Contains(item))
            {
                flowLayoutPanel1.Controls.Remove(item);
            }
        }

        public int ItemCount()
        {
            return flowLayoutPanel1.Controls.Count;
        }

        /// <summary> The curves of every run that is shown and not empty. </summary>
        public List<RunCurve> GetVisibleCurves()
        {
            List<RunCurve> ret = new();

            foreach(RunItem run in flowLayoutPanel1.Controls)
            {
                RunCurve? curve = run.GetCurve();
                if(curve != null)
                {
                    ret.Add(curve);
                }
            }

            return ret;
        }
    }
}
