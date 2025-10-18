using AntdUI;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.Forms
{
    /// <summary>
    /// Guild Selection Form - Allows user to select a Discord guild for Discord integration
    /// </summary>
    public partial class GuildSelectionForm : BorderlessForm
    {
        private List<Core.DiscordGuild> _guilds;
        private Core.DiscordGuild? _selectedGuild;

        public Core.DiscordGuild? SelectedGuild => _selectedGuild;

        /// <summary>
        /// Constructor - Initialize the form with guild list
        /// </summary>
        /// <param name="guilds">List of Discord guilds to choose from</param>
        public GuildSelectionForm(List<Core.DiscordGuild> guilds)
        {
            _guilds = guilds ?? throw new ArgumentNullException(nameof(guilds));
            InitializeComponent();
            FormGui.SetDefaultGUI(this);
            FormGui.SetColorMode(this, AppConfig.IsLight);
            
            // Set up fonts from resources
            SetDefaultFontFromResources();
            
            // Make the form draggable by the header
            SetupDraggableHeader();
            
            // Populate guild list
            PopulateGuildList();
        }

        /// <summary>
        /// Set default fonts from application resources
        /// </summary>
        private void SetDefaultFontFromResources()
        {
            TitleText.Font = AppConfig.SaoFont;
            button_Select.Font = AppConfig.ContentFont;
            button_Cancel.Font = AppConfig.ContentFont;
            comboBox_Guilds.Font = AppConfig.ContentFont;
            label_Instructions.Font = AppConfig.ContentFont;
        }

        /// <summary>
        /// Populate the guild dropdown with available guilds
        /// </summary>
        private void PopulateGuildList()
        {
            comboBox_Guilds.Items.Clear();
            
            foreach (var guild in _guilds)
            {
                var displayText = guild.Name;
                if (guild.Owner)
                {
                    displayText += " (Owner)";
                }
                
                comboBox_Guilds.Items.Add(new GuildListItem
                {
                    Guild = guild,
                    DisplayText = displayText
                });
            }

            // Select first item if available
            if (comboBox_Guilds.Items.Count > 0)
            {
                comboBox_Guilds.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Guild list item for display
        /// </summary>
        private class GuildListItem
        {
            public Core.DiscordGuild Guild { get; set; }
            public string DisplayText { get; set; } = string.Empty;

            public override string ToString() => DisplayText;
        }

        /// <summary>
        /// Form load event handler
        /// </summary>
        private void GuildSelectionForm_Load(object sender, EventArgs e)
        {
            // Additional initialization can be added here
        }

        /// <summary>
        /// Select button click event handler
        /// </summary>
        private void button_Select_Click(object sender, EventArgs e)
        {
            if (comboBox_Guilds.SelectedIndex >= 0 && comboBox_Guilds.SelectedIndex < _guilds.Count)
            {
                _selectedGuild = _guilds[comboBox_Guilds.SelectedIndex];
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please select a guild from the dropdown.", "No Guild Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Cancel button click event handler
        /// </summary>
        private void button_Cancel_Click(object sender, EventArgs e)
        {
            _selectedGuild = null;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// Guild dropdown selection changed event handler
        /// </summary>
        private void comboBox_Guilds_SelectedIndexChanged(object sender, EventArgs e)
        {
            button_Select.Enabled = comboBox_Guilds.SelectedIndex >= 0;
        }

        /// <summary>
        /// Form closing event handler
        /// </summary>
        private void GuildSelectionForm_FormClosing(object sender, FormClosingEventArgs e)
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

        #endregion
    }
}
