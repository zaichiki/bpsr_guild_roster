using System.Diagnostics;
using AntdUI;
using SharpPcap;
using StarResonanceDpsAnalysis.Control;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Effects;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Plugin.DamageStatistics;
using StarResonanceDpsAnalysis.Properties;

namespace StarResonanceDpsAnalysis.Forms
{
    public partial class MainForm : BorderlessForm
    {


        #region ========== Constructor and Startup Loading ==========

        public MainForm()
        {

            InitializeComponent(); // # WinForms initialization
            FormGui.SetDefaultGUI(this); // # UI style: unified default appearance


            pageHeader_MainHeader.Text = Text = $"{FormManager.APP_NAME} {FormManager.AppVersion}";
            label_NowVersionNumber.Text = FormManager.AppVersion;

            //InitTableColumnsConfigAtFirstRun(); // # Column visibility initialization: establish column configuration on first run
            //LoadTableColumnVisibilitySettings(); // # Load column visibility settings
            //ToggleTableView(); // # Table view toggle (based on configuration)
            //LoadFromEmbeddedSkillConfig(); // # Pre-load skill metadata → SkillBook

            FormGui.SetColorMode(this, AppConfig.IsLight); // # Theme: main form light/dark mode

            var alimamaFont_Size12Bold = HandledResources.GetHarmonyOS_SansFont(12, FontStyle.Bold);
            var alimamaFont_Size9 = HandledResources.GetHarmonyOS_SansFont(9);

            var size12BoldControlList = new List<System.Windows.Forms.Control>()
            {
                groupBox_About, label_AppName, label_NowVersionTip, label_NowVersionDevelopersTip
            };
            foreach (var c in size12BoldControlList)
            {
                c.Font = alimamaFont_Size12Bold;
            }

            var size9ControlList = new List<System.Windows.Forms.Control>
            {
                label_SelfIntroduce, label_NowVersionNumber, label_NowVersionDevelopers,
                label_OpenSourceTip_1, linkLabel_GitHub, label_OpenSourceTip_2, linkLabel_QQGroup,
                label_ThankHelpFromTip_1, linkLabel_NodeJsProject, label_ThankHelpFromTip_2,
                label_Copyright
            };
            foreach (var c in size9ControlList)
            {
                c.Font = alimamaFont_Size9;
            }

            label_AppName.Text = $"{FormManager.APP_NAME}";

            using var ms = new MemoryStream(Resources.ApplicationIcon_256x256);
            pictureBox_AppIcon.Image = new Bitmap(ms);
        }


        #endregion


        #region ========== Device/Table Configuration on Startup ==========



        #endregion

        #region ========== Hotkeys/Interactive Events ==========

        #region —— Button/Checkbox/Dropdown Events —— 
        private void button_ThemeSwitch_Click(object sender, EventArgs e)
        {
            AppConfig.IsLight = !AppConfig.IsLight; // # State toggle: light/dark

            button_ThemeSwitch.Toggle = !AppConfig.IsLight; // # UI sync: button toggle state

            FormGui.SetColorMode(this, AppConfig.IsLight);
            FormGui.SetColorMode(FormManager.skillDiary, AppConfig.IsLight);

            FormGui.SetColorMode(FormManager.skillDetailForm, AppConfig.IsLight);//Set form color
            FormGui.SetColorMode(FormManager.settingsForm, AppConfig.IsLight);//Set form color
            FormGui.SetColorMode(FormManager.dpsStatistics, AppConfig.IsLight);//Set form color
            FormGui.SetColorMode(FormManager.rankingsForm, AppConfig.IsLight);//Set form color
            FormGui.SetColorMode(FormManager.historicalBattlesForm, AppConfig.IsLight);//Set form color

            // # Note: Some forms may be null or disposed, SetColorMode should handle null and IsDisposed checks internally for safe calling
        }

        private void button_AlwaysOnTop_Click(object sender, EventArgs e)
        {

        }

        private void dropdown_History_SelectedValueChanged(object sender, ObjectNEventArgs e)
        {

        }

        private void button_SkillDiary_Click(object sender, EventArgs e)
        {
            if (FormManager.dpsStatistics == null || FormManager.dpsStatistics.IsDisposed)
            {
                FormManager.dpsStatistics = new DpsStatisticsForm(); // # Open team statistics form
            }

            FormManager.dpsStatistics.Show();

        }

        private void button_Settings_MouseClick(object sender, MouseEventArgs e)
        {

        }

        #endregion
        #endregion


        #region ========== Timer Tick Events ==========

        private void timer_RefreshDpsTable_Tick(object sender, EventArgs e)
        {
            // Task.Run(() => RefreshDpsTable()); // # Performance tip: if async table refresh is needed, uncomment here; currently disabled to avoid concurrent updates
        }

        private void timer_RefreshRunningTime_Tick(object sender, EventArgs e)
        {


        }

        #endregion

        private void table_DpsDataTable_CellClick(object sender, TableClickEventArgs e)
        {
            if (e.RowIndex == 0) return; // # Ignore header clicks
            //ulong uid = 0;

            //if (FormManager.skillDetailForm == null || FormManager.skillDetailForm.IsDisposed)
            //{
            //    FormManager.skillDetailForm = new SkillDetailForm(); // # Detail form: lazy creation
            //}
            //SkillTableDatas.SkillTable.Clear(); // # Clear old detail data

            //FormManager.skillDetailForm.Uid = uid;
            ////Get player information
            //var info = StatisticData._manager.GetPlayerBasicInfo(uid); // # Query player basic info (nickname/combat power/profession)
            //FormManager.skillDetailForm.GetPlayerInfo(info.Nickname, info.CombatPower, info.Profession);
            //FormManager.skillDetailForm.SelectDataType(); // # Refresh based on currently selected "damage/healing/taken damage" type
            //FormManager.skillDetailForm.Show(); // # Show details

        }



        private void button1_Click(object sender, EventArgs e)
        {
            if (FormManager.rankingsForm == null || FormManager.rankingsForm.IsDisposed)
            {
                FormManager.rankingsForm = new RankingsForm(); // # Rankings window: lazy creation
            }
            FormManager.rankingsForm.Show();
        }

        private void linkLabel_GitHub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/anying1073/StarResonanceDps",
                UseShellExecute = true
            });

            linkLabel_GitHub.LinkVisited = true;
        }

        private void linkLabel_QQGroup_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://qm.qq.com/cgi-bin/qm/qr?k=QeIozXvLSRH9dL_CTZeisQ1Ae4CZpiSc&jump_from=webapi&authKey=HNr5BrrIhqRPyGs2R54NucKsg7Pb9/c0a03gih69PekWfSNLh9MIi/ClXXnaMzHK",
                UseShellExecute = true
            });

            linkLabel_QQGroup.LinkVisited = true;
        }

        private void linkLabel_NodeJsProject_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/dmlgzs/StarResonanceDamageCounter",
                UseShellExecute = true
            });

            linkLabel_NodeJsProject.LinkVisited = true;
        }

        private void MainForm_ForeColorChanged(object sender, EventArgs e)
        {
            if (Config.IsLight)
            {
                groupBox_About.ForeColor = Color.Black;
                linkLabel_GitHub.LinkColor = linkLabel_QQGroup.LinkColor = linkLabel_NodeJsProject.LinkColor = Color.Blue;
                linkLabel_GitHub.VisitedLinkColor = linkLabel_QQGroup.VisitedLinkColor = linkLabel_NodeJsProject.VisitedLinkColor = Color.Purple;
            }
            else
            {
                groupBox_About.ForeColor = Color.White;
                linkLabel_GitHub.LinkColor = linkLabel_QQGroup.LinkColor = linkLabel_NodeJsProject.LinkColor = Color.LightSkyBlue;
                linkLabel_GitHub.VisitedLinkColor = linkLabel_QQGroup.VisitedLinkColor = linkLabel_NodeJsProject.VisitedLinkColor = Color.MediumPurple;
            }
        }
    }
}
