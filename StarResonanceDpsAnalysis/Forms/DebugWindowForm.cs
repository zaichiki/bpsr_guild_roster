using AntdUI;
using StarResonanceDpsAnalysis.Plugin;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.Forms
{
    /// <summary>
    /// Master Score Window - Collect and send player master score data
    /// </summary>
    public partial class DebugWindowForm : BorderlessForm
    {
        // Global hotkey registration
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        private const int HOTKEY_ID = 9000;
        private const uint MOD_NONE = 0x0000;
        private const uint VK_F8 = 0x77; // F8 key
        /// <summary>
        /// Constructor - Initialize the debug window
        /// </summary>
        public DebugWindowForm()
        {
            InitializeComponent();
            FormGui.SetDefaultGUI(this); // Apply unified default GUI styling
            FormGui.SetColorMode(this, AppConfig.IsLight); // Set form color theme
            
            // Set up fonts from resources
            SetDefaultFontFromResources();
            
            // Make the form draggable by the header
            SetupDraggableHeader();
            
            // Enable keyboard input handling
            this.KeyPreview = true;
            this.KeyDown += DebugWindowForm_KeyDown;
        }

        /// <summary>
        /// Set default fonts from application resources
        /// </summary>
        private void SetDefaultFontFromResources()
        {
            TitleText.Font = AppConfig.SaoFont;
            input_Output.Font = new Font("Consolas", 9F);
            button_Test1.Font = AppConfig.ContentFont;
            button_Test2.Font = AppConfig.ContentFont;
            button_Clear.Font = AppConfig.ContentFont;
            button_Close.Font = AppConfig.ContentFont;
        }

        /// <summary>
        /// Form load event handler
        /// </summary>
        private void DebugWindowForm_Load(object sender, EventArgs e)
        {
            // Enable always on top (same as other forms)
            EnsureTopMost();
            
            // Register global hotkey (F8) to cancel automation
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_NONE, VK_F8);
            
            // Initialize with welcome message
            AppendDebugText("Master Score Collector initialized...");
            AppendDebugText($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            AppendDebugText("Press F8 anytime to cancel automation");
            AppendDebugText("");
            
            // Show current collection status
            UpdateCollectionStatus();
        }

        /// <summary>
        /// Ensure form is always on top
        /// </summary>
        private void EnsureTopMost()
        {
            TopMost = false;   // Turn off then on to force style refresh
            TopMost = true;
            Activate();
            BringToFront();
        }

        /// <summary>
        /// Form closing event handler
        /// </summary>
        private void DebugWindowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Unregister global hotkey
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            
            // Hide instead of close to prevent disposal
            e.Cancel = true;
            this.Hide();
        }

        /// <summary>
        /// Override WndProc to handle global hotkey messages
        /// </summary>
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                // F8 pressed - cancel automation if running
                if (_automationRunning)
                {
                    AppendDebugText("[INFO] F8 pressed - Cancelling automation...");
                    _automationCts?.Cancel();
                }
            }
            
            base.WndProc(ref m);
        }

        /// <summary>
        /// Handle keyboard shortcuts (for when form has focus)
        /// </summary>
        private void DebugWindowForm_KeyDown(object sender, KeyEventArgs e)
        {
            // ESC key cancels the running automation (when form has focus)
            if (e.KeyCode == Keys.Escape && _automationRunning)
            {
                AppendDebugText("[INFO] ESC pressed - Cancelling automation...");
                _automationCts?.Cancel();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Close button click handler
        /// </summary>
        private void button_Close_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        /// <summary>
        /// Clear button click handler - Clears collected Master Score data
        /// </summary>
        private void button_Clear_Click(object sender, EventArgs e)
        {
            var result = AntdUI.Modal.open(new AntdUI.Modal.Config(this, "Clear Master Score Data", 
                "Are you sure you want to clear all collected Master Score data?")
            {
                OkText = "Yes, Clear",
                CancelText = "Cancel",
                OkType = AntdUI.TTypeMini.Error
            });
            
            if (result == DialogResult.OK)
            {
                var (_, count) = Core.MasterScoreCollector.GetStatus();
                Core.MasterScoreCollector.ClearData();
                AppendDebugText($"[INFO] Cleared {count} Master Score records");
                AppendDebugText("");
                UpdateCollectionStatus();
            }
        }

        /// <summary>
        /// Send to API button click handler
        /// </summary>
        private async void button_Test1_Click(object sender, EventArgs e)
        {
            try
            {
                // Read API key from config (using Prod section)
                var config = Core.GuildRosterServiceConfigHelper.ReadGuildRosterServiceConfig("GuildRosterServiceProd");
                
                if (string.IsNullOrEmpty(config.ApiKey))
                {
                    AppendDebugText("[ERROR] API Key not configured!");
                    AppendDebugText("Please set ApiKey in private_config.ini under [GuildRosterServiceProd] section.");
                    return;
                }
                
                // Use the specified endpoint
                string apiEndpoint = "https://orca-app-xsrfn.ondigitalocean.app/members/masterscore";
                
                AppendDebugText("========================================");
                AppendDebugText($"[{DateTime.Now:HH:mm:ss}] Sending Master Score data to API");
                AppendDebugText($"Endpoint: {apiEndpoint}");
                AppendDebugText("========================================");
                
                var (isCollecting, count) = Core.MasterScoreCollector.GetStatus();
                AppendDebugText($"Records to send: {count}");
                
                if (count == 0)
                {
                    AppendDebugText("[WARNING] No data collected yet!");
                    AppendDebugText("Run automation first to collect Master Score data.");
                    return;
                }
                
                AppendDebugText("Sending data...");
                bool success = await Core.MasterScoreCollector.SendToApiAsync(apiEndpoint, config.ApiKey);
                
                if (success)
                {
                    AppendDebugText("[SUCCESS] Data sent successfully!");
                    AppendDebugText("Check console for API response details.");
                }
                else
                {
                    AppendDebugText("[ERROR] Failed to send data. Check console for details.");
                }
                
                AppendDebugText("========================================");
            }
            catch (Exception ex)
            {
                AppendDebugText($"[ERROR] Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update collection status display
        /// </summary>
        private void UpdateCollectionStatus()
        {
            var (isCollecting, count) = Core.MasterScoreCollector.GetStatus();
            AppendDebugText($"Collection Status: {(isCollecting ? "ACTIVE" : "INACTIVE")}");
            AppendDebugText($"Records Collected: {count}");
        }

        /// <summary>
        /// Test button 2 click handler
        /// </summary>
        private CancellationTokenSource? _automationCts;
        private bool _automationRunning = false;

        private async void button_Test2_Click(object sender, EventArgs e)
        {
            // If automation is running, cancel it
            if (_automationRunning)
            {
                AppendDebugText("[INFO] Cancelling automation...");
                _automationCts?.Cancel();
                button_Test2.Text = "Auto-Click Guild Roster";
                return;
            }

            try
            {
                AppendDebugText("========================================");
                AppendDebugText($"[{DateTime.Now:HH:mm:ss.fff}] Starting Guild Roster Automation");
                AppendDebugText("========================================");
                AppendDebugText("");

                // Configuration (adjust these based on your game resolution/UI)
                // For 1440p (2560x1440) - User adjusted coordinates:
                int memberCount = 83;           // Test scrolling with 10 members
                int startX = 300;               // X position of member name (in Player column)
                int startY = 436;               // Y position of first member "Zaichiki"
                int offsetY = 156;              // Vertical spacing between members
                int membersPerPage = 5;         // How many members visible at once
                int clickDelay = 200;          // Wait time between clicks (ms)
                
                // NOTE: Watch console for "[AUTOMATION] Drag scroll..." messages!

                AppendDebugText($"Configuration:");
                AppendDebugText($"  Total Members: {memberCount}");
                AppendDebugText($"  Click Position: ({startX}, {startY})");
                AppendDebugText($"  Members per Page: {membersPerPage}");
                AppendDebugText($"  Click Delay: {clickDelay}ms");
                AppendDebugText("");
                AppendDebugText("Press F8 to cancel automation (works even when game is focused)");
                AppendDebugText("NOTE: Window must stay focused on the guild roster.");
                AppendDebugText("");

                // Change button text to show it can cancel
                _automationRunning = true;
                button_Test2.Text = "STOP Automation";
                _automationCts = new CancellationTokenSource();

                // Progress callback
                void ProgressCallback(int current, string message)
                {
                    AppendDebugText($"[{current}/{memberCount}] {message}");
                }

                // Run automation
                await Core.GameAutomation.AutoClickGuildRoster(
                    memberCount,
                    startX,
                    startY,
                    offsetY,
                    membersPerPage,
                    clickDelay,
                    ProgressCallback,
                    _automationCts.Token
                );

                AppendDebugText("");
                AppendDebugText("========================================");
                AppendDebugText("Automation completed successfully!");
                AppendDebugText("Check the outbound packet monitor for captured data.");
                AppendDebugText("========================================");
            }
            catch (OperationCanceledException)
            {
                AppendDebugText("");
                AppendDebugText("[INFO] Automation cancelled by user.");
            }
            catch (Exception ex)
            {
                AppendDebugText("");
                AppendDebugText($"[ERROR] Automation failed: {ex.Message}");
                AppendDebugText($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _automationRunning = false;
                button_Test2.Text = "Auto-Click Guild Roster";
                _automationCts?.Dispose();
                _automationCts = null;
            }
        }

        /// <summary>
        /// Append text to debug output (thread-safe)
        /// </summary>
        public void AppendDebugText(string text)
        {
            if (input_Output.InvokeRequired)
            {
                input_Output.Invoke(new Action(() => AppendDebugText(text)));
                return;
            }

            input_Output.Text += text + "\r\n";
            
            // Auto-scroll to bottom
            input_Output.SelectionStart = input_Output.Text.Length;
            input_Output.ScrollToCaret();
        }

        /// <summary>
        /// Clear debug output (thread-safe)
        /// </summary>
        public void ClearDebugText()
        {
            if (input_Output.InvokeRequired)
            {
                input_Output.Invoke(new Action(ClearDebugText));
                return;
            }

            input_Output.Text = string.Empty;
        }

        #region Draggable Header Functionality

        private bool _isDragging = false;
        private Point _lastMousePosition;

        /// <summary>
        /// Set up draggable header functionality
        /// </summary>
        private void SetupDraggableHeader()
        {
            // Make the page header draggable
            pageHeader1.MouseDown += PageHeader_MouseDown;
            pageHeader1.MouseMove += PageHeader_MouseMove;
            pageHeader1.MouseUp += PageHeader_MouseUp;
            
            // Also make the title text draggable
            TitleText.MouseDown += PageHeader_MouseDown;
            TitleText.MouseMove += PageHeader_MouseMove;
            TitleText.MouseUp += PageHeader_MouseUp;
        }

        /// <summary>
        /// Handle mouse down on header to start dragging
        /// </summary>
        private void PageHeader_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _lastMousePosition = e.Location;
                pageHeader1.Cursor = Cursors.SizeAll;
            }
        }

        /// <summary>
        /// Handle mouse move on header to drag the form
        /// </summary>
        private void PageHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.Button == MouseButtons.Left)
            {
                Point currentPosition = this.PointToScreen(e.Location);
                Point newLocation = new Point(
                    currentPosition.X - _lastMousePosition.X,
                    currentPosition.Y - _lastMousePosition.Y
                );
                this.Location = newLocation;
            }
        }

        /// <summary>
        /// Handle mouse up on header to stop dragging
        /// </summary>
        private void PageHeader_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
                pageHeader1.Cursor = Cursors.Default;
            }
        }

        #endregion
    }
}

