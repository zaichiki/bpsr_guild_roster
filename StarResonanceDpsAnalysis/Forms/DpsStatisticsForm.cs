using System;
using System.Drawing;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks; // Reference async task support (Task/async/await)
using System.Windows.Forms;

using AntdUI; // Reference AntdUI component library (third-party UI controls/styles)
using StarResonanceDpsAnalysis.Control; // Reference project internal UI control/helper class namespace
using StarResonanceDpsAnalysis.Effects;
using StarResonanceDpsAnalysis.Forms.PopUp; // Reference popup related form/component namespace
using StarResonanceDpsAnalysis.Plugin; // Reference project plugin layer common namespace
using StarResonanceDpsAnalysis.Plugin.DamageStatistics; // Reference damage statistics plugin namespace (includes FullRecord, StatisticData, etc.)
using StarResonanceDpsAnalysis.Plugin.LaunchFunction; // Reference startup related functionality (loading skill configurations, etc.)
using StarResonanceDpsAnalysis.Properties; // Reference resources (icons/localized strings, etc.)

using static StarResonanceDpsAnalysis.Control.SkillDetailForm;
using System.Security.Cryptography.Xml;
using Button = AntdUI.Button;
using DocumentFormat.OpenXml.Office2010.Excel;
using Color = System.Drawing.Color;
using StarResonanceDpsAnalysis.Forms.ModuleForm;

namespace StarResonanceDpsAnalysis.Forms // Define namespace: location of form-related code
{ // Namespace start
    public partial class DpsStatisticsForm : BorderlessForm // Define partial class for borderless form (merged with Designer generated part)
    { // Class start
        // # Navigation
        // # This file responsibilities:
        // #   1) Form construction and startup flow (initialize UI/hooks/config/devices/skill config).
        // #   2) List interaction (select item → open skill detail window).
        // #   3) Top operations (always on top, settings menu, tooltips).
        // #   4) Statistics view switching (single/full record + left/right switch metrics).
        // #   5) Timed refresh (battle duration, leaderboard data refresh).
        // #   6) Clear/piling mode (timer and upload flow).
        // #   7) Theme switching (control background adaptation when foreground color changes).
        // #   8) Global hotkey hooks (install/uninstall/key routing).
        // #   9) Window control (mouse penetration, transparency switching).

        // # Construction and startup flow
        public DpsStatisticsForm() // Constructor: executed once when creating form instance
        {
            // Constructor start
            InitializeComponent(); // Initialize designer generated controls and layout


            Text = FormManager.APP_NAME;

            FormGui.SetDefaultGUI(this); // Uniformly set form default GUI style (font, spacing, shadows, etc.)

            //ApplyResolutionScale(); // Optional: scale entire interface based on screen resolution (currently disabled, only call preserved)

            // Set font from resource file
            SetDefaultFontFromResources();

            // Load hooks
            RegisterKeyboardHook(); // Install keyboard hook for global hotkey monitoring and handling

            // Initialize basic configuration on first startup
            InitTableColumnsConfigAtFirstRun(); // Initialize table column configuration on first run (column width/display items, etc.)

            // Load network cards
            LoadNetworkDevices(); // Load/enumerate network devices (packet capture device list)

            FormGui.SetColorMode(this, AppConfig.IsLight);//Set form color // Set form color theme based on configuration (light/dark)

            // Load skill configuration
            StartupInitializer.LoadFromEmbeddedSkillConfig(); // Read and load skill data from embedded resources (metadata/icons/mapping)


            sortedProgressBarList1.SelectionChanged += (s, i, d) => // Subscribe to progress bar list selection change event (click item)
            { // Event handling start
                // # UI list interaction: triggered when user clicks list item (i is index, d is ProgressBarData)
                if (i < 0 || d == null) // If no valid item selected or data is null
                { // Conditional branch start
                    return; // Return directly without any processing
                } // Conditional branch end
                  // # Pass selected item UID to detail window for refresh
                sortedProgressBarList_SelectionChanged((ulong)d.ID); // Convert item ID to UID and call detail refresh logic
            }; // Event handling end and disassociate from next statement

            SetStyle(); // Set/apply personalized style for this form (defined in other parts of same class/partial class)
            ApplyLocalization();
        } // Constructor end


        // # Screen resolution scaling determination
        private static float GetPrimaryResolutionScale() // Return recommended scaling ratio based on primary screen height
        {
            try // Defense: getting screen info may be abnormal in some environments
            { // try start
                var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080); // Get primary screen size, default to 1080p if failed
                if (bounds.Height >= 2160) return 2.0f;       // 4K screen: recommend scaling 2.0
                if (bounds.Height >= 1440) return 1.3333f;    // 2K screen: recommend scaling 1.3333
                return 1.0f;                                   // 1080p: no scaling
            } // try end
            catch // Catch any exception
            { // catch start
                return 1.0f; // Safely return 1.0 on exception (no scaling)
            } // catch end
        }

        // # Form load event: start packet capture
        private void DpsStatistics_Load(object sender, EventArgs e) // Form Load event handler
        {
            //Enable default always on top

            StartCapture(); // Start network packet capture/data collection (one of core runtime entry points)

            // Reset to position and size before last close
            SetStartupPositionAndSize();

            EnsureTopMost();
        }

        // # List selection change → open skill details
        private void sortedProgressBarList_SelectionChanged(ulong uid) // List item selection callback: pass selected player UID
        {
            // If currently in "NPC damage taken" view: click NPC row to switch to "players ranking attacking this NPC"
            if (FormManager.currentIndex == 3)
            {
                // Full record display: directly refresh to this NPC's attacker leaderboard
                _npcDetailMode = true;
                _npcFocusId = uid;

                // Immediately refresh this NPC's attacker leaderboard (current/full record already automatically separated within method)
                RefreshNpcAttackers(_npcFocusId);
                // Optional: update title
                pageHeader1.SubText = FormManager.showTotal
                    ? string.Format(Properties.Strings.Header_NpcAttackers_FullRecord, uid)
                    : string.Format(Properties.Strings.Header_NpcAttackers_Current, uid);
                return;
            }

            // ...Below is your original player skill detail logic...
            if (FormManager.skillDetailForm == null || FormManager.skillDetailForm.IsDisposed)
                FormManager.skillDetailForm = new SkillDetailForm();

            SkillTableDatas.SkillTable.Clear();

            FormManager.skillDetailForm.Uid = uid;
            var info = StatisticData._manager.GetPlayerBasicInfo(uid);
            FormManager.skillDetailForm.GetPlayerInfo(info.Nickname, info.CombatPower, info.Profession);

            if (FormManager.showTotal) { FormManager.skillDetailForm.ContextType = DetailContextType.FullRecord; FormManager.skillDetailForm.SnapshotStartTime = null; }
            else { FormManager.skillDetailForm.ContextType = DetailContextType.Current; FormManager.skillDetailForm.SnapshotStartTime = null; }

            FormManager.skillDetailForm.SelectDataType();
            if (!FormManager.skillDetailForm.Visible) FormManager.skillDetailForm.Show(); else FormManager.skillDetailForm.Activate();
        }

        // # Top: always on top window button
        private void button_AlwaysOnTop_Click(object sender, EventArgs e) // Always on top button click event
        {
            TopMost = !TopMost; // Simplified toggle
            FormManager.skillDetailForm.TopMost = TopMost;

            button_AlwaysOnTop.Toggle = TopMost; // Synchronize button visual state
        }

        #region Switch display type (supports single/full record damage) // Collapse: view labels and switching logic


        // # Header title text refresh: based on showTotal & currentIndex
        private void UpdateHeaderText() // Update top label text based on current mode and index
        {

            if (FormManager.showTotal)
            {
                pageHeader1.SubText = FormManager.currentIndex switch
                {
                    1 => Properties.Strings.Header_FullRecord_Healing,
                    2 => Properties.Strings.Header_FullRecord_Taken,
                    3 => Properties.Strings.Header_FullRecord_NpcTaken,
                    _ => Properties.Strings.Header_FullRecord_Damage
                };
            }
            else
            {
                pageHeader1.SubText = FormManager.currentIndex switch
                {
                    1 => Properties.Strings.Header_Current_Healing,
                    2 => Properties.Strings.Header_Current_Taken,
                    3 => Properties.Strings.Header_Current_NpcTaken,
                    _ => Properties.Strings.Header_Current_Damage
                };
            }
        }



        // Single/full record toggle
        private void button3_Click(object sender, EventArgs e) // Single/full record toggle button event
        {
            FormManager.showTotal = !FormManager.showTotal; // Invert: toggle between single and full record
            UpdateHeaderText(); // Refresh top text after toggle
        }
        #endregion

        // # Timed refresh: battle duration display + leaderboard refresh
        private void timer_RefreshRunningTime_Tick(object sender, EventArgs e) // Timer: periodic refresh (UI bound)
        {
            if (FormManager.currentIndex == 3)
            {
                // NPC damage taken page
                if (_npcDetailMode && _npcFocusId != 0)
                {
                    // Currently viewing some NPC's attacker leaderboard —— stay on detail page and refresh that leaderboard
                    RefreshNpcAttackers((ulong)_npcFocusId);

                    // (Optional robustness) If this NPC has disappeared/no data, can automatically exit detail and return to overview
                    // You can automatically call ExitNpcDetailMode() + RefreshNpcOverview() when checking for null inside RefreshNpcAttackers
                }
                else
                {
                    // Non-detail mode: refresh NPC damage taken overview (current/full record already handled internally by method)
                    RefreshNpcOverview();
                }
            }
            else
            {
                var source = FormManager.showTotal ? SourceType.FullRecord : SourceType.Current;
                var metric = FormManager.currentIndex switch
                {
                    1 => MetricType.Healing,
                    2 => MetricType.Taken,
                    3 => MetricType.NpcTaken,   // ★ Reserved: if used elsewhere
                    _ => MetricType.Damage
                };
                RefreshDpsTable(source, metric);
            }

            var duration = StatisticData._manager.GetFormattedCombatDuration();
            if (FormManager.showTotal) duration = FullRecord.GetEffectiveDurationString();
            BattleTimeText.Text = duration;
        }


        /// <summary>
        /// Clear current data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e) // Clear button click: trigger clear logic
        {
            // # Clear: trigger HandleClearData (stop chart refresh→clear data→reset chart)
            HandleClearData(); // Call clear handling
        }


        // # Settings button → right-click menu
        private void button_Settings_Click(object sender, EventArgs e) // Settings button click: popup right-click menu
        {


            var menulist = new IContextMenuStripItem[] // Build right-click menu item array
             { // Array start
                    new ContextMenuStripItem(Properties.Strings.Menu_HistoricalBattles) // First level menu: historical battles
                    { // Configuration start
                        IconSvg = Resources.historicalRecords, // Icon
                    }, // First level menu configuration end
                    new ContextMenuStripItem(Properties.Strings.Menu_Settings){ IconSvg = Resources.set_up}, // First level menu: basic settings
                    new ContextMenuStripItem(Properties.Strings.Menu_MainForm){ IconSvg = Resources.HomeIcon, }, // First level menu: main form
                    new ContextMenuStripItem(Properties.Strings.Menu_ModuleConfig){ IconSvg= Resources.moduleIcon }, // First level menu: data display settings
                    //new ContextMenuStripItem("技能循环监测"), // First level menu: skill rotation monitoring
                    //new ContextMenuStripItem(""){ IconSvg = Resources.userUid, }, // Example: user UID (not used for now)
                    new ContextMenuStripItem(Properties.Strings.Menu_DeathStatistics){ IconSvg = Resources.exclude, }, // First level menu: statistics exclusion
                    new ContextMenuStripItem(Properties.Strings.Menu_SkillDiary){ IconSvg = Resources.diaryIcon, },
                    new ContextMenuStripItem(Properties.Strings.Menu_DamageReference){ IconSvg = Resources.reference, },
                    new ContextMenuStripItem(Properties.Strings.Menu_GuildRoster){ IconSvg = Resources.userUid, }, // First level menu: guild roster
                    new ContextMenuStripItem(Properties.Strings.Menu_GuildMemberDiscordData){ IconSvg = Resources.userUid, }, // First level menu: guild member discord data
                    new ContextMenuStripItem(Properties.Strings.Menu_PilingMode){ IconSvg = Resources.Stakes }, // First level menu: piling mode
                    new ContextMenuStripItem(Properties.Strings.Menu_Exit){ IconSvg = Resources.quit, }, // First level menu: exit
             } // Array end
            ; // Statement end (semicolon preserved)

            AntdUI.ContextMenuStrip.open(this, it => // Open right-click menu and handle click callback (it is the clicked item)
            {



                // Callback start
                // # Menu click callback: execute corresponding action based on Text
                switch (it.Text) // Branch based on menu text
                {
                    case var s when s == Properties.Strings.Menu_HistoricalBattles:
                        if (FormManager.historicalBattlesForm == null || FormManager.historicalBattlesForm.IsDisposed)
                        {
                            FormManager.historicalBattlesForm = new HistoricalBattlesForm();
                        }
                        FormManager.historicalBattlesForm.Show();
                        break;
                    // switch start
                    case var s when s == Properties.Strings.Menu_Settings: // Click "Basic Settings"
                        OpenSettingsDialog(); // Open settings panel
                        break; // Break out of switch
                    case var s when s == Properties.Strings.Menu_MainForm: // Click "Main Form"
                        if (FormManager.mainForm == null || FormManager.mainForm.IsDisposed) // If main form doesn't exist or is disposed
                        {
                            FormManager.mainForm = new MainForm(); // Create main form
                        }
                        FormManager.mainForm.Show(); // Show main form
                        break; // Break out of switch
                    case var s when s == Properties.Strings.Menu_ModuleConfig:
                        if (FormManager.moduleCalculationForm == null || FormManager.moduleCalculationForm.IsDisposed) // If main form doesn't exist or is disposed
                        {
                            FormManager.moduleCalculationForm = new ModuleCalculationForm(); // Create main form
                        }
                        FormManager.moduleCalculationForm.Show(); // Show main form
                        break;

                    case var s when s == Properties.Strings.Menu_DeathStatistics:
                        if (FormManager.deathStatisticsForm == null || FormManager.deathStatisticsForm.IsDisposed)
                        {
                            FormManager.deathStatisticsForm = new DeathStatisticsForm();
                        }
                        FormManager.deathStatisticsForm.Show();
                        break;
                    case var s when s == Properties.Strings.Menu_SkillDiary:
                        if (FormManager.skillDiary == null || FormManager.skillDiary.IsDisposed)
                        {
                            FormManager.skillDiary = new SkillDiary();
                        }
                        FormManager.skillDiary.Show();
                        break;
                    case "技能循环监测": // Click "Skill Rotation Monitoring"
                        if (FormManager.skillRotationMonitorForm == null || FormManager.skillRotationMonitorForm.IsDisposed) // If monitoring form doesn't exist or is disposed
                        {
                            FormManager.skillRotationMonitorForm = new SkillRotationMonitorForm(); // Create window
                        }
                        FormManager.skillRotationMonitorForm.Show(); // Show window
                        //FormGui.SetColorMode(FormManager.skillRotationMonitorForm, AppConfig.IsLight); // Synchronize theme (light/dark)
                        break; // Break out of switch
                    case "数据显示设置": // Click "Data Display Settings" (currently only placeholder preserved)
                        //dataDisplay(); 
                        break; // Placeholder: to be implemented later
                    case var s when s == Properties.Strings.Menu_DamageReference:
                        if (FormManager.rankingsForm == null || FormManager.rankingsForm.IsDisposed) // If monitoring form doesn't exist or is disposed
                        {
                            FormManager.rankingsForm = new RankingsForm(); // Create window
                        }
                        FormManager.rankingsForm.Show(); // Show window
                        break;
                    case var s when s == Properties.Strings.Menu_GuildRoster: // Click "Guild Roster"
                        if (FormManager.guildRosterForm == null || FormManager.guildRosterForm.IsDisposed) // If guild roster form doesn't exist or is disposed
                        {
                            FormManager.guildRosterForm = new GuildRosterForm(); // Create window
                        }
                        FormManager.guildRosterForm.Show(); // Show window
                        FormManager.guildRosterForm.BringToFront(); // Bring window to front
                        FormManager.guildRosterForm.Activate(); // Activate window (give it focus)
                        break;
                    case var s when s == Properties.Strings.Menu_GuildMemberDiscordData: // Click "Guild Member Discord Data"
                        if (FormManager.guildMemberDiscordDataForm == null || FormManager.guildMemberDiscordDataForm.IsDisposed) // If guild member discord data form doesn't exist or is disposed
                        {
                            FormManager.guildMemberDiscordDataForm = new GuildMemberDiscordDataForm(); // Create window
                        }
                        FormManager.guildMemberDiscordDataForm.Show(); // Show window
                        FormManager.guildMemberDiscordDataForm.BringToFront(); // Bring window to front
                        FormManager.guildMemberDiscordDataForm.Activate(); // Activate window (give it focus)
                        break;
                    case "统计排除": // Click "Statistics Exclusion"
                        break; // Placeholder: to be implemented later
                    case var s when s == Properties.Strings.Menu_PilingMode: // Click "Piling Mode"
                        PilingModeCheckbox.Visible = !PilingModeCheckbox.Visible;
                        break; // Break out of switch
                    case var s when s == Properties.Strings.Menu_Exit: // Click "Exit"
                        System.Windows.Forms.Application.Exit(); // Terminate application
                        break; // Break out of switch
                } // switch end
            }, menulist); // Open menu and pass menu items
        }

        /// <summary>
        /// Open basic settings panel
        /// </summary>
        private void OpenSettingsDialog() // Open basic settings form
        {
            if (FormManager.settingsForm == null || FormManager.settingsForm.IsDisposed) // If settings form doesn't exist or is disposed
            {
                FormManager.settingsForm = new SettingsForm(); // Create settings form
            }
            FormManager.settingsForm.Show(); // Show settings form (or bring to front)

        }

        // # Button tooltip (always on top)
        private void button_AlwaysOnTop_MouseEnter(object sender, EventArgs e) // Show tooltip when mouse enters always on top button
        {
            ToolTip(button_AlwaysOnTop, Properties.Strings.Tooltip_AlwaysOnTop); // Show "Always on top window" bubble tooltip
        }

        // # General tooltip utility

        private void ToolTip(System.Windows.Forms.Control control, string text) // General wrapper: display tooltip text on specified control
        {
            tooltip.SetTip(control, text); // Display specified text tooltip on target control
        }

        // # Button tooltip (clear)
        private void button1_MouseEnter(object sender, EventArgs e) // Show tooltip when mouse enters "Clear" button
        {
            ToolTip(button1, Properties.Strings.Tooltip_ClearData); // Show "Clear current data" bubble tooltip
        }
        private void button2_MouseEnter(object sender, EventArgs e)
        {
            ToolTip(button2, Properties.Strings.Tooltip_Minimize);
        }

        // # Button tooltip (single/full record toggle)
        private void button3_MouseEnter(object sender, EventArgs e) // Show tooltip when mouse enters "Single/Full record toggle" button
        {
            ToolTip(button3, Properties.Strings.Tooltip_SwitchSingleTotal); // Show toggle tooltip (as original, preserved)
        }

        private void button_ThemeSwitch_MouseEnter(object sender, EventArgs e)
        {
            ToolTip(button_ThemeSwitch, Properties.Strings.Tooltip_SwitchTheme);
        }

        // Piling mode timer logic
        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (PilingModeCheckbox.Checked)
            {
                if (AppConfig.Uid == 0)
                {

                    PilingModeCheckbox.Checked = false;
                    timer1.Enabled = false;
                    var _ = AppMessageBox.ShowMessage(Properties.Strings.Msg_UidNotFound, this);
                    return;
                }
                TimeSpan duration = StatisticData._manager.GetCombatDuration();
                if (duration >= TimeSpan.FromMinutes(3))
                {
                    PilingModeCheckbox.Checked = false;
                    timer1.Enabled = false;

                    var snapshot = StatisticData._manager.TakeSnapshotAndGet();
                    var result = AppMessageBox.ShowMessage(Properties.Strings.Msg_PilingComplete, this);

                    if (result == DialogResult.OK)
                    {
                        bool data = await Common.AddUserDps(snapshot);
                        if (data)
                        {
                            AntdUI.Modal.open(new AntdUI.Modal.Config(this, Properties.Strings.Msg_UploadSuccess, Properties.Strings.Msg_UploadSuccess)
                            {
                                CloseIcon = true,
                                Keyboard = false,
                                MaskClosable = false,
                            });
                        }
                        else
                        {
                            AntdUI.Modal.open(new AntdUI.Modal.Config(this, Properties.Strings.Msg_UploadFail, Properties.Strings.Msg_UploadFailDetail)
                            {
                                CloseIcon = true,
                                Keyboard = false,
                                MaskClosable = false,
                            });
                        }
                    }
                }
            }
        }

        // 打桩模式勾选变化
        private void PilingModeCheckbox_CheckedChanged(object sender, BoolEventArgs e)
        {
            TimeSpan duration = StatisticData._manager.GetCombatDuration(); // 保留获取以与原逻辑一致

            if (e.Value)
            {
                var result = AppMessageBox.ShowMessage(Properties.Strings.Msg_PilingModeInfo, this);
                if (result == DialogResult.OK)
                {
                    DpsTableDatas.DpsTable.Clear();
                    StatisticData._manager.ClearAll();
                    SkillTableDatas.SkillTable.Clear();
                    Task.Delay(200);
                    AppConfig.PilingMode = true;
                    timer1.Enabled = true;
                }
                else
                {
                    PilingModeCheckbox.Checked = false;
                }
            }
            else
            {
                AppConfig.PilingMode = false;
                timer1.Enabled = false;
            }
        }

        // 主题切换
        private void DpsStatisticsForm_ForeColorChanged(object sender, EventArgs e)
        {
            List<Button> buttonList = new List<Button>() { TotalDamageButton, TotalTreatmentButton, AlwaysInjuredButton, NpcTakeDamageButton };

            if (Config.IsLight)
            {
                sortedProgressBarList1.BackColor = ColorTranslator.FromHtml("#F5F5F5");
                AppConfig.colorText = Color.Black;
                sortedProgressBarList1.OrderColor = Color.Black;
                panel1.Back = ColorTranslator.FromHtml("#F5F5F5"); //bottom
                panel2.Back = ColorTranslator.FromHtml("#F5F5F5"); //top

                TotalDamageButton.Icon = Common.BytesToImage(Properties.Resources.伤害);
                TotalTreatmentButton.Icon = Common.BytesToImage(Properties.Resources.治疗);
                AlwaysInjuredButton.Icon = Common.BytesToImage(Properties.Resources.承伤);
                NpcTakeDamageButton.Icon = Common.BytesToImage(Properties.Resources.Npc);
                Color colorWhite = Color.FromArgb(223, 223, 223);
                foreach (var item in buttonList)
                {
                    item.DefaultBack = Color.FromArgb(247, 247, 247);
                    if (item.Name == "TotalDamageButton" && FormManager.currentIndex == 0)
                    {
                        item.DefaultBack = colorWhite;
                    }
                    if (item.Name == "TotalTreatmentButton" && FormManager.currentIndex == 1)
                    {
                        item.DefaultBack = colorWhite;
                    }
                    if (item.Name == "AlwaysInjuredButton" && FormManager.currentIndex == 2)
                    {
                        item.DefaultBack = colorWhite;
                    }
                    if (item.Name == "NpcTakeDamageButton" && FormManager.currentIndex == 3)
                    {
                        item.DefaultBack = colorWhite;
                    }

                }

            }
            else
            {
                sortedProgressBarList1.BackColor = ColorTranslator.FromHtml("#252527");
                panel1.Back = ColorTranslator.FromHtml("#252527");
                panel2.Back = ColorTranslator.FromHtml("#252527");

                AppConfig.colorText = Color.White;
                sortedProgressBarList1.OrderColor = Color.White;
                TotalDamageButton.Icon = Common.BytesToImage(Properties.Resources.伤害白色);
                TotalTreatmentButton.Icon = Common.BytesToImage(Properties.Resources.治疗白色);
                AlwaysInjuredButton.Icon = Common.BytesToImage(Properties.Resources.承伤白色);
                NpcTakeDamageButton.Icon = Common.BytesToImage(Properties.Resources.NpcWhite);
                Color colorBack = Color.FromArgb(60, 60, 60);
                foreach (var item in buttonList)
                {
                    item.DefaultBack = Color.FromArgb(27, 27, 27);
                    if (item.Name == "TotalDamageButton" && FormManager.currentIndex == 0)
                    {
                        item.DefaultBack = colorBack;
                    }
                    if (item.Name == "TotalTreatmentButton" && FormManager.currentIndex == 1)
                    {
                        item.DefaultBack = colorBack;
                    }
                    if (item.Name == "AlwaysInjuredButton" && FormManager.currentIndex == 2)
                    {
                        item.DefaultBack = colorBack;
                    }
                    if (item.Name == "NpcTakeDamageButton" && FormManager.currentIndex == 3)
                    {
                        item.DefaultBack = colorBack;
                    }

                }
            }

            SetSortedProgressBarListForeColor();
        }

        private void SetSortedProgressBarListForeColor()
        {
            if (sortedProgressBarList1.Data == null) return;

            lock (sortedProgressBarList1.Data)
            {
                foreach (var data in sortedProgressBarList1.Data)
                {
                    if (data.ContentList == null) continue;

                    foreach (var content in data.ContentList)
                    {
                        if (content.Type != Control.GDI.RenderContent.ContentType.Text) continue;

                        content.ForeColor = AppConfig.colorText;
                    }
                }
            }
        }

        private void SetDefaultFontFromResources()
        {
            pageHeader1.Font = AppConfig.SaoFont;
            pageHeader1.SubFont = AppConfig.ContentFont;
            PilingModeCheckbox.Font = AppConfig.ContentFont;
            label2.Font = label1.Font = AppConfig.ContentFont;

            TotalDamageButton.Font = AppConfig.BoldHarmonyFont;
            TotalTreatmentButton.Font = AppConfig.BoldHarmonyFont;
            AlwaysInjuredButton.Font = AppConfig.BoldHarmonyFont;
            NpcTakeDamageButton.Font = AppConfig.BoldHarmonyFont;
        }

        private void SetStartupPositionAndSize()
        {
            var startupRect = AppConfig.StartUpState;
            if (startupRect != null && startupRect != Rectangle.Empty)
            {
                Left = startupRect.Value.Left;
                Top = startupRect.Value.Top;
                Width = startupRect.Value.Width;
                Height = startupRect.Value.Height;
            }
        }

        #region 钩子
        private KeyboardHook KbHook { get; } = new();
        public void RegisterKeyboardHook()
        {
            KbHook.SetHook();
            KbHook.OnKeyDownEvent += kbHook_OnKeyDownEvent;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { KbHook?.UnHook(); }
            catch (Exception ex) { Console.WriteLine($"窗体关闭清理时出错: {ex.Message}"); }
            base.OnFormClosed(e);
        }

        public void kbHook_OnKeyDownEvent(object? sender, KeyEventArgs e)
        {
            if (e.KeyData == AppConfig.MouseThroughKey) { HandleMouseThrough(); }
            //else if (e.KeyData == AppConfig.FormTransparencyKey) { HandleFormTransparency(); }
            //else if (e.KeyData == AppConfig.WindowToggleKey) { }
            else if (e.KeyData == AppConfig.ClearDataKey) { HandleClearData(); }
            else if (e.KeyData == AppConfig.ClearHistoryKey)
            {
                StatisticData._manager.ClearSnapshots();//清空快照
                StarResonanceDpsAnalysis.Plugin.DamageStatistics.FullRecord.ClearSessionHistory();//清空全程快照

            }
        }

        private void HandleMouseThrough()
        {
            if (!MousePenetrationHelper.IsPenetrating(this.Handle))
            {
                // 方案 O：AppConfig.Transparency 现在表示“不透明度百分比”
                MousePenetrationHelper.SetMousePenetrate(this, enable: true, opacityPercent: AppConfig.Transparency);
            }
            else
            {
                MousePenetrationHelper.SetMousePenetrate(this, enable: false);
            }
        }

        // 配置变化时实时刷新不透明度（不改变穿透开关）
        private void ApplyOpacityFromConfig()
        {
            MousePenetrationHelper.UpdateOpacityPercent(this.Handle, AppConfig.Transparency);
        }


        bool hyaline = false;

        #endregion



        private void button2_Click_1(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void DpsStatisticsForm_Shown(object sender, EventArgs e)
        {

        }

        private void EnsureTopMost()
        {
            TopMost = false;   // 先关再开，强制触发样式刷新
            TopMost = true;
            Activate();
            BringToFront();
            button_AlwaysOnTop.Toggle = TopMost; // 同步你的按钮状态
        }

        private void DamageType_Click(object sender, EventArgs e)
        {
            ExitNpcDetailMode(); // 退出详情模式
            Button button = (Button)sender;
            List<Button> buttonList = new List<Button>() { TotalDamageButton, TotalTreatmentButton, AlwaysInjuredButton, NpcTakeDamageButton };
            Color colorBack = Color.FromArgb(60, 60, 60);
            Color colorWhite = Color.FromArgb(223, 223, 223);
            foreach (Button btn in buttonList)
            {
                if (btn.Name == button.Name)
                {
                    if (Config.IsLight)
                    {
                        btn.DefaultBack = colorWhite;
                    }
                    else
                    {
                        btn.DefaultBack = colorBack;
                    }

                }
                else
                {
                    if (Config.IsLight)
                    {
                        btn.DefaultBack = Color.FromArgb(247, 247, 247);
                    }
                    else
                    {
                        btn.DefaultBack = Color.FromArgb(27, 27, 27);
                    }

                }

            }

            switch (button.Name)
            {
                //总伤害
                case "TotalDamageButton":
                    FormManager.currentIndex = 0;
                    break;
                //总治疗
                case "TotalTreatmentButton":
                    FormManager.currentIndex = 1;
                    break;
                //总承伤
                case "AlwaysInjuredButton":
                    FormManager.currentIndex = 2;
                    break;
                //NPC承伤
                case "NpcTakeDamageButton":
                    FormManager.currentIndex = 3;
                    break;
            }

            UpdateHeaderText(); // 刷新顶部文本

        }

        private void DpsStatisticsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            AppConfig.StartUpState = new Rectangle(Left, Top, Width, Height);
        }

        /// <summary>
        /// 退出详情模式
        /// </summary>
        private void ExitNpcDetailMode()
        {
            _npcDetailMode = false;
            _npcFocusId = 0;
        }

        private void button_ThemeSwitch_Click(object sender, EventArgs e)
        {
            AppConfig.IsLight = !AppConfig.IsLight; // # 状态翻转：明/暗

            button_ThemeSwitch.Toggle = !AppConfig.IsLight; // # UI同步：按钮切换状态

            FormGui.SetColorMode(this, AppConfig.IsLight);
            FormGui.SetColorMode(FormManager.skillDiary, AppConfig.IsLight);
            FormGui.SetColorMode(FormManager.mainForm, AppConfig.IsLight);//设置窗体颜色
            FormGui.SetColorMode(FormManager.skillDetailForm, AppConfig.IsLight);//设置窗体颜色
            FormGui.SetColorMode(FormManager.settingsForm, AppConfig.IsLight);//设置窗体颜色
            FormGui.SetColorMode(FormManager.dpsStatistics, AppConfig.IsLight);//设置窗体颜色
            FormGui.SetColorMode(FormManager.rankingsForm, AppConfig.IsLight);//设置窗体颜色
            FormGui.SetColorMode(FormManager.historicalBattlesForm, AppConfig.IsLight);//设置窗体颜色
            FormGui.SetColorMode(FormManager.moduleCalculationForm, AppConfig.IsLight);//设置窗体颜色
        }
        public void ApplyLocalization()
        {
            // Page header
            // pageHeader1.Text = Properties.Strings.Header_DpsStatistics_Title;
            pageHeader1.SubText = Properties.Strings.Header_DpsStatistics_Subtitle;

            // Buttons
            TotalDamageButton.Text = Properties.Strings.TotalDamageLabel;
            TotalTreatmentButton.Text = Properties.Strings.TotalTreatmentLabel;
            AlwaysInjuredButton.Text = Properties.Strings.AlwaysInjuredLabel;
            NpcTakeDamageButton.Text = Properties.Strings.NpcTakeDamageLabel;

            // Checkbox
            PilingModeCheckbox.Text = Properties.Strings.PilingModeCheckboxLabel;
        }

        private void sortedProgressBarList1_Load(object sender, EventArgs e)
        {

        }
    }
}
