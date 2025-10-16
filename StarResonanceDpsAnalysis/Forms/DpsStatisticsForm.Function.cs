using AntdUI;
using SharpPcap;
using StarResonanceDpsAnalysis.Control;
using StarResonanceDpsAnalysis.Control.GDI;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Effects;
using StarResonanceDpsAnalysis.Effects.Enum;
using StarResonanceDpsAnalysis.Extends;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Plugin.DamageStatistics;
using StarResonanceDpsAnalysis.Plugin.LaunchFunction;
using StarResonanceDpsAnalysis.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Forms
{
    public partial class DpsStatisticsForm
    {
        // # Navigation
        // # This file main responsibilities:
        // #   1) Start/stop network packet capture lifecycle management (StartCapture/StopCapture).
        // #   2) Clear/reset statistics data and charts (HandleClearData/ListClear).
        // #   3) Initialize user and control styles (InitTableColumnsConfigAtFirstRun/SetStyle).
        // #   4) Handle packet arrival events, pass raw data to PacketAnalyzer (Device_OnPacketArrival).
        // #   5) Build and refresh DPS/healing/damage taken UI lists (RefreshDpsTable/BuildUiRows).
        // # Event classification index:
        // #   * [Startup and initialization events] InitTableColumnsConfigAtFirstRun / LoadNetworkDevices / SetStyle
        // #   * [Packet capture events] StartCapture / StopCapture / Device_OnPacketArrival
        // #   * [Cleanup and reset events] HandleClearData / ListClear
        // #   * [UI refresh events] RefreshDpsTable / BuildUiRows
        // #   * [Thread safety and state] _dataLock / _isClearing / IsCaptureStarted / SelectedDevice

        #region Load network cards, startup devices/initialize statistics data/start packet capture/stop packet capture/clear data/close events
        private void InitTableColumnsConfigAtFirstRun()
        {
            // # Startup and initialization event: first run initialization of table header configuration & bind local identity information
            if (AppConfig.GetConfigExists())
            {
                AppConfig.ClearPicture = AppConfig.GetValue("UserConfig", "ClearPicture", "1").ToInt();
                AppConfig.NickName = AppConfig.GetValue("UserConfig", "NickName", "Unknown");
                AppConfig.Uid = (ulong)AppConfig.GetValue("UserConfig", "Uid", "0").ToInt();
                AppConfig.Profession = AppConfig.GetValue("UserConfig", "Profession", Properties.Strings.Profession_Unknown);
                AppConfig.CombatPower = AppConfig.GetValue("UserConfig", "CombatPower", "0").ToInt();

                // Write to local statistics cache (for UI initial display)
                StatisticData._manager.SetNickname(AppConfig.Uid, AppConfig.NickName);
                StatisticData._manager.SetProfession(AppConfig.Uid, AppConfig.Profession);
                StatisticData._manager.SetCombatPower(AppConfig.Uid, AppConfig.CombatPower);

                if (AppConfig.Uid != 0)
                {

                    // Refresh current view after backfill completion (avoid dependency on subsequent packet capture)
                    RequestActiveViewRefresh();
                }

                SortedProgressBarStatic = this.sortedProgressBarList1; // # Key: bind instance here
                return;
            }
        }

        #region —— Packet capture devices/statistics —— 

        public static ICaptureDevice? SelectedDevice { get; set; } = null; // # Packet capture device: network card device selected by program (may be null, initialized based on settings)

        /// <summary>
        /// Analyzer
        /// </summary>
        private PacketAnalyzer PacketAnalyzer { get; } = new(); // # Packet capture/analyzer: each arriving packet is processed by this analyzer
        #endregion

        /// <summary>
        /// Load network card devices at startup
        /// </summary>
        public void LoadNetworkDevices()
        {
            // # Startup and initialization event: load network device list during application startup phase, select default network card based on configuration
            Console.WriteLine("Loading network cards during application startup...");

            if (AppConfig.NetworkCard >= 0)
            {
                var devices = CaptureDeviceList.Instance; // # Device list: provided by SharpPcap
                if (AppConfig.NetworkCard < devices.Count)
                {
                    SelectedDevice = devices[AppConfig.NetworkCard]; // # Select device based on index
                    Console.WriteLine($"Network card selected at startup: {SelectedDevice.Description} (index: {AppConfig.NetworkCard})");
                }
            }
            else
            {
                // When not set, popup settings window to guide user selection
                if (FormManager.settingsForm == null || FormManager.settingsForm.IsDisposed)
                {
                    FormManager.settingsForm = new SettingsForm();
                }
                FormManager.settingsForm.LoadDevices(); // # Settings form: populate device list
            }
        }

        /// <summary>
        /// Packet arrival event
        /// </summary>
        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            // # Packet capture event: callback when packet arrives (SharpPcap thread)
            try
            {
                var dev = (ICaptureDevice)sender;
                PacketAnalyzer.StartNewAnalyzer(dev, e.GetPacket());
            }
            catch (Exception ex)
            {
                // # Exception protection: avoid packet capture thread interruption due to unhandled exceptions
                Console.WriteLine($"Exception occurred while processing packet after arrival {ex.Message}\r\n{ex.StackTrace}");
            }
        }
        #region StartCapture() Packet capture: start/stop/events/statistics
        /// <summary>
        /// Whether packet capture has started
        /// </summary>
        private static bool IsCaptureStarted { get; set; } = false; // # Runtime state: indicates whether currently in packet capture/monitoring

        /// <summary>
        /// Start packet capture
        /// </summary>
        public async void StartCapture()
        {
            // # Packet capture event: triggered when user clicks "Start" or auto-start
            // # Step 1: Pre-validation —— network device index/availability check
            if (AppConfig.NetworkCard < 0)
            {
                MessageBox.Show("Please select a network card device");
                return;
            }

            var devices = CaptureDeviceList.Instance;
            if (devices == null || devices.Count == 0)
                throw new InvalidOperationException("No available network packet capture devices found");

            if (AppConfig.NetworkCard < 0 || AppConfig.NetworkCard >= devices.Count)
                throw new InvalidOperationException($"Invalid network device index: {AppConfig.NetworkCard}");

            SelectedDevice = devices[AppConfig.NetworkCard];
            if (SelectedDevice == null)
                throw new InvalidOperationException($"Unable to get network device, index: {AppConfig.NetworkCard}");

            await Task.Delay(1000);
            // # Step 3: Chart history and auto-refresh —— start new battle record
            ChartVisualizationService.ClearAllHistory();

            // Start auto-refresh for all charts + background sampling (satisfies "load curves from DPS damage start")
            ChartVisualizationService.StartAllChartsAutoRefresh(1000);

            // # Step 4: Open and start device monitoring —— bind callbacks, set filters
            SelectedDevice.Open(new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                Immediate = true,
                ReadTimeout = 1000,
                BufferSize = 1024 * 1024 * 4
            });
            SelectedDevice.Filter = "ip and tcp";
            SelectedDevice.OnPacketArrival += new PacketArrivalEventHandler(Device_OnPacketArrival);
            SelectedDevice.StartCapture();

            // # Step 5: Mark state, start full record logger
            IsCaptureStarted = true;
            FullRecord.Start();
            Console.WriteLine("Starting packet capture...");
        }

        /// <summary>
        /// Stop packet capture
        /// </summary>
        public void StopCapture()
        {
            // # Packet capture event: triggered when user clicks "Stop" or before program exit
            // # Step 1: First stop auto-refresh for all charts to prevent continued data updates after stopping packet capture
            ChartVisualizationService.StopAllChartsAutoRefresh();

            // When stopping packet capture, notify chart service that battle has ended to ensure final 0-value state is displayed
            ChartVisualizationService.OnCombatEnd();

            if (SelectedDevice != null)
            {
                try
                {
                    // # Step 2: Unbind events to avoid callbacks accessing disposed objects
                    SelectedDevice.OnPacketArrival -= Device_OnPacketArrival;

                    // # Step 3: Stop packet capture (usually asynchronous internally)
                    SelectedDevice.StopCapture();

                    // # Step 4: Wait for background capture thread to actually exit (simple polling, max ~1s)
                    for (int i = 0; i < 100; i++)
                    {
                        if (!(SelectedDevice.Started)) break;
                        System.Threading.Thread.Sleep(10);
                    }

                    // # Step 5: Close and release handles (Dispose is critical)
                    SelectedDevice.Close();
                    SelectedDevice.Dispose();
                    Console.WriteLine("Stopped packet capture");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Stop packet capture exception: {ex}");
                }
                finally
                {
                    SelectedDevice = null;
                }
            }

            // # Step 6: State reset and parsing state clear
            IsCaptureStarted = false;

            // Clear parsing/reassembly state
            PacketAnalyzer.ResetCaptureState();

            // # Step 7: Update network card setting tip on UI
            StartupInitializer.RefreshNetworkCardSettingTip();
        }

        #region HandleClearData() Respond to clear data

        public void HandleClearData(bool ClearPicture = false)
        {
            // # Cleanup and reset event: triggered when user clicks "Clear" (does not affect packet capture start/stop state)
            // First stop auto-refresh for all charts
            ChartVisualizationService.StopAllChartsAutoRefresh();

            // Before clearing data, notify chart service that battle has ended
            ChartVisualizationService.OnCombatEnd();
            if (FormManager.showTotal && !ClearPicture)
            {
                FullRecord.Reset(false);//Clear full record statistics data
                // Synchronously clear "full record curve" history to ensure timeline restarts from 0
                ChartVisualizationService.ClearFullHistory();
            }

            ListClear();

            // Only clear current curve history, preserve full record curve (satisfies "full record damage recorded from damage start to F9 refresh")
            ChartVisualizationService.ClearCurrentHistory();

            // If currently capturing packets, restart chart auto-refresh (continue background sampling)
            if (IsCaptureStarted)
            {
                ChartVisualizationService.StartAllChartsAutoRefresh(1000);
            }
        }
        private readonly object _dataLock = new();
        private int _isClearing = 0; // 0: normal, 1: clearing
        public void ListClear()
        {
            // # Cleanup and reset event: clear UI progress bar list and cache (thread-safe)
            if (Interlocked.Exchange(ref _isClearing, 1) == 1) return; // Already clearing

            StatisticData._manager.ClearAll();
            SkillTableDatas.SkillTable.Clear();
            label1.Text = $"";
            label2.Text = $"";
            try
            {
                lock (_dataLock)
                {
                    // # Clear data models in memory
                    DictList.Clear();
                    list.Clear();
                    userRenderContent.Clear();
                    // UI component cache clear (note: switch back to UI thread)
                    var ctrl = SortedProgressBarStatic;
                    // ListClear() — clear UI
                    if (ctrl != null && !ctrl.IsDisposed)
                    {
                        if (ctrl.InvokeRequired)
                            ctrl.BeginInvoke(() => ctrl.Data = new List<ProgressBarData>());
                        else
                            ctrl.Data = new List<ProgressBarData>();
                    }

                }
            }
            finally
            {
                // # Exit clearing state
                Volatile.Write(ref _isClearing, 0);
            }
        }

        #endregion
        #endregion
        #endregion


        public void SetStyle()
        {
            // # Startup and initialization event: interface style and rendering settings (UI appearance only, no data involved)
            // ======= Single progress bar (textProgressBar1) appearance settings =======
            sortedProgressBarList1.OrderImageOffset = new RenderContent.ContentOffset { X = 6, Y = 0 };
            sortedProgressBarList1.OrderImageRenderSize = new Size(22, 22);
            sortedProgressBarList1.OrderOffset = new RenderContent.ContentOffset { X = 32, Y = 0 };
            sortedProgressBarList1.OrderCallback = (i) => $"{i:d2}.";
            sortedProgressBarList1.OrderImages =
            [
                new Bitmap(new MemoryStream(Resources.皇冠)),
                           
            ];


            if (Config.IsLight)
            {
                sortedProgressBarList1.OrderColor = Color.Black;
            }
            else
            {
                sortedProgressBarList1.OrderColor = Color.White;
            }

            sortedProgressBarList1.OrderFont = AppConfig.SaoFont;

            // ======= Progress bar list (sortedProgressBarList1) initialization and appearance =======
            sortedProgressBarList1.ProgressBarHeight = AppConfig.ProgressBarHeight;  // Height per row
            sortedProgressBarList1.AnimationDuration = 1000; // Animation duration (milliseconds)
            sortedProgressBarList1.AnimationQuality = Quality.Low; // Animation quality (enum in your project)
            //sortedProgressBarList1.OrderImages =
            //[
            //    new Bitmap(new MemoryStream(Resources.皇冠)),
            //                new Bitmap(new MemoryStream(Resources.皇冠白))
            //];
        }

        // Whether currently staying on "NPC attacker leaderboard" detail
        private volatile bool _npcDetailMode = false;
        // NPC Id currently being viewed in detail
        private ulong _npcFocusId = 0;

        /// <summary>
        /// Instantiate SortedProgressBarList control
        /// </summary>
        public static SortedProgressBarList SortedProgressBarStatic { get; private set; }

        /// <summary>
        /// User battle data dictionary
        /// </summary>
        readonly static Dictionary<long, List<RenderContent>> DictList = new Dictionary<long, List<RenderContent>>();

        /// <summary>
        /// User battle data update event
        /// </summary>
        static List<ProgressBarData> list = new List<ProgressBarData>();

        /// <summary>
        /// User displays their own information at the bottom
        /// </summary>
        static List<RenderContent> userRenderContent = new List<RenderContent>();

        //Light form
        Dictionary<string, Color> colorDict = new Dictionary<string, Color>()
        {
                { Properties.Strings.Profession_Unknown, ColorTranslator.FromHtml("#67AEF6") },

                { Properties.Strings.Profession_Marksman, ColorTranslator.FromHtml("#fffca3") },
                { Properties.Strings.Profession_FrostMage, ColorTranslator.FromHtml("#aaa6ff") },
                { Properties.Strings.Profession_HeavyGuardian, ColorTranslator.FromHtml("#8ee392") },
                { Properties.Strings.Profession_Stormblade, ColorTranslator.FromHtml("#b8a3ff") },
                { Properties.Strings.Profession_SoulMusician, ColorTranslator.FromHtml("#ff5353") },
                { Properties.Strings.Profession_WindKnight, ColorTranslator.FromHtml("#abfaff") },
                { Properties.Strings.Profession_VerdantOracle, ColorTranslator.FromHtml("#78ff95") },
                { Properties.Strings.Profession_AegisKnight, ColorTranslator.FromHtml("#bfe6ff") },

                { Properties.Strings.SubProfession_IceRay, ColorTranslator.FromHtml("#fffca3") },
                { Properties.Strings.SubProfession_Concerto, ColorTranslator.FromHtml("#ff5353") },
                { Properties.Strings.SubProfession_Lifebloom, ColorTranslator.FromHtml("#78ff95") },
                { Properties.Strings.SubProfession_Thornlash, ColorTranslator.FromHtml("#78ff95") },
                { Properties.Strings.SubProfession_RagingSound, ColorTranslator.FromHtml("#ff5353") },
                { Properties.Strings.SubProfession_IceSpear, ColorTranslator.FromHtml("#aaa6ff") },
                { Properties.Strings.SubProfession_Iai, ColorTranslator.FromHtml("#b8a3ff") },
                { Properties.Strings.SubProfession_MoonBlade, ColorTranslator.FromHtml("#b8a3ff") },
                { Properties.Strings.SubProfession_EagleBow, ColorTranslator.FromHtml("#fffca3") },
                { Properties.Strings.SubProfession_WolfBow, ColorTranslator.FromHtml("#fffca3") },
                { Properties.Strings.SubProfession_AirStyle, ColorTranslator.FromHtml("#abfaff") },
                { Properties.Strings.SubProfession_Overdrive, ColorTranslator.FromHtml("#abfaff") },
                { Properties.Strings.SubProfession_Protection, ColorTranslator.FromHtml("#bfe6ff") },
                { Properties.Strings.SubProfession_LightShield, ColorTranslator.FromHtml("#bfe6ff") },
                { Properties.Strings.SubProfession_RockShield, ColorTranslator.FromHtml("#8ee392") },
                { Properties.Strings.SubProfession_Block, ColorTranslator.FromHtml("#8ee392") },

        };

        //Dark form
        Dictionary<string, Color> blackColorDict = new Dictionary<string, Color>()
        {
                { Properties.Strings.Profession_Unknown, ColorTranslator.FromHtml("#67AEF6") },

                { Properties.Strings.Profession_Marksman, ColorTranslator.FromHtml("#8e8b47") },
                { Properties.Strings.Profession_FrostMage, ColorTranslator.FromHtml("#79779c") },
                { Properties.Strings.Profession_HeavyGuardian, ColorTranslator.FromHtml("#537758") },
                { Properties.Strings.Profession_Stormblade, ColorTranslator.FromHtml("#70629c") },
                { Properties.Strings.Profession_SoulMusician, ColorTranslator.FromHtml("#9c5353") },
                { Properties.Strings.Profession_WindKnight, ColorTranslator.FromHtml("#799a9c") },
                { Properties.Strings.Profession_VerdantOracle, ColorTranslator.FromHtml("#639c70") },
                { Properties.Strings.Profession_AegisKnight, ColorTranslator.FromHtml("#9c9b75") },

                { Properties.Strings.SubProfession_IceRay, ColorTranslator.FromHtml("#8e8b47") },
                { Properties.Strings.SubProfession_Concerto, ColorTranslator.FromHtml("#9c5353") },
                { Properties.Strings.SubProfession_Lifebloom, ColorTranslator.FromHtml("#639c70") },
                { Properties.Strings.SubProfession_Thornlash, ColorTranslator.FromHtml("#639c70") },
                { Properties.Strings.SubProfession_RagingSound, ColorTranslator.FromHtml("#9c5353") },
                { Properties.Strings.SubProfession_IceSpear, ColorTranslator.FromHtml("#79779c") },
                { Properties.Strings.SubProfession_Iai, ColorTranslator.FromHtml("#70629c") },
                { Properties.Strings.SubProfession_MoonBlade, ColorTranslator.FromHtml("#70629c") },
                { Properties.Strings.SubProfession_EagleBow, ColorTranslator.FromHtml("#8e8b47") },
                { Properties.Strings.SubProfession_WolfBow, ColorTranslator.FromHtml("#8e8b47") },
                { Properties.Strings.SubProfession_AirStyle, ColorTranslator.FromHtml("#799a9c") },
                { Properties.Strings.SubProfession_Overdrive, ColorTranslator.FromHtml("#799a9c") },
                { Properties.Strings.SubProfession_Protection, ColorTranslator.FromHtml("#9c9b75") },
                { Properties.Strings.SubProfession_LightShield, ColorTranslator.FromHtml("#9c9b75") },
                { Properties.Strings.SubProfession_RockShield, ColorTranslator.FromHtml("#537758") },
                { Properties.Strings.SubProfession_Block, ColorTranslator.FromHtml("#537758") },
        };
        static Bitmap EmptyBitmap(int w = 1, int h = 1)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.Clear(Color.Transparent);   // Completely transparent
            return bmp;
        }
        public static Dictionary<string, Bitmap> imgDict = new Dictionary<string, Bitmap>() // convert to resource key
        {
            { Properties.Strings.Profession_Unknown, EmptyBitmap() },
            { Properties.Strings.SubProfession_IceRay, new Bitmap(new MemoryStream(Resources.冰魔导师)) },
            { Properties.Strings.Profession_FrostMage, new Bitmap(new MemoryStream(Resources.冰魔导师)) },
            { Properties.Strings.Profession_HeavyGuardian, new Bitmap(new MemoryStream(Resources.巨刃守护者)) },
            { Properties.Strings.Profession_VerdantOracle, new Bitmap(new MemoryStream(Resources.森语者)) },
            { Properties.Strings.Profession_SoulMusician, new Bitmap(new MemoryStream(Resources.灵魂乐手)) },
            { Properties.Strings.Profession_Marksman, new Bitmap(new MemoryStream(Resources.神射手)) },
            { Properties.Strings.Profession_Stormblade, new Bitmap(new MemoryStream(Resources.雷影剑士)) },
            { Properties.Strings.Profession_WindKnight, new Bitmap(new MemoryStream(Resources.青岚骑士)) },
            { Properties.Strings.Profession_AegisKnight, new Bitmap(new MemoryStream(Resources.神盾骑士)) },
            { Properties.Strings.SubProfession_Concerto, new Bitmap(new MemoryStream(Resources.灵魂乐手)) },
            { Properties.Strings.SubProfession_MoonBlade, new Bitmap(new MemoryStream(Resources.雷影剑士)) },
            { Properties.Strings.SubProfession_EagleBow, new Bitmap(new MemoryStream(Resources.神射手)) },
            { Properties.Strings.SubProfession_WolfBow, new Bitmap(new MemoryStream(Resources.神射手)) },
            { Properties.Strings.SubProfession_AirStyle, new Bitmap(new MemoryStream(Resources.青岚骑士)) },
            { Properties.Strings.SubProfession_Overdrive, new Bitmap(new MemoryStream(Resources.青岚骑士)) },
            { Properties.Strings.SubProfession_Protection, new Bitmap(new MemoryStream(Resources.神盾骑士)) },
            { Properties.Strings.SubProfession_LightShield, new Bitmap(new MemoryStream(Resources.神盾骑士)) },
            { Properties.Strings.SubProfession_RockShield, new Bitmap(new MemoryStream(Resources.巨刃守护者)) },
            { Properties.Strings.SubProfession_Block, new Bitmap(new MemoryStream(Resources.巨刃守护者)) },
            { Properties.Strings.SubProfession_Lifebloom, new Bitmap(new MemoryStream(Resources.森语者)) },
            { Properties.Strings.SubProfession_Thornlash, new Bitmap(new MemoryStream(Resources.森语者)) },
        };



        public enum SourceType { Current, FullRecord }
        public enum MetricType { Damage, Healing, Taken, NpcTaken }

        // Provide a static entry point for requesting refresh of current view after backfill
        public static void RequestActiveViewRefresh()
        {
            try
            {
                var form = FormManager.dpsStatistics;
                if (form == null || form.IsDisposed) return;
                var source = FormManager.showTotal ? SourceType.FullRecord : SourceType.Current;
                var metric = FormManager.currentIndex switch
                {
                    1 => MetricType.Healing,
                    2 => MetricType.Taken,
                    3 => MetricType.NpcTaken,   // ★

                    _ => MetricType.Damage
                };
                if (form.InvokeRequired)
                    form.BeginInvoke(() => form.RefreshDpsTable(source, metric));
                else
                    form.RefreshDpsTable(source, metric);
            }
            catch { }
        }
        // NPC overview row: one NPC per row
        private class NpcRow
        {
            public long NpcId;
            public string Name;
            public ulong TotalTaken;
            public double TakenPerSec;
        }
        // Attacker row for a specific NPC (still player row, style reuse)
        private class NpcAttackerRow
        {
            public long Uid;
            public string Nickname;
            public int CombatPower;
            public string Profession;
            public string SubProfession;
            public ulong DamageToNpc;
            public double PlayerDps;   // Player full record DPS (info item)
            public double NpcOnlyDps;  // This player's exclusive DPS for this NPC (main basis for progress bar)
        }

        private class UiRow
        {
            public long Uid;
            public string Nickname;
            public int CombatPower;
            public string Profession;
            public ulong Total;
            public double PerSecond;
            public string SubProfession;
        }

        public void RefreshDpsTable(SourceType source, MetricType metric)
        {
            // # UI refresh event: rebuild and bind progress bar list based on specified data source (single/full record) and metrics (damage/healing/damage taken)
            if (Interlocked.CompareExchange(ref _isClearing, 0, 0) == 1) return;
            // —— Gate #1: validate current visible view matches before starting ——
            var visible = FormManager.showTotal ? SourceType.FullRecord : SourceType.Current;
            if (source != visible) return;

            var uiList = BuildUiRows(source, metric)
                .Where(r => (r?.Total ?? 0) > 0)   // Filter 0 values (applies to damage/healing/damage taken)
                .ToList();

            if (uiList.Count == 0)
            {
                if (sortedProgressBarList1.InvokeRequired)
                    sortedProgressBarList1.BeginInvoke(() => sortedProgressBarList1.Data = new List<ProgressBarData>());
                else
                    sortedProgressBarList1.Data = new List<ProgressBarData>();
                return;
            }

            var ordered = uiList.OrderByDescending(x => x.Total).ToList();

            double teamSum = uiList.Sum(x => (double)x.Total);
            if (teamSum <= 0d) teamSum = 1d;
            double top = uiList.Max(x => (double)x.Total);
            if (top <= 0d) top = 1d;
            lock (_dataLock)
            {
                if (_isClearing == 1) return;

                // 1) Take snapshot of current list, use it for all enumeration-related calculations
                var snapshot = list
                    .GroupBy(pb => pb.ID)
                    .Select(g => g.Last())
                    .ToList();

                var present = new HashSet<long>(ordered.Select(x => x.Uid));

                // 2) First calculate old rows to delete using snapshot (avoid directly enumerating original list)
                var toRemove = snapshot.Where(pb => !present.Contains(pb.ID))
                                       .Select(pb => pb.ID)
                                       .ToList();

                // 3) Build index based on snapshot, faster and safer for later lookups
                var byId = new Dictionary<long, ProgressBarData>(snapshot.Count);
                foreach (var pb in snapshot) byId[pb.ID] = pb;

                // 4) Prepare a "next frame" new list, replace all at once at the end
                var next = new List<ProgressBarData>(present.Count);

                for (int i = 0; i < ordered.Count; i++)
                {
                    var p = ordered[i];


                    float ratio = (float)(p.Total / top);
                    if (!float.IsFinite(ratio)) ratio = 0f;
                    ratio = Math.Clamp(ratio, 0f, 1f);

                    string totalFmt = Common.FormatWithEnglishUnits(p.Total);
                    string perSec = Common.FormatWithEnglishUnits(Math.Round(p.PerSecond, 1));

                    var iconKey = (p?.Profession is string pr && pr != Properties.Strings.Profession_Unknown && imgDict.ContainsKey(pr)) ? pr
                                : (p?.SubProfession is string sr && sr != Properties.Strings.Profession_Unknown && imgDict.ContainsKey(sr)) ? sr
                                : Properties.Strings.Profession_Unknown;

                    var profBmp = imgDict.TryGetValue(iconKey, out var bmp) ? bmp : EmptyBitmap(); ;

                    var colorMap = Config.IsLight ? colorDict : blackColorDict;

                    var colorKey = (p?.Profession is string pr2 && pr2 != Properties.Strings.Profession_Unknown && colorMap.ContainsKey(pr2)) ? pr2
                                 : (p?.SubProfession is string sr2 && sr2 != Properties.Strings.Profession_Unknown && colorMap.ContainsKey(sr2)) ? sr2
                                 : Properties.Strings.Profession_Unknown;

                    var color = colorMap.TryGetValue(colorKey, out var c) ? c : ColorTranslator.FromHtml("#67AEF6");

                    // Render row content: DictList also only modified within lock
                    if (!DictList.TryGetValue(p.Uid, out var row))
                    {
                        row = [
                            new() { Type = RenderContent.ContentType.Image, Align = RenderContent.ContentAlign.MiddleLeft, Offset =AppConfig.ProgressBarImage, Image = profBmp, ImageRenderSize = AppConfig.ProgressBarImageSize },
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleLeft, Offset =AppConfig.ProgressBarNmae, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont},
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset = AppConfig.ProgressBarHarm, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset =AppConfig.ProgressBarProportion,  ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                        ];
                        DictList[p.Uid] = row;
                    }

                    string share = $"{Math.Round(p.Total / teamSum * 100d, 0, MidpointRounding.AwayFromZero)}%";
                    row[0].Image = profBmp;
                    // Only sub-profession; if no sub-profession use combat power; otherwise only show nickname
                    string sp = Common.GetTranslatedSubProfession(p.SubProfession);

                    row[1].Text = $"{p.Nickname}-{sp}({p.CombatPower})"; //TODO come back here, update subprofession when changing language


                    row[2].Text = $"{totalFmt} ({perSec})";
                    row[3].Text = share;

                    if (p.Uid == (long)AppConfig.Uid)
                    {
                        label1.Text = $" [{i + 1}]";
                        label2.Text = $"{totalFmt} ({perSec})";
                    }

                    // Reuse old ProgressBarData to avoid UI jitter; create new if none exists
                    if (!byId.TryGetValue(p.Uid, out var pb))
                    {
                        pb = new ProgressBarData
                        {
                            ID = p.Uid,
                            ContentList = row,
                            ProgressBarCornerRadius = 3,
                            ProgressBarValue = ratio,
                            ProgressBarColor = color,
                        };
                    }
                    else
                    {
                        pb.ContentList = row;      // Fallback sync
                        pb.ProgressBarValue = ratio;
                        pb.ProgressBarColor = color;
                    }

                    next.Add(pb);
                }

                // 5) Handle DictList deletion (optional, keep clean)
                if (toRemove.Count > 0)
                {
                    foreach (var uid in toRemove)
                        DictList.Remove(uid);
                }

                // 6) Replace list all at once to avoid "modification during enumeration"
                list = next;

                // RefreshDpsTable(...) — final binding within lock
                void Bind()
                {
                    sortedProgressBarList1.Data = list; // list is never null
                }

                if (sortedProgressBarList1.InvokeRequired) sortedProgressBarList1.BeginInvoke((Action)Bind);
                else Bind();
            }
        }
        #region NPC damage taken and player damage ranking against specific NPCs
        #region Full record
        /// <summary>Refresh: NPC damage taken overview (full record FullRecord)</summary>
        public void RefreshNpcOverview()
        {
            if (Interlocked.CompareExchange(ref _isClearing, 0, 0) == 1) return;

            // 1) Build NPC rows based on current view
            var uiList = FormManager.showTotal
                ? BuildNpcOverviewRows_FullRecord()
                : BuildNpcOverviewRows_Current();

            // 2) Clear UI if empty list
            if (uiList.Count == 0)
            {
                if (sortedProgressBarList1.InvokeRequired)
                    sortedProgressBarList1.BeginInvoke(() => sortedProgressBarList1.Data = new List<ProgressBarData>());
                else
                    sortedProgressBarList1.Data = new List<ProgressBarData>();
                return;
            }

            // 3) Render (consistent with original full record logic)
            var ordered = uiList.OrderByDescending(x => x.TotalTaken).ToList();
            double teamSum = uiList.Sum(x => (double)x.TotalTaken);
            if (teamSum <= 0d) teamSum = 1d;
            double top = uiList.Max(x => (double)x.TotalTaken);
            if (top <= 0d) top = 1d;

            lock (_dataLock)
            {
                if (_isClearing == 1) return;

                var snapshot = list.GroupBy(pb => pb.ID).Select(g => g.Last()).ToList();
                var present = new HashSet<long>(ordered.Select(x => x.NpcId));
                var toRemove = snapshot.Where(pb => !present.Contains(pb.ID)).Select(pb => pb.ID).ToList();
                var byId = snapshot.ToDictionary(pb => pb.ID, pb => pb);

                var next = new List<ProgressBarData>(present.Count);

                for (int i = 0; i < ordered.Count; i++)
                {
                    var p = ordered[i];

                    float ratio = (float)(p.TotalTaken / top);
                    if (!float.IsFinite(ratio)) ratio = 0f;
                    ratio = Math.Clamp(ratio, 0f, 1f);

                    string totalFmt = Common.FormatWithEnglishUnits(p.TotalTaken);
                    string perSec = Common.FormatWithEnglishUnits(Math.Round(p.TakenPerSec, 1));
                    string share = $"{Math.Round(p.TotalTaken / teamSum * 100d, 0, MidpointRounding.AwayFromZero)}%";

                    // Avatar & color (use "unknown")
                    var profBmp = imgDict.TryGetValue(Properties.Strings.Profession_Unknown, out var bmp) ? bmp : EmptyBitmap();
                    var colorMap = Config.IsLight ? colorDict : blackColorDict;
                    var color = colorMap.TryGetValue(Properties.Strings.Profession_Unknown, out var c) ? c : ColorTranslator.FromHtml("#67AEF6");

                    if (!DictList.TryGetValue(p.NpcId, out var row))
                    {
                        row = [
                            new() { Type = RenderContent.ContentType.Image, Align = RenderContent.ContentAlign.MiddleLeft, Offset =AppConfig.ProgressBarImage, Image = profBmp, ImageRenderSize = AppConfig.ProgressBarImageSize },
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleLeft, Offset =AppConfig.ProgressBarNmae, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont},
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset = AppConfig.ProgressBarHarm, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset =AppConfig.ProgressBarProportion,  ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                        ];
                        DictList[p.NpcId] = row;
                    }

                    row[0].Image = profBmp;
                    row[1].Text = p.Name;
                    row[2].Text = $"{totalFmt}({perSec})";
                    row[3].Text = share;

                    if (!byId.TryGetValue(p.NpcId, out var pb))
                    {
                        pb = new ProgressBarData
                        {
                            ID = p.NpcId,
                            ContentList = row,
                            ProgressBarCornerRadius = 3,
                            ProgressBarValue = ratio,
                            ProgressBarColor = color,
                        };
                    }
                    else
                    {
                        pb.ContentList = row;
                        pb.ProgressBarValue = ratio;
                        pb.ProgressBarColor = color;
                    }

                    next.Add(pb);
                }

                if (toRemove.Count > 0)
                    foreach (var id in toRemove) DictList.Remove(id);

                list = next;

                void Bind() => sortedProgressBarList1.Data = list;
                if (sortedProgressBarList1.InvokeRequired) sortedProgressBarList1.BeginInvoke(Bind);
                else Bind();
            }
        }


        /// <summary>Refresh: attacker ranking for a specific NPC (full record FullRecord)</summary>
        public void RefreshNpcAttackers(ulong npcId)
        {
            if (Interlocked.CompareExchange(ref _isClearing, 0, 0) == 1) return;

            var uiList = FormManager.showTotal
                ? BuildNpcAttackerRows_FullRecord(npcId)
                : BuildNpcAttackerRows_Current(npcId);

            if (uiList.Count == 0)
            {
                if (sortedProgressBarList1.InvokeRequired)
                    sortedProgressBarList1.BeginInvoke(() => sortedProgressBarList1.Data = new List<ProgressBarData>());
                else
                    sortedProgressBarList1.Data = new List<ProgressBarData>();
                return;
            }

            var ordered = uiList.OrderByDescending(x => x.DamageToNpc).ToList();
            double npcSum = uiList.Sum(x => (double)x.DamageToNpc);
            if (npcSum <= 0d) npcSum = 1d;
            double top = uiList.Max(x => (double)x.DamageToNpc);
            if (top <= 0d) top = 1d;

            lock (_dataLock)
            {
                if (_isClearing == 1) return;

                var snapshot = list.GroupBy(pb => pb.ID).Select(g => g.Last()).ToList();
                var present = new HashSet<long>(ordered.Select(x => x.Uid));
                var toRemove = snapshot.Where(pb => !present.Contains(pb.ID)).Select(pb => pb.ID).ToList();
                var byId = snapshot.ToDictionary(pb => pb.ID, pb => pb);

                var next = new List<ProgressBarData>(present.Count);

                for (int i = 0; i < ordered.Count; i++)
                {
                    var p = ordered[i];

                    float ratio = (float)(p.DamageToNpc / top);
                    if (!float.IsFinite(ratio)) ratio = 0f;
                    ratio = Math.Clamp(ratio, 0f, 1f);

                    string totalFmt = Common.FormatWithEnglishUnits(p.DamageToNpc);
                    string perSec = Common.FormatWithEnglishUnits(Math.Round(p.NpcOnlyDps, 1));
                    string share = $"{Math.Round(p.DamageToNpc / npcSum * 100d, 0, MidpointRounding.AwayFromZero)}%";

                    var profBmp = imgDict.TryGetValue(p.Profession ?? Properties.Strings.Profession_Unknown, out var bmp) ? bmp : EmptyBitmap();
                    var colorMap = Config.IsLight ? colorDict : blackColorDict;
                    var color = colorMap.TryGetValue(p.Profession ?? Properties.Strings.Profession_Unknown, out var c) ? c : ColorTranslator.FromHtml("#67AEF6");

                    if (!DictList.TryGetValue(p.Uid, out var row))
                    {
                        row = [
                            new() { Type = RenderContent.ContentType.Image, Align = RenderContent.ContentAlign.MiddleLeft, Offset =AppConfig.ProgressBarImage, Image = profBmp, ImageRenderSize = AppConfig.ProgressBarImageSize },
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleLeft, Offset =AppConfig.ProgressBarNmae, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont},
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset = AppConfig.ProgressBarHarm, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                            new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset =AppConfig.ProgressBarProportion,  ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                        ];
                        DictList[p.Uid] = row;
                    }

                    row[0].Image = profBmp;
                    row[1].Text = $"{p.Nickname}-{p.SubProfession}({p.CombatPower})";
                    row[2].Text = $"{totalFmt}({perSec})";
                    row[3].Text = share;

                    if (!byId.TryGetValue(p.Uid, out var pb))
                    {
                        pb = new ProgressBarData
                        {
                            ID = p.Uid,
                            ContentList = row,
                            ProgressBarCornerRadius = 3,
                            ProgressBarValue = ratio,
                            ProgressBarColor = color,
                        };
                    }
                    else
                    {
                        pb.ContentList = row;
                        pb.ProgressBarValue = ratio;
                        pb.ProgressBarColor = color;
                    }

                    next.Add(pb);
                }

                if (toRemove.Count > 0)
                    foreach (var id in toRemove) DictList.Remove(id);

                list = next;

                void Bind() => sortedProgressBarList1.Data = list;
                if (sortedProgressBarList1.InvokeRequired) sortedProgressBarList1.BeginInvoke((Action)Bind);
                else Bind();
            }
        }

        #endregion
        #region Single battle
        /// <summary>Build: NPC damage taken overview (current Current)</summary>
        private List<NpcRow> BuildNpcOverviewRows_Current()
        {
            // First drive one real-time window update (smoother if available; no harm if not)
            StatisticData._npcManager.UpdateAllRealtime();

            var ids = StatisticData._npcManager.GetAllNpcIds();
            if (ids == null || ids.Count == 0) return new();

            var list = new List<NpcRow>(ids.Count);
            foreach (var id in ids)
            {
                var name = StatisticData._npcManager.GetNpcName(id);
                var ov = StatisticData._npcManager.GetNpcOverview(id);
                if (ov.TotalTaken == 0) continue;

                // Damage taken PS: use Total / ActiveSeconds, more stable; if you prefer real-time window, can replace with ov.RealtimeTaken
                var perSec = StatisticData._npcManager.GetNpcTakenPerSecond(id);

                list.Add(new NpcRow
                {
                    NpcId = (long)id,
                    Name = name,
                    TotalTaken = ov.TotalTaken,
                    TakenPerSec = perSec
                });
            }

            return list.OrderByDescending(r => r.TotalTaken).ToList();
        }
        /// <summary>Build: attacker ranking for specified NPC (current Current)</summary>
        private List<NpcAttackerRow> BuildNpcAttackerRows_Current(ulong npcId, int topN = 20)
        {
            // First refresh real-time window to ensure Realtime values and ActiveSeconds are closer to current
            StatisticData._npcManager.UpdateAllRealtime();

            // First use existing Top list to get "total damage to NPC/basic info/player full record DPS"
            var top = StatisticData._npcManager.GetNpcTopAttackers(npcId, topN);
            if (top == null || top.Count == 0) return new();

            var rows = new List<NpcAttackerRow>(top.Count);
            foreach (var t in top)
            {
                // Exclusive DPS: use player's StatisticData for this NPC (Total / ActiveSeconds)
                var npcOnlyDps = StatisticData._npcManager.GetPlayerNpcOnlyDps(npcId, t.Uid);

                rows.Add(new NpcAttackerRow
                {
                    Uid = (long)t.Uid,
                    Nickname = t.Nickname,
                    CombatPower = t.CombatPower,
                    Profession = t.Profession,
                    SubProfession = StatisticData._manager.GetOrCreate(t.Uid).SubProfession ?? "",
                    DamageToNpc = t.DamageToNpc,
                    PlayerDps = t.TotalDps,
                    NpcOnlyDps = npcOnlyDps
                });
            }

            return rows
                .Where(r => r.DamageToNpc > 0)
                .OrderByDescending(r => r.DamageToNpc)
                .ToList();
        }

        #endregion
        #endregion

        private List<UiRow> BuildUiRows(SourceType source, MetricType metric)
        {
            // # UI refresh event: build lightweight row structure for display based on data source (decoupled from underlying statistics objects)
            if (source == SourceType.Current)
            {
                var statsList = StatisticData._manager.GetPlayersWithCombatData().ToArray();
                if (statsList.Length == 0) return new();



                return statsList.Select(p =>
                {
                    ulong total;
                    double ps;
                    switch (metric)
                    {
                        case MetricType.Healing:
                            total = p.HealingStats.Total;
                            ps = p.HealingStats.GetTotalPerSecond();
                            break;
                        case MetricType.Taken:
                            total = p.TakenStats.Total;
                            ps = p.TakenStats.GetTotalPerSecond();
                            break;
                        default: // Damage
                            total = p.DamageStats.Total;
                            ps = p.DamageStats.GetTotalPerSecond();
                            break;
                    }

                    return new UiRow
                    {
                        Uid = (long)p.Uid,
                        Nickname = p.Nickname,
                        CombatPower = p.CombatPower,
                        Profession = p.Profession,
                        SubProfession = p.SubProfession ?? Properties.Strings.Profession_Unknown,
                        Total = total,
                        PerSecond = ps
                    };
                }).ToList();
            }
            else // FullRecord
            {
                var fr = FullRecord.GetPlayersWithTotalsArray();
                if (fr.Length == 0) return new();



                var sessionSecs = Math.Max(1.0, FullRecord.GetSessionTotalTimeSpan().TotalSeconds);

                return fr.Select(p =>
                {
                    ulong total;
                    double ps;
                    switch (metric)
                    {
                        case MetricType.Healing:
                            total = p.TotalHealing;
                            ps = p.Hps;
                            break;
                        case MetricType.Taken:
                            total = p.TakenDamage;
                            ps = total / sessionSecs;
                            break;
                        default: // Damage
                            total = p.TotalDamage;
                            ps = p.Dps;
                            break;
                    }

                    return new UiRow
                    {
                        Uid = (long)p.Uid,
                        Nickname = p.Nickname,
                        CombatPower = p.CombatPower,
                        Profession = p.Profession,
                        SubProfession = p.SubProfession ?? "",
                        Total = total,
                        PerSecond = ps
                    };
                }).ToList();
            }
        }

        /// <summary>Build: NPC damage taken overview (full record FullRecord)</summary>
        private List<NpcRow> BuildNpcOverviewRows_FullRecord()
        {
            var snap = FullRecord.TakeSnapshot();
            if (snap.Npcs == null || snap.Npcs.Count == 0) return new();

            // Convert to list and sort by total damage taken descending
            var list = snap.Npcs.Values
                .Select(n => new NpcRow
                {
                    NpcId = (long)n.NpcId,
                    Name = n.Name ?? $"NPC[{n.NpcId}]",
                    TotalTaken = n.TotalTaken,
                    TakenPerSec = n.TakenPerSec
                })
                .Where(r => r.TotalTaken > 0)
                .OrderByDescending(r => r.TotalTaken)
                .ToList();

            return list;
        }

        /// <summary>Build: attacker ranking for specified NPC (full record FullRecord)</summary>
        private List<NpcAttackerRow> BuildNpcAttackerRows_FullRecord(ulong npcId, int topN = 20)
        {
            var top = FullRecord.GetNpcTopAttackers(npcId, topN);
            if (top == null || top.Count == 0) return new();


            // Map FullRecord returned items to UI rows
            var rows = top.Select(t => new NpcAttackerRow
            {
                Uid = (long)t.Uid,
                Nickname = t.Nickname,
                CombatPower = t.CombatPower,
                Profession = t.Profession,
                SubProfession = StatisticData._manager.GetOrCreate(t.Uid).SubProfession ?? "",
                DamageToNpc = t.DamageToNpc,
                PlayerDps = t.PlayerDps,
                NpcOnlyDps = t.NpcOnlyDps
            })
            .Where(r => r.DamageToNpc > 0)
            .OrderByDescending(r => r.DamageToNpc)
            .ToList();

            return rows;
        }

    }
}
