using System;
using System.IO;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Helper class for reading Google Sheets configuration from config.ini
    /// </summary>
    public static class GoogleSheetsConfigHelper
    {
        /// <summary>
        /// Configuration structure for Google Sheets
        /// </summary>
        public class GoogleSheetsConfig
        {
            public string ClientId { get; set; } = "";
            public string ClientSecret { get; set; } = "";
            public string DocumentId { get; set; } = "";
            public string SheetName { get; set; } = "Guild Roster";
        }

        /// <summary>
        /// Read Google Sheets configuration from config.ini
        /// </summary>
        /// <returns>Google Sheets configuration</returns>
        public static GoogleSheetsConfig ReadGoogleSheetsConfig()
        {
            var config = new GoogleSheetsConfig();
            
            try
            {
                if (!File.Exists("config.ini"))
                {
                    Console.WriteLine("config.ini not found, using default Google Sheets configuration");
                    return config;
                }

                var lines = File.ReadAllLines("config.ini");
                bool inGoogleSheetsSection = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Check for section start
                    if (trimmedLine.Equals("[GoogleSheets]", StringComparison.OrdinalIgnoreCase))
                    {
                        inGoogleSheetsSection = true;
                        continue;
                    }
                    
                    // Check for section end
                    if (trimmedLine.StartsWith("[") && !trimmedLine.Equals("[GoogleSheets]", StringComparison.OrdinalIgnoreCase))
                    {
                        inGoogleSheetsSection = false;
                        continue;
                    }
                    
                    // Parse configuration values
                    if (inGoogleSheetsSection && trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            
                            switch (key.ToLower())
                            {
                                case "clientid":
                                    config.ClientId = value;
                                    break;
                                case "clientsecret":
                                    config.ClientSecret = value;
                                    break;
                                case "documentid":
                                    config.DocumentId = value;
                                    break;
                                case "sheetname":
                                    config.SheetName = value;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading Google Sheets configuration: {ex.Message}");
            }

            return config;
        }

        /// <summary>
        /// Save Google Sheets configuration to config.ini
        /// </summary>
        /// <param name="config">Configuration to save</param>
        public static void SaveGoogleSheetsConfig(GoogleSheetsConfig config)
        {
            try
            {
                var lines = File.Exists("config.ini") ? File.ReadAllLines("config.ini") : new string[0];
                var newLines = new List<string>();
                bool inGoogleSheetsSection = false;
                bool googleSheetsSectionFound = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Check for GoogleSheets section start
                    if (trimmedLine.Equals("[GoogleSheets]", StringComparison.OrdinalIgnoreCase))
                    {
                        inGoogleSheetsSection = true;
                        googleSheetsSectionFound = true;
                        newLines.Add(line);
                        newLines.Add($"ClientId={config.ClientId}");
                        newLines.Add($"ClientSecret={config.ClientSecret}");
                        newLines.Add($"DocumentId={config.DocumentId}");
                        newLines.Add($"SheetName={config.SheetName}");
                        continue;
                    }
                    
                    // Check for section end
                    if (inGoogleSheetsSection && trimmedLine.StartsWith("[") && !trimmedLine.Equals("[GoogleSheets]", StringComparison.OrdinalIgnoreCase))
                    {
                        inGoogleSheetsSection = false;
                        newLines.Add(line);
                        continue;
                    }
                    
                    // Skip lines within GoogleSheets section (we're replacing them)
                    if (inGoogleSheetsSection)
                        continue;
                    
                    newLines.Add(line);
                }

                // If GoogleSheets section wasn't found, add it at the end
                if (!googleSheetsSectionFound)
                {
                    newLines.Add("");
                    newLines.Add("[GoogleSheets]");
                    newLines.Add($"ClientId={config.ClientId}");
                    newLines.Add($"ClientSecret={config.ClientSecret}");
                    newLines.Add($"DocumentId={config.DocumentId}");
                    newLines.Add($"SheetName={config.SheetName}");
                }

                File.WriteAllLines("config.ini", newLines);
                Console.WriteLine("Google Sheets configuration saved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving Google Sheets configuration: {ex.Message}");
            }
        }
    }
}
