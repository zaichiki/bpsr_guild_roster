using AntdUI;
using StarResonanceDpsAnalysis.Plugin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.Forms
{
    public partial class HistoricalRecordsForm : BorderlessForm
    {
        public HistoricalRecordsForm()
        {
            InitializeComponent();
            FormGui.SetDefaultGUI(this);
            FormGui.SetColorMode(this, AppConfig.IsLight);//设置窗体颜色
        }
    }
}
