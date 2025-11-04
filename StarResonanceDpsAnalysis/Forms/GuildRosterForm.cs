using AntdUI;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.Forms
{
    /// <summary>
    /// Guild Roster Form - Displays guild member information in a table format
    /// </summary>
    public partial class GuildRosterForm : BorderlessForm
    {
        /// <summary>
        /// Discord member data structure
        /// </summary>
        public class DiscordMemberData
        {
            public string ServerNickname { get; set; } = string.Empty;
            public string Nickname { get; set; } = string.Empty;
            public bool HasGuildMemberRole { get; set; } = false;
        }

        private List<DiscordMemberData> _discordMembers = new List<DiscordMemberData>();
        /// <summary>
        /// Constructor - Initialize the form and set up UI components
        /// </summary>
        public GuildRosterForm()
        {
            InitializeComponent();
            FormGui.SetDefaultGUI(this); // Apply unified default GUI styling (font, spacing, shadows, etc.)
            FormGui.SetColorMode(this, AppConfig.IsLight); // Set form color theme based on configuration (light/dark)
            
            // Set up fonts from resources
            SetDefaultFontFromResources();
            
            // Update button visibility based on config availability
            UpdateButtonVisibility();
            
            // Subscribe to guild data updates
            GuildRosterExporter.GuildDataUpdated += OnGuildDataUpdated;
            
            // Initialize the table view
            InitializeTableView();
            
            // Make the form draggable by the header
            SetupDraggableHeader();
            
            // Load Discord data silently
            LoadDiscordDataAsync();
        }

        /// <summary>
        /// Update button visibility based on configuration availability
        /// </summary>
        private void UpdateButtonVisibility()
        {
            // Check ClientId for button_ExportSpreadsheet
            // First check secret_config.ini, then fall back to private_config.ini
            bool hasClientId = false;
            try
            {
                // Check secret_config.ini first
                var secretConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secret_config.ini");
                if (File.Exists(secretConfigPath))
                {
                    var lines = File.ReadAllLines(secretConfigPath);
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("ClientId", StringComparison.OrdinalIgnoreCase) && trimmedLine.Contains("="))
                        {
                            var parts = trimmedLine.Split('=', 2);
                            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1].Trim()))
                            {
                                hasClientId = true;
                                break;
                            }
                        }
                    }
                }
                
                // Fall back to checking private_config.ini if not found in secret_config.ini
                if (!hasClientId)
                {
                    var config = GoogleSheetsConfigHelper.ReadGoogleSheetsConfig();
                    hasClientId = !string.IsNullOrEmpty(config.ClientId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking ClientId configuration: {ex.Message}");
            }
            button_ExportSpreadsheet.Visible = hasClientId;

            // Check PortraitService URL
            var portraitConfig = PortraitServiceConfigHelper.ReadPortraitServiceConfig();
            button_SendPortraits.Visible = !string.IsNullOrEmpty(portraitConfig.Url);

            // Check PortraitServiceProd URL
            var portraitProdConfig = PortraitServiceConfigHelper.ReadPortraitServiceConfig("PortraitServiceProd");
            button_SendPortraitsProd.Visible = !string.IsNullOrEmpty(portraitProdConfig.Url);

            // Check GuildRosterService URL
            var guildRosterConfig = GuildRosterServiceConfigHelper.ReadGuildRosterServiceConfig("GuildRosterService");
            button_SendDataTest.Visible = !string.IsNullOrEmpty(guildRosterConfig.Url);

            // Check GuildRosterServiceProd URL
            var guildRosterProdConfig = GuildRosterServiceConfigHelper.ReadGuildRosterServiceConfig("GuildRosterServiceProd");
            button_SendDataProd.Visible = !string.IsNullOrEmpty(guildRosterProdConfig.Url);
        }

        /// <summary>
        /// Set default fonts from application resources
        /// </summary>
        private void SetDefaultFontFromResources()
        {
            TitleText.Font = AppConfig.SaoFont;
            table_GuildRoster.Font = AppConfig.ContentFont;
            button_Close.Font = AppConfig.ContentFont;
            button_SendPortraits.Font = AppConfig.ContentFont;
            button_SendPortraitsProd.Font = AppConfig.ContentFont;
            button_SendDataTest.Font = AppConfig.ContentFont;
            button_SendDataProd.Font = AppConfig.ContentFont;
        }

        /// <summary>
        /// Initialize the table view with guild data columns
        /// </summary>
        private void InitializeTableView()
        {
            // Clear existing columns
            table_GuildRoster.Columns.Clear();

            // Define table columns for guild roster
            table_GuildRoster.Columns = new AntdUI.ColumnCollection
            {
                // Row number column
                new("", "No.")
                {
                    Render = (value, record, rowIndex) => rowIndex + 1,
                    Fixed = true
                },
                // Player name column
                new AntdUI.Column("PlayerName", "Player Name") 
                { 
                    Fixed = true 
                },
                // Player level column
                new AntdUI.Column("CharacterLevel", "Level") 
                { 
                    Fixed = true,
                    SortOrder = true 
                },
                // Player class column
                new AntdUI.Column("ClassDisplay", "Class") 
                { 
                    Fixed = true 
                },
                // Gear score column
                new AntdUI.Column("GearScore", "Gear Score") 
                { 
                    Fixed = true,
                    SortOrder = true 
                },                
                // Last login column
                new AntdUI.Column("LastLoginDisplay", "Last Login") 
                { 
                    Fixed = true 
                },
                // Join date column
                new AntdUI.Column("JoinDateDisplay", "Join Date") 
                { 
                    Fixed = true 
                },
                // Role column
                new AntdUI.Column("RoleDisplay", "Role") 
                { 
                    Fixed = true 
                },
                // Discord member status column
                new AntdUI.Column("DiscordIsMember", "Discord Member") 
                { 
                    Fixed = true 
                },
                // Discord name data column
                new AntdUI.Column("DiscordNameData", "Discord Name") 
                { 
                    Fixed = true 
                },
                // Discord has role column
                new AntdUI.Column("DiscordHasRole", "Has Guild Role") 
                { 
                    Fixed = true 
                }
            };

            // Set custom empty text
            table_GuildRoster.EmptyText = "No data available. Please open the guild roster in the game to update the data.";

            // Load current guild data if available
            LoadGuildData();
        }

        /// <summary>
        /// Load guild data from the exporter and bind to table
        /// </summary>
        private void LoadGuildData()
        {
            try
            {
                var guildData = GuildRosterExporter.CurrentGuildData.Values.ToList();
                
                if (guildData.Count > 0)
                {
                    // Populate Discord data for each guild member
                    PopulateDiscordData(guildData);
                    
                    var antList = new AntdUI.AntList<JoinedGuildMemberData>(guildData);
                    table_GuildRoster.Binding(antList);
                }
                else
                {
                    // Show empty table if no data
                    table_GuildRoster.Binding(new AntdUI.AntList<JoinedGuildMemberData>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading guild data: {ex.Message}");
                // Show empty table on error
                table_GuildRoster.Binding(new AntdUI.AntList<JoinedGuildMemberData>());
            }
        }

        /// <summary>
        /// Event handler for guild data updates
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void OnGuildDataUpdated(object? sender, EventArgs e)
        {
            // Ensure UI updates happen on the UI thread
            if (InvokeRequired)
            {
                Invoke(new Action(() => LoadGuildData()));
            }
            else
            {
                LoadGuildData();
            }
        }

        /// <summary>
        /// Form load event handler
        /// </summary>
        private void GuildRosterForm_Load(object sender, EventArgs e)
        {
            // Enable always on top (same as DpsStatisticsForm)
            EnsureTopMost();
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
        /// Close button click event handler
        /// </summary>
        private void button_Close_Click(object sender, EventArgs e)
        {
            this.Close(); // Close the form
        }

        /// <summary>
        /// Export button click event handler
        /// </summary>
        private void button_Export_Click(object sender, EventArgs e)
        {
            try
            {
                string filePath = GuildRosterExporter.ExportCurrentDataToTsv();
                MessageBox.Show($"Guild roster exported successfully to:\n{filePath}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting guild roster:\n{ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Send Portraits button click event handler
        /// </summary>
        private async void button_SendPortraits_Click(object sender, EventArgs e)
        {
            try
            {
                // Disable the button to prevent multiple clicks
                button_SendPortraits.Enabled = false;
                button_SendPortraits.Text = "Sending...";

                // Load PortraitService configuration
                var config = PortraitServiceConfigHelper.ReadPortraitServiceConfig();
                
                // Check if configuration is complete
                if (string.IsNullOrEmpty(config.Url) || string.IsNullOrEmpty(config.ApiKey))
                {
                    MessageBox.Show("PortraitService configuration is incomplete.\n\nPlease set the URL and API Key in private_config.ini under [PortraitService] section.", 
                        "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get current guild data
                var guildData = GuildRosterExporter.CurrentGuildData.Values.ToList();
                
                if (guildData.Count == 0)
                {
                    MessageBox.Show("No guild data available to send.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Convert guild data to portrait data
                var portraitData = new List<PortraitService.PortraitData>();
                foreach (var member in guildData)
                {
                    portraitData.Add(new PortraitService.PortraitData
                    {
                        PlayerId = member.UserId.ToString(),
                        PlayerName = member.PlayerName,
                        SquareImageUrl = member.SquareImageUrl,
                        VerticalImageUrl = member.VerticalImageUrl
                    });
                }

                // Create and use PortraitService
                using (var portraitService = new PortraitService(config.Url, config.ApiKey))
                {
                    bool success = await portraitService.SendPortraitsAsync(portraitData);
                    
                    if (success)
                    {
                        Console.WriteLine($"Successfully sent {portraitData.Count} portraits to the service");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending portraits:\n{ex.Message}", "Send Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Portrait send error: {ex}");
            }
            finally
            {
                // Re-enable the button
                button_SendPortraits.Enabled = true;
                button_SendPortraits.Text = "Send Portraits";
            }
        }

        /// <summary>
        /// Send Portraits to Production button click event handler
        /// </summary>
        private async void button_SendPortraitsProd_Click(object sender, EventArgs e)
        {
            try
            {
                // Disable the button to prevent multiple clicks
                button_SendPortraitsProd.Enabled = false;
                button_SendPortraitsProd.Text = "Sending...";

                // Load PortraitServiceProd configuration
                var config = PortraitServiceConfigHelper.ReadPortraitServiceConfig("PortraitServiceProd");
                
                // Check if configuration is complete
                if (string.IsNullOrEmpty(config.Url) || string.IsNullOrEmpty(config.ApiKey))
                {
                    MessageBox.Show("PortraitServiceProd configuration is incomplete.\n\nPlease set the URL and API Key in private_config.ini under [PortraitServiceProd] section.", 
                        "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get current guild data
                var guildData = GuildRosterExporter.CurrentGuildData.Values.ToList();
                
                if (guildData.Count == 0)
                {
                    MessageBox.Show("No guild data available to send.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Convert guild data to portrait data
                var portraitData = new List<PortraitService.PortraitData>();
                foreach (var member in guildData)
                {
                    portraitData.Add(new PortraitService.PortraitData
                    {
                        PlayerId = member.UserId.ToString(),
                        PlayerName = member.PlayerName,
                        SquareImageUrl = member.SquareImageUrl,
                        VerticalImageUrl = member.VerticalImageUrl
                    });
                }

                // Create and use PortraitService
                using (var portraitService = new PortraitService(config.Url, config.ApiKey))
                {
                    bool success = await portraitService.SendPortraitsAsync(portraitData);
                    
                    if (success)
                    {
                        Console.WriteLine($"Successfully sent {portraitData.Count} portraits to production service");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending portraits to production:\n{ex.Message}", "Send Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Portrait send to production error: {ex}");
            }
            finally
            {
                // Re-enable the button
                button_SendPortraitsProd.Enabled = true;
                button_SendPortraitsProd.Text = "Send to Prod";
            }
        }

        /// <summary>
        /// Send Data to Test endpoint button click event handler
        /// </summary>
        private async void button_SendDataTest_Click(object sender, EventArgs e)
        {
            try
            {
                // Disable the button to prevent multiple clicks
                button_SendDataTest.Enabled = false;
                button_SendDataTest.Text = "Sending...";

                // Load GuildRosterService configuration
                var config = GuildRosterServiceConfigHelper.ReadGuildRosterServiceConfig("GuildRosterService");
                
                // Check if configuration is complete
                if (string.IsNullOrEmpty(config.Url) || string.IsNullOrEmpty(config.ApiKey))
                {
                    MessageBox.Show("GuildRosterService configuration is incomplete.\n\nPlease set the URL and API Key in private_config.ini under [GuildRosterService] section.", 
                        "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get current guild data
                var guildData = GuildRosterExporter.CurrentGuildData.Values.ToList();
                
                if (guildData.Count == 0)
                {
                    MessageBox.Show("No guild data available to send.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Create and use GuildRosterService
                using (var guildRosterService = new GuildRosterService(config.Url, config.ApiKey))
                {
                    bool success = await guildRosterService.SendGuildDataAsync(guildData);
                    
                    if (success)
                    {
                        Console.WriteLine($"Successfully sent {guildData.Count} guild members to test service");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending guild data to test:\n{ex.Message}", "Send Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Guild data send to test error: {ex}");
            }
            finally
            {
                // Re-enable the button
                button_SendDataTest.Enabled = true;
                button_SendDataTest.Text = "data>test";
            }
        }

        /// <summary>
        /// Send Data to Production endpoint button click event handler
        /// </summary>
        private async void button_SendDataProd_Click(object sender, EventArgs e)
        {
            try
            {
                // Disable the button to prevent multiple clicks
                button_SendDataProd.Enabled = false;
                button_SendDataProd.Text = "Sending...";

                // Load GuildRosterServiceProd configuration
                var config = GuildRosterServiceConfigHelper.ReadGuildRosterServiceConfig("GuildRosterServiceProd");
                
                // Check if configuration is complete
                if (string.IsNullOrEmpty(config.Url) || string.IsNullOrEmpty(config.ApiKey))
                {
                    MessageBox.Show("GuildRosterServiceProd configuration is incomplete.\n\nPlease set the URL and API Key in private_config.ini under [GuildRosterServiceProd] section.", 
                        "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get current guild data
                var guildData = GuildRosterExporter.CurrentGuildData.Values.ToList();
                
                if (guildData.Count == 0)
                {
                    MessageBox.Show("No guild data available to send.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Create and use GuildRosterService
                using (var guildRosterService = new GuildRosterService(config.Url, config.ApiKey))
                {
                    bool success = await guildRosterService.SendGuildDataAsync(guildData);
                    
                    if (success)
                    {
                        Console.WriteLine($"Successfully sent {guildData.Count} guild members to production service");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending guild data to production:\n{ex.Message}", "Send Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Guild data send to production error: {ex}");
            }
            finally
            {
                // Re-enable the button
                button_SendDataProd.Enabled = true;
                button_SendDataProd.Text = "data>prod";
            }
        }

        /// <summary>
        /// Export to Spreadsheet button click event handler
        /// </summary>
        private async void button_ExportSpreadsheet_Click(object sender, EventArgs e)
        {
            try
            {
                // Disable the button to prevent multiple clicks
                button_ExportSpreadsheet.Enabled = false;
                button_ExportSpreadsheet.Text = "Exporting...";

                // Load Google Sheets configuration
                var config = GoogleSheetsConfigHelper.ReadGoogleSheetsConfig();
                
                // Check if configuration is complete
                if (!config.DocumentId.Any())
                {
                    MessageBox.Show("Google Sheets Document ID is not configured.\n\nPlease set the Document ID in config.ini under [GoogleSheets] section.", 
                        "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Create and authenticate with Google Sheets service
                var sheetsService = new GoogleSheetsService(new GoogleSheetsService.GoogleSheetsConfig
                {
                    ClientId = config.ClientId,
                    ClientSecret = config.ClientSecret,
                    DocumentId = config.DocumentId,
                    SheetName = config.SheetName
                });

                // Authenticate
                bool authenticated = await sheetsService.AuthenticateAsync();
                if (!authenticated)
                {
                    return; // Error message already shown in AuthenticateAsync
                }

                // Export the data
                bool success = await sheetsService.ExportGuildRosterAsync(GuildRosterExporter.CurrentGuildData);
                
                if (success)
                {
                    Console.WriteLine("Guild roster exported successfully to Google Sheets");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting to Google Sheets:\n{ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Google Sheets export error: {ex}");
            }
            finally
            {
                // Re-enable the button
                button_ExportSpreadsheet.Enabled = true;
                button_ExportSpreadsheet.Text = "Export to Spreadsheet";
            }
        }

        /// <summary>
        /// Form closing event handler
        /// </summary>
        private void GuildRosterForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Unsubscribe from events to prevent memory leaks
            GuildRosterExporter.GuildDataUpdated -= OnGuildDataUpdated;
            
            // Save form position if needed
            // AppConfig.GuildRosterFormState = new Rectangle(Left, Top, Width, Height);
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
        /// <param name="sender">Event sender</param>
        /// <param name="e">Mouse event arguments</param>
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
        /// <param name="sender">Event sender</param>
        /// <param name="e">Mouse event arguments</param>
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
        /// <param name="sender">Event sender</param>
        /// <param name="e">Mouse event arguments</param>
        private void PageHeader_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
                pageHeader1.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Populates Discord data for guild members by matching with Discord members
        /// </summary>
        private void PopulateDiscordData(List<JoinedGuildMemberData> guildData)
        {
            if (_discordMembers.Count == 0)
            {
                // No Discord data available, set defaults
                foreach (var member in guildData)
                {
                    member.DiscordIsMember = "no";
                    member.DiscordNameData = "";
                    member.DiscordServerNickname = "";
                    member.DiscordNickname = "";
                    member.DiscordHasRole = "false";
                }
                return;
            }

            foreach (var guildMember in guildData)
            {
                // Find best match in Discord members
                var bestMatch = FindBestDiscordMatch(guildMember.PlayerName);
                
                if (bestMatch != null)
                {
                    guildMember.DiscordNameData = $"{bestMatch.ServerNickname}|{bestMatch.Nickname}";
                    guildMember.DiscordServerNickname = bestMatch.ServerNickname;
                    guildMember.DiscordNickname = bestMatch.Nickname;
                    guildMember.DiscordHasRole = bestMatch.HasGuildMemberRole ? "true" : "false";
                    
                    // Determine membership status
                    if (string.Equals(guildMember.PlayerName, bestMatch.ServerNickname, StringComparison.OrdinalIgnoreCase))
                    {
                        guildMember.DiscordIsMember = "yes";
                    }
                    else
                    {
                        guildMember.DiscordIsMember = "maybe";
                    }
                }
                else
                {
                    guildMember.DiscordIsMember = "no";
                    guildMember.DiscordNameData = "";
                    guildMember.DiscordServerNickname = "";
                    guildMember.DiscordNickname = "";
                    guildMember.DiscordHasRole = "false";
                }
            }
        }

        /// <summary>
        /// Finds the best matching Discord member for a guild member name
        /// </summary>
        private DiscordMemberData? FindBestDiscordMatch(string guildMemberName)
        {
            if (string.IsNullOrEmpty(guildMemberName))
                return null;

            // Debug output for specific cases
            if (guildMemberName.Equals("TKPStefan", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"\n=== DEBUG: Looking for TKPStefan ===");
                Console.WriteLine($"Guild member name: '{guildMemberName}'");
                Console.WriteLine("Available Discord members:");
                foreach (var d in _discordMembers.Where(d => 
                    d.ServerNickname.Contains("TKPStefan", StringComparison.OrdinalIgnoreCase) ||
                    d.Nickname.Contains("TKPStefan", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"  ServerNickname: '{d.ServerNickname}', Nickname: '{d.Nickname}'");
                }
                Console.WriteLine("=== END DEBUG ===\n");
            }

            // First try exact match on server nickname
            var exactMatch = _discordMembers.FirstOrDefault(d => 
                string.Equals(d.ServerNickname, guildMemberName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            // Then try substring match on server nickname
            var substringMatch = _discordMembers.FirstOrDefault(d => 
                d.ServerNickname.Contains(guildMemberName, StringComparison.OrdinalIgnoreCase));
            if (substringMatch != null)
                return substringMatch;

            // Finally try fuzzy matching (simple Levenshtein distance)
            var bestFuzzyMatch = _discordMembers
                .Select(d => new { Member = d, Score = CalculateSimilarity(guildMemberName, d.ServerNickname) })
                .Where(x => x.Score >= 80) // 80% similarity threshold
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return bestFuzzyMatch?.Member;
        }

        /// <summary>
        /// Calculate similarity between two strings using Levenshtein distance
        /// </summary>
        private int CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 100 : 0;
            if (string.IsNullOrEmpty(target)) return 0;

            var sourceLength = source.Length;
            var targetLength = target.Length;
            var distance = new int[sourceLength + 1, targetLength + 1];

            // Initialize
            for (int i = 0; i <= sourceLength; distance[i, 0] = i++) { }
            for (int j = 0; j <= targetLength; distance[0, j] = j++) { }

            // Calculate
            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            var maxLength = Math.Max(sourceLength, targetLength);
            return maxLength == 0 ? 100 : (int)((1.0 - (double)distance[sourceLength, targetLength] / maxLength) * 100);
        }

        /// <summary>
        /// Loads Discord data silently in the background
        /// </summary>
        private async void LoadDiscordDataAsync()
        {
            try
            {
                var config = DiscordConfigHelper.ReadDiscordConfig();
                
                // Check if Discord is configured
                if (string.IsNullOrEmpty(config.BotToken) || string.IsNullOrEmpty(config.SelectedGuildId))
                {
                    Console.WriteLine("Discord not configured - skipping Discord data fetch");
                    return;
                }

                Console.WriteLine($"Loading Discord data for guild: {config.SelectedGuildName} (ID: {config.SelectedGuildId})");
                
                var discordService = new DiscordService();
                var success = await discordService.StartAsync(config.BotToken);
                
                if (!success)
                {
                    Console.WriteLine("Failed to connect to Discord - skipping Discord data fetch");
                    return;
                }

                // Fetch all members from the selected guild
                var members = await discordService.GetGuildMembersAsync(config.SelectedGuildId);
                Console.WriteLine($"Fetched {members.Count} Discord members");

                // Get members with the guild member role
                var guildMemberRoleName = "Defiance Blue Protocol"; // Same as in GuildMemberDiscordDataForm
                var membersWithRole = await discordService.GetMembersWithRoleAsync(config.SelectedGuildId, guildMemberRoleName, 80);
                Console.WriteLine($"Found {membersWithRole.Count} members with guild member role");

                // Create a set of member IDs who have the guild member role for quick lookup
                var membersWithRoleIds = new HashSet<string>(membersWithRole.Select(m => m.Id));

                // Process and store Discord member data
                _discordMembers.Clear();
                foreach (var member in members)
                {
                    // Debug: Check specific members we're having issues with
                    bool isDebugMember = member.Username.Contains("fleurentine", StringComparison.OrdinalIgnoreCase) ||
                                       member.Username.Contains("TKPStefan", StringComparison.OrdinalIgnoreCase) ||
                                       member.Username.Contains("peanuts", StringComparison.OrdinalIgnoreCase);
                    
                    if (isDebugMember)
                    {
                        Console.WriteLine($"\n=== DEBUG: Processing Discord member ===");
                        Console.WriteLine($"Username: '{member.Username}'");
                        Console.WriteLine($"DisplayName: '{member.DisplayName}'");
                        Console.WriteLine($"Id: '{member.Id}'");
                        Console.WriteLine("=== END DEBUG ===\n");
                    }
                    
                    var discordData = new DiscordMemberData
                    {
                        ServerNickname = member.DisplayName, // Server nickname or username (handled by Discord.Net)
                        Nickname = member.Username, // Global Discord username
                        HasGuildMemberRole = membersWithRoleIds.Contains(member.Id)
                    };
                    
                    _discordMembers.Add(discordData);
                }

                // Output the data in the requested format
                Console.WriteLine("\n=== Discord Member Data ===");
                Console.WriteLine("server_nickname|nickname|has_guild_member_role");
                Console.WriteLine("---------------------------");
                foreach (var member in _discordMembers)
                {
                    Console.WriteLine($"{member.ServerNickname}|{member.Nickname}|{member.HasGuildMemberRole}");
                }
                Console.WriteLine($"\nTotal Discord members: {_discordMembers.Count}");
                Console.WriteLine($"Members with guild role: {_discordMembers.Count(m => m.HasGuildMemberRole)}");
                Console.WriteLine("=== End Discord Data ===\n");

                // Refresh the table to show Discord data
                LoadGuildData();

                await discordService.StopAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Discord data: {ex.Message}");
            }
        }

        #endregion
    }

}
