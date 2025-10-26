using System;
using System.IO;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Helper class for reading GuildRosterService configuration from private_config.ini
    /// </summary>
    public static class GuildRosterServiceConfigHelper
    {
        /// <summary>
        /// Read GuildRosterService configuration from private_config.ini
        /// </summary>
        /// <returns>GuildRosterService configuration</returns>
        public static GuildRosterService.GuildRosterServiceConfig ReadGuildRosterServiceConfig()
        {
            return ReadGuildRosterServiceConfig("GuildRosterService");
        }

        /// <summary>
        /// Read GuildRosterService configuration from private_config.ini for a specific section
        /// </summary>
        /// <param name="sectionName">Name of the configuration section to read</param>
        /// <returns>GuildRosterService configuration</returns>
        public static GuildRosterService.GuildRosterServiceConfig ReadGuildRosterServiceConfig(string sectionName)
        {
            var config = new GuildRosterService.GuildRosterServiceConfig();
            
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "private_config.ini");
                
                if (!File.Exists(configPath))
                {
                    Console.WriteLine("private_config.ini not found - using default GuildRosterService configuration");
                    return config;
                }

                var lines = File.ReadAllLines(configPath);
                bool inGuildRosterServiceSection = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Check for section header
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        inGuildRosterServiceSection = trimmedLine.Equals($"[{sectionName}]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    // Skip comments and empty lines
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith(";"))
                        continue;

                    // Parse configuration values if we're in the GuildRosterService section
                    if (inGuildRosterServiceSection && trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();

                            switch (key.ToLowerInvariant())
                            {
                                case "url":
                                    config.Url = value;
                                    break;
                                case "apikey":
                                    config.ApiKey = value;
                                    break;
                            }
                        }
                    }
                }

                Console.WriteLine($"{sectionName} configuration loaded - URL: '{config.Url}', API Key: {(string.IsNullOrEmpty(config.ApiKey) ? "Not set" : "Set")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {sectionName} configuration: {ex.Message}");
            }

            return config;
        }
    }
}


