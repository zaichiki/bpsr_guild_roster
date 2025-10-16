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
    /// Guild Roster Form - Displays guild member information in a table format
    /// </summary>
    public partial class GuildRosterForm : BorderlessForm
    {
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
            
            // Subscribe to guild data updates
            GuildRosterExporter.GuildDataUpdated += OnGuildDataUpdated;
            
            // Initialize the table view
            InitializeTableView();
            
            // Make the form draggable by the header
            SetupDraggableHeader();
        }

        /// <summary>
        /// Set default fonts from application resources
        /// </summary>
        private void SetDefaultFontFromResources()
        {
            TitleText.Font = AppConfig.SaoFont;
            table_GuildRoster.Font = AppConfig.ContentFont;
            button_Close.Font = AppConfig.ContentFont;
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

        #endregion
    }

}
