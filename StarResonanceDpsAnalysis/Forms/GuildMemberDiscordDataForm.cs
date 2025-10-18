using AntdUI;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Core;
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
    /// <summary>
    /// Guild Member Discord Data Form - Displays guild member discord information
    /// </summary>
    public partial class GuildMemberDiscordDataForm : BorderlessForm
    {
        /// <summary>
        /// Constructor - Initialize the form and set up UI components
        /// </summary>
        public GuildMemberDiscordDataForm()
        {
            InitializeComponent();
            FormGui.SetDefaultGUI(this); // Apply unified default GUI styling (font, spacing, shadows, etc.)
            FormGui.SetColorMode(this, AppConfig.IsLight); // Set form color theme based on configuration (light/dark)
            
            // Set up fonts from resources
            SetDefaultFontFromResources();
            
            // Make the form draggable by the header
            SetupDraggableHeader();
            
            // Load saved Discord configuration and auto-connect if possible
            LoadDiscordConfiguration();
        }

        /// <summary>
        /// Set default fonts from application resources
        /// </summary>
        private void SetDefaultFontFromResources()
        {
            TitleText.Font = AppConfig.SaoFont;
            button_Close.Font = AppConfig.ContentFont;
            button_Stub.Font = AppConfig.ContentFont;
        }

        /// <summary>
        /// Form load event handler
        /// </summary>
        private void GuildMemberDiscordDataForm_Load(object sender, EventArgs e)
        {
            // Additional initialization can be added here
        }

        /// <summary>
        /// Close button click event handler
        /// </summary>
        private void button_Close_Click(object sender, EventArgs e)
        {
            this.Close(); // Close the form
        }

        /// <summary>
        /// Stub button click event handler - Tests Discord OAuth2 functionality
        /// </summary>
        private async void button_Stub_Click(object sender, EventArgs e)
        {
            try
            {
                // Test Discord bot setup
                await TestDiscordBot();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error testing Discord bot: {ex.Message}", "Discord Test Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Tests Discord bot functionality
        /// </summary>
        private async Task TestDiscordBot()
        {
            // Read configuration from config.ini
            var config = DiscordConfigHelper.ReadDiscordConfig();
            
            if (string.IsNullOrEmpty(config.BotToken))
            {
                MessageBox.Show(
                    "Discord bot not configured yet.\n\n" +
                    "To set up Discord bot:\n" +
                    "1. Go to https://discord.com/developers/applications\n" +
                    "2. Select your application\n" +
                    "3. Go to 'Bot' section\n" +
                    "4. Copy the bot token\n" +
                    "5. Update BotToken in config.ini\n" +
                    "6. Enable 'Server Members Intent' in Privileged Gateway Intents\n" +
                    "7. Invite the bot to your server with proper permissions\n" +
                    "8. Click the stub button again to test",
                    "Discord Bot Not Configured",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Initialize Discord service
            var discordService = new DiscordService();
            
            // Show progress message
            MessageBox.Show(
                "Starting Discord bot...\n\n" + 
                "The bot will connect to Discord and fetch guild information.\n" +
                "This may take a few seconds.",
                "Discord Bot Test",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            try
            {
                // Start the bot
                var success = await discordService.StartAsync(config.BotToken);
                
                if (!success)
                {
                    MessageBox.Show(
                        "❌ Discord bot failed to connect.\n\n" +
                        "Possible issues:\n" +
                        "1. Invalid bot token\n" +
                        "2. Bot not properly configured in Discord Developer Portal\n" +
                        "3. Missing 'Server Members Intent' in bot settings\n" +
                        "4. Network connectivity issues\n\n" +
                        "Check the console output for detailed error information.",
                        "Discord Bot Connection Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Get guilds
                var guilds = discordService.GetGuilds();
                
                if (guilds.Count == 0)
                {
                    MessageBox.Show(
                        "⚠️ No Discord guilds found.\n\n" +
                        "Make sure the bot is invited to at least one Discord server.",
                        "No Guilds Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Show guild selection dialog
                using (var guildSelectionForm = new GuildSelectionForm(guilds))
                {
                    if (guildSelectionForm.ShowDialog() == DialogResult.OK && guildSelectionForm.SelectedGuild != null)
                    {
                        var selectedGuild = guildSelectionForm.SelectedGuild;
                        
                        // Save selected guild to config
                        DiscordConfigHelper.UpdateDiscordConfig(cfg =>
                        {
                            cfg.SelectedGuildId = selectedGuild.Id;
                            cfg.SelectedGuildName = selectedGuild.Name;
                        });
                        
                        MessageBox.Show(
                            $"✅ Discord setup complete!\n\n" +
                            $"Selected guild: {selectedGuild.Name}\n" +
                            $"Guild ID: {selectedGuild.Id}\n\n" +
                            "Discord integration is now ready for use.",
                            "Discord Setup Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        // Test fetching members from selected guild
                        try
                        {
                            var members = await discordService.GetGuildMembersAsync(selectedGuild.Id);
                            MessageBox.Show(
                                $"✅ Guild member fetch test successful!\n\n" +
                                $"Found {members.Count} members in {selectedGuild.Name}\n\n" +
                                "Sample members:\n" +
                                string.Join("\n", members.Take(5).Select(m => $"• {m.DisplayName}")) +
                                (members.Count > 5 ? $"\n... and {members.Count - 5} more" : ""),
                                "Guild Member Test Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            // Test role fetching
                            try
                            {
                                var roles = await discordService.GetGuildRolesAsync(selectedGuild.Id);
                                var roleList = string.Join("\n", roles.Take(10).Select(r => $"• {r.Name} (Position: {r.Position})"));
                                
                                MessageBox.Show(
                                    $"✅ Guild roles fetch test successful!\n\n" +
                                    $"Found {roles.Count} roles in {selectedGuild.Name}\n\n" +
                                    "Sample roles:\n" +
                                    roleList +
                                    (roles.Count > 10 ? $"\n... and {roles.Count - 10} more" : ""),
                                    "Guild Roles Test Success",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);

                                // Test role-based member filtering
                                var roleName = "Defiance Blue Protocol"; // You can change this to test different roles
                                var membersWithRole = await discordService.GetMembersWithRoleAsync(selectedGuild.Id, roleName, 80);
                                
                                var memberList = string.Join("\n", membersWithRole.Take(10).Select(m => $"• {m.DisplayName} (Roles: {string.Join(", ", m.Roles.Select(r => r.Name))})"));
                                
                                MessageBox.Show(
                                    $"✅ Role-based member filtering test successful!\n\n" +
                                    $"Found {membersWithRole.Count} members with role matching '{roleName}'\n\n" +
                                    "Sample members with matching roles:\n" +
                                    memberList +
                                    (membersWithRole.Count > 10 ? $"\n... and {membersWithRole.Count - 10} more" : ""),
                                    "Role Filtering Test Success",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            catch (Exception roleEx)
                            {
                                MessageBox.Show(
                                    $"⚠️ Role testing failed: {roleEx.Message}\n\n" +
                                    "This might be due to missing permissions or the role not existing.",
                                    "Role Test Warning",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"⚠️ Guild member fetch failed: {ex.Message}\n\n" +
                                "Make sure the bot has the 'Server Members Intent' enabled and proper permissions.",
                                "Guild Member Test Warning",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "Discord setup cancelled.\n\n" +
                            "You can run the setup again anytime by clicking the stub button.",
                            "Setup Cancelled",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Error during Discord bot test: {ex.Message}\n\n" +
                    "Please check your bot token and configuration.",
                    "Discord Bot Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // Stop the bot
                await discordService.StopAsync();
            }
        }

        /// <summary>
        /// Shows a simple input dialog for testing purposes using AntdUI
        /// </summary>
        private string ShowInputDialog(string text, string caption)
        {
            var inputForm = new BorderlessForm()
            {
                Width = 400,
                Height = 200,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                Text = caption
            };

            var panel = new AntdUI.Panel()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            var textLabel = new AntdUI.Label()
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 60,
                Font = AppConfig.ContentFont
            };

            var textBox = new AntdUI.Input()
            {
                Dock = DockStyle.Top,
                Height = 40,
                Font = AppConfig.ContentFont,
                PlaceholderText = "Enter authorization code here..."
            };

            var buttonPanel = new AntdUI.Panel()
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(0, 10, 0, 0)
            };

            var okButton = new AntdUI.Button()
            {
                Text = "OK",
                Size = new Size(80, 30),
                Location = new Point(220, 10),
                Font = AppConfig.ContentFont
            };

            var cancelButton = new AntdUI.Button()
            {
                Text = "Cancel",
                Size = new Size(80, 30),
                Location = new Point(310, 10),
                Font = AppConfig.ContentFont
            };

            string result = string.Empty;
            bool dialogResult = false;

            okButton.Click += (sender, e) => 
            { 
                result = textBox.Text;
                dialogResult = true;
                inputForm.Close(); 
            };

            cancelButton.Click += (sender, e) => 
            { 
                dialogResult = false;
                inputForm.Close(); 
            };

            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(cancelButton);
            panel.Controls.Add(buttonPanel);
            panel.Controls.Add(textBox);
            panel.Controls.Add(textLabel);
            inputForm.Controls.Add(panel);

            // Apply AntdUI styling
            FormGui.SetDefaultGUI(inputForm);
            FormGui.SetColorMode(inputForm, AppConfig.IsLight);

            inputForm.ShowDialog();
            return dialogResult ? result : string.Empty;
        }

        /// <summary>
        /// Form closing event handler
        /// </summary>
        private void GuildMemberDiscordDataForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Additional cleanup can be added here
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
        /// Loads saved Discord configuration and auto-connects if possible
        /// </summary>
        private async void LoadDiscordConfiguration()
        {
            try
            {
                var config = DiscordConfigHelper.ReadDiscordConfig();
                
                // Check if we have a saved guild and bot token
                if (!string.IsNullOrEmpty(config.SelectedGuildId) && 
                    !string.IsNullOrEmpty(config.BotToken) && 
                    !string.IsNullOrEmpty(config.SelectedGuildName))
                {
                    // Show a status message
                    MessageBox.Show(
                        $"🔄 Auto-connecting to Discord...\n\n" +
                        $"Saved guild: {config.SelectedGuildName}\n" +
                        $"Guild ID: {config.SelectedGuildId}\n\n" +
                        "The bot will connect automatically on startup.",
                        "Discord Auto-Connect",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Auto-connect to Discord
                    await AutoConnectToDiscord(config);
                }
                else
                {
                    // No saved configuration, show setup instructions
                    MessageBox.Show(
                        "🔧 Discord not configured yet.\n\n" +
                        "Click the 'Stub' button to set up Discord integration:\n" +
                        "1. Configure bot token\n" +
                        "2. Select a Discord server\n" +
                        "3. Test member fetching\n\n" +
                        "Your settings will be saved for next time!",
                        "Discord Setup Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"⚠️ Error loading Discord configuration: {ex.Message}\n\n" +
                    "You can still set up Discord manually using the 'Stub' button.",
                    "Configuration Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Auto-connects to Discord using saved configuration
        /// </summary>
        private async Task AutoConnectToDiscord(DiscordConfigHelper.DiscordConfig config)
        {
            try
            {
                var discordService = new DiscordService();
                var success = await discordService.StartAsync(config.BotToken);
                
                if (success)
                {
                    // Test the saved guild
                    var guilds = discordService.GetGuilds();
                    var savedGuild = guilds.FirstOrDefault(g => g.Id == config.SelectedGuildId);
                    
                    if (savedGuild != null)
                    {
                        MessageBox.Show(
                            $"✅ Discord auto-connect successful!\n\n" +
                            $"Connected to: {savedGuild.Name}\n" +
                            $"Guild ID: {savedGuild.Id}\n\n" +
                            "Discord integration is ready to use!",
                            "Discord Auto-Connect Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"⚠️ Saved guild '{config.SelectedGuildName}' not found.\n\n" +
                            "The bot may have been removed from this server.\n" +
                            "Click 'Stub' to select a different server.",
                            "Guild Not Found",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "❌ Discord auto-connect failed.\n\n" +
                        "Click 'Stub' to reconfigure Discord integration.",
                        "Auto-Connect Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                
                // Stop the bot after testing
                await discordService.StopAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Error during auto-connect: {ex.Message}\n\n" +
                    "Click 'Stub' to reconfigure Discord integration.",
                    "Auto-Connect Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #endregion
    }
}
