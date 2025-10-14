using System;
using System.Runtime.InteropServices;

using StarResonanceDpsAnalysis.Control;
using StarResonanceDpsAnalysis.Forms.AuxiliaryForms;
using StarResonanceDpsAnalysis.Forms.ModuleForm;

namespace StarResonanceDpsAnalysis.Forms
{
    public class FormManager
    {
        public const string APP_NAME = "别查我DPS";
        public static string AppVersion { get => $"v{Application.ProductVersion.Split('+')[0]}"; }


        public static SkillDiary skillDiary;//



        public static SkillDetailForm skillDetailForm;//技能详情窗体

        public static SettingsForm settingsForm;//设置窗体

        public static DpsStatisticsForm dpsStatistics;//DPS统计窗体

        public static UserUidSetForm userUidSetForm;//用户Uid设置窗体

        public static RankingsForm rankingsForm;//排行榜窗体

        public static MainForm mainForm;//主窗口

        public static SkillRotationMonitorForm skillRotationMonitorForm;//技能循环监控窗体

        public static HistoricalBattlesForm historicalBattlesForm;//历史战绩窗体

        public static DeathStatisticsForm deathStatisticsForm;//死亡统计窗体

        public static SkillReferenceForm skillReferenceForm;//技能参考窗体

        public static ModuleCalculationForm moduleCalculationForm;//模块计算窗体

        /// <summary>
        /// 统一设置透明度
        /// </summary>
        /// <param name="opacity"></param>
        public static void FullFormTransparency(double opacity)
        {

            foreach (var form in new Form[]
            {
                skillDiary,
                skillDetailForm,
                settingsForm,
                dpsStatistics,
                userUidSetForm,
                rankingsForm,
                skillRotationMonitorForm
            })
            {
                try
                {
                    if (form != null)
                        form.Opacity = opacity;
                }
                catch (Exception)
                { }
              
            }
        }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        #region 单次伤害/全程伤害
        public static int currentIndex = 0;        // 当前类别：0伤害/1治疗/2承伤
        public static bool showTotal = false;      // false=单次；true=全程
        #endregion
    }
}
