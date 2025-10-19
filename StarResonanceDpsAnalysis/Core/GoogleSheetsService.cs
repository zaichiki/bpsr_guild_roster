using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Service for interacting with Google Sheets API
    /// Handles authentication and data export to predefined Google Sheets
    /// </summary>
    public class GoogleSheetsService
    {
        private SheetsService? _sheetsService;
        private readonly GoogleSheetsConfig _config;
        private readonly string _documentId;
        private readonly string _sheetName;

        /// <summary>
        /// Configuration for Google Sheets service
        /// </summary>
        public class GoogleSheetsConfig
        {
            public string ClientId { get; set; } = "";
            public string ClientSecret { get; set; } = "";
            public string DocumentId { get; set; } = "";
            public string SheetName { get; set; } = "Guild Roster";
            public string[] Scopes { get; set; } = { SheetsService.Scope.Spreadsheets };
        }

        /// <summary>
        /// Initialize Google Sheets service with configuration
        /// </summary>
        /// <param name="config">Configuration settings</param>
        public GoogleSheetsService(GoogleSheetsConfig config)
        {
            _config = config;
            _documentId = config.DocumentId;
            _sheetName = config.SheetName;
        }

        /// <summary>
        /// Authenticate with Google Sheets API using OAuth 2.0
        /// </summary>
        /// <returns>True if authentication successful, false otherwise</returns>
        public async Task<bool> AuthenticateAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_config.ClientId) || string.IsNullOrEmpty(_config.ClientSecret))
                {
                    MessageBox.Show("Google OAuth credentials are not configured.\n\nPlease set the ClientId and ClientSecret in the configuration.", 
                        "Google Sheets Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (string.IsNullOrEmpty(_documentId))
                {
                    MessageBox.Show("Google Sheets Document ID is not configured.\n\nPlease set the Document ID in the configuration.", 
                        "Google Sheets Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Create OAuth 2.0 flow
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _config.ClientId,
                        ClientSecret = _config.ClientSecret
                    },
                    Scopes = _config.Scopes,
                    DataStore = new FileDataStore("StarResonanceDpsAnalysis", true)
                });

                // Try to get existing credentials
                var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets
                    {
                        ClientId = _config.ClientId,
                        ClientSecret = _config.ClientSecret
                    },
                    _config.Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore("StarResonanceDpsAnalysis", true));

                // Create the Sheets service
                _sheetsService = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Star Resonance DPS Analysis"
                });

                // Test the connection by trying to get spreadsheet info
                var spreadsheet = await _sheetsService.Spreadsheets.Get(_documentId).ExecuteAsync();
                Console.WriteLine($"Successfully connected to Google Sheet: {spreadsheet.Properties.Title}");

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to authenticate with Google Sheets API:\n{ex.Message}\n\nThis may be due to:\n" +
                    "1. Invalid OAuth credentials\n" +
                    "2. Missing Google Sheets API access\n" +
                    "3. Network connectivity issues\n" +
                    "4. The Google Sheet is not shared with your account", 
                    "Google Sheets Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Google Sheets authentication error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Export guild roster data to Google Sheets
        /// </summary>
        /// <param name="guildData">Guild member data to export</param>
        /// <returns>True if export successful, false otherwise</returns>
        public async Task<bool> ExportGuildRosterAsync(Dictionary<int, JoinedGuildMemberData> guildData)
        {
            if (_sheetsService == null)
            {
                MessageBox.Show("Not authenticated with Google Sheets API. Please authenticate first.", 
                    "Google Sheets Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                // Prepare the data for export
                var values = PrepareDataForExport(guildData);

                // Clear existing data in the sheet
                await ClearSheetAsync();

                // Update the sheet with new data
                var range = $"{_sheetName}!A1";
                var valueRange = new ValueRange
                {
                    Values = values
                };

                var updateRequest = _sheetsService.Spreadsheets.Values.Update(valueRange, _documentId, range);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

                var updateResponse = await updateRequest.ExecuteAsync();
                Console.WriteLine($"Updated {updateResponse.UpdatedCells} cells in Google Sheet");

                // Format the header row
                await FormatHeaderRowAsync();

                MessageBox.Show($"Guild roster exported successfully to Google Sheets!\n\nUpdated {updateResponse.UpdatedCells} cells in sheet '{_sheetName}'", 
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export data to Google Sheets:\n{ex.Message}", 
                    "Google Sheets Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Google Sheets export error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Prepare guild data for export to Google Sheets
        /// </summary>
        private List<IList<object>> PrepareDataForExport(Dictionary<int, JoinedGuildMemberData> guildData)
        {
            var values = new List<IList<object>>();

            // Add header row
            var headerRow = new List<object>
            {
                "No.",
                "Player Name",
                "Level",
                "Class",
                "Gear Score",
                "Last Login",
                "Last Online",
                "Join Date",
                "Role",
                "Discord Member",
                "Discord Name",
                "Has Guild Role"
            };
            values.Add(headerRow);

            // Add data rows
            int rowNumber = 1;
            foreach (var kvp in guildData.OrderBy(x => x.Key))
            {
                var data = kvp.Value;
                var row = new List<object>
                {
                    rowNumber++,
                    data.PlayerName,
                    data.CharacterLevel,
                    data.ClassDisplay,
                    data.GearScore,
                    data.LastLoginDisplay,
                    FormatLastOnlineTime(data.LastLoginTS),
                    data.JoinDateDisplay,
                    GetRoleDisplayName(data.RoleId),
                    data.DiscordIsMember,
                    data.DiscordNameData,
                    data.DiscordHasRole
                };
                values.Add(row);
            }

            return values;
        }

        /// <summary>
        /// Clear all data in the target sheet
        /// </summary>
        private async Task ClearSheetAsync()
        {
            try
            {
                var range = $"{_sheetName}!A:L"; // Clear columns A through L (12 columns)
                var clearRequest = _sheetsService!.Spreadsheets.Values.Clear(new ClearValuesRequest(), _documentId, range);
                await clearRequest.ExecuteAsync();
                Console.WriteLine($"Cleared data in sheet '{_sheetName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to clear sheet data: {ex.Message}");
                // Don't throw here as clearing is not critical
            }
        }

        /// <summary>
        /// Format the header row with bold text and background color
        /// </summary>
        private async Task FormatHeaderRowAsync()
        {
            try
            {
                var requests = new List<Request>
                {
                    // Bold header text
                    new Request
                    {
                        RepeatCell = new RepeatCellRequest
                        {
                            Range = new GridRange
                            {
                                SheetId = await GetSheetIdAsync(),
                                StartRowIndex = 0,
                                EndRowIndex = 1,
                                StartColumnIndex = 0,
                                EndColumnIndex = 12 // Number of columns
                            },
                            Cell = new CellData
                            {
                                UserEnteredFormat = new CellFormat
                                {
                                    TextFormat = new TextFormat
                                    {
                                        Bold = true
                                    },
                                    BackgroundColor = new Google.Apis.Sheets.v4.Data.Color
                                    {
                                        Red = 0.9f,
                                        Green = 0.9f,
                                        Blue = 0.9f
                                    }
                                }
                            },
                            Fields = "userEnteredFormat(textFormat,backgroundColor)"
                        }
                    }
                };

                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = requests
                };

                await _sheetsService!.Spreadsheets.BatchUpdate(batchUpdateRequest, _documentId).ExecuteAsync();
                Console.WriteLine("Formatted header row in Google Sheet");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to format header row: {ex.Message}");
                // Don't throw here as formatting is not critical
            }
        }

        /// <summary>
        /// Get the sheet ID for the target sheet name
        /// </summary>
        private async Task<int> GetSheetIdAsync()
        {
            var spreadsheet = await _sheetsService!.Spreadsheets.Get(_documentId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == _sheetName);
            
            if (sheet == null)
            {
                throw new InvalidOperationException($"Sheet '{_sheetName}' not found in the document");
            }

            return sheet.Properties.SheetId ?? 0;
        }

        /// <summary>
        /// Convert RoleId to readable role name
        /// </summary>
        private static string GetRoleDisplayName(ulong roleId)
        {
            return roleId switch
            {
                1 => "master",
                2 => "vice master",
                3 => "administrator",
                4 => "member",
                _ => $"role_{roleId}"
            };
        }

        /// <summary>
        /// Format last online time as "X days ago" or "Never"
        /// </summary>
        private static string FormatLastOnlineTime(ulong lastLoginTS)
        {
            if (lastLoginTS <= 0)
                return "Never";

            try
            {
                // Convert Unix timestamp to DateTime
                var lastLogin = DateTimeOffset.FromUnixTimeSeconds((long)lastLoginTS).DateTime;
                var now = DateTime.Now;
                var timeDiff = now - lastLogin;

                if (timeDiff.TotalDays >= 1)
                {
                    var days = (int)timeDiff.TotalDays;
                    return $"{days} day{(days == 1 ? "" : "s")} ago";
                }
                else if (timeDiff.TotalHours >= 1)
                {
                    var hours = (int)timeDiff.TotalHours;
                    return $"{hours} hour{(hours == 1 ? "" : "s")} ago";
                }
                else if (timeDiff.TotalMinutes >= 1)
                {
                    var minutes = (int)timeDiff.TotalMinutes;
                    return $"{minutes} minute{(minutes == 1 ? "" : "s")} ago";
                }
                else
                {
                    return "just now";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error formatting last online time for timestamp {lastLoginTS}: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Check if the service is properly configured
        /// </summary>
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_documentId) && 
                   !string.IsNullOrEmpty(_config.ClientId) && 
                   !string.IsNullOrEmpty(_config.ClientSecret);
        }

        /// <summary>
        /// Get configuration status message
        /// </summary>
        public string GetConfigurationStatus()
        {
            if (string.IsNullOrEmpty(_documentId))
                return "Document ID not configured";
            
            if (string.IsNullOrEmpty(_config.ClientId))
                return "Client ID not configured";
            
            if (string.IsNullOrEmpty(_config.ClientSecret))
                return "Client Secret not configured";
            
            return "Properly configured";
        }
    }
}
