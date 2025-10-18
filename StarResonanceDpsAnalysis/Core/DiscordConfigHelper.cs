using System;
using System.IO;
using System.Collections.Generic;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Helper class for reading and writing Discord configuration
    /// </summary>
    public static class DiscordConfigHelper
    {
        private const string CONFIG_FILE = "config.ini";
        private const string SECRETS_FILE = "discord_secrets.ini";
        private const string DISCORD_SECTION = "[Discord]";

        /// <summary>
        /// Discord configuration data structure
        /// </summary>
        public class DiscordConfig
        {
            public string BotToken { get; set; } = string.Empty;
            public string SelectedGuildId { get; set; } = string.Empty;
            public string SelectedGuildName { get; set; } = string.Empty;
            public int MatchThreshold { get; set; } = 80;
        }

        /// <summary>
        /// Reads Discord configuration from config files
        /// </summary>
        /// <returns>Discord configuration object</returns>
        public static DiscordConfig ReadDiscordConfig()
        {
            var config = new DiscordConfig();

            try
            {
                // Read bot token from secrets file
                ReadSecretsFile(config);
                
                // Read other settings from main config file
                ReadMainConfigFile(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading Discord config: {ex.Message}");
            }

            return config;
        }

        /// <summary>
        /// Reads bot token from secrets file
        /// </summary>
        private static void ReadSecretsFile(DiscordConfig config)
        {
            if (!File.Exists(SECRETS_FILE))
            {
                return;
            }

            var lines = File.ReadAllLines(SECRETS_FILE);
            bool inDiscordSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine == DISCORD_SECTION)
                {
                    inDiscordSection = true;
                    continue;
                }

                if (inDiscordSection)
                {
                    if (trimmedLine.StartsWith("[") && trimmedLine != DISCORD_SECTION)
                    {
                        break;
                    }

                    if (trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2 && parts[0].Trim() == "BotToken")
                        {
                            config.BotToken = parts[1].Trim();
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads other Discord settings from main config file
        /// </summary>
        private static void ReadMainConfigFile(DiscordConfig config)
        {
            if (!File.Exists(CONFIG_FILE))
            {
                return;
            }

            var lines = File.ReadAllLines(CONFIG_FILE);
            bool inDiscordSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine == DISCORD_SECTION)
                {
                    inDiscordSection = true;
                    continue;
                }

                if (inDiscordSection)
                {
                    if (trimmedLine.StartsWith("[") && trimmedLine != DISCORD_SECTION)
                    {
                        break;
                    }

                    if (trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();

                            switch (key)
                            {
                                case "SelectedGuildId":
                                    config.SelectedGuildId = value;
                                    break;
                                case "SelectedGuildName":
                                    config.SelectedGuildName = value;
                                    break;
                                case "MatchThreshold":
                                    if (int.TryParse(value, out int threshold))
                                    {
                                        config.MatchThreshold = threshold;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates Discord configuration in config.ini
        /// </summary>
        /// <param name="updateAction">Action to update the config object</param>
        public static void UpdateDiscordConfig(Action<DiscordConfig> updateAction)
        {
            try
            {
                var config = ReadDiscordConfig();
                updateAction(config);
                WriteDiscordConfig(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Discord config: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes Discord configuration to config.ini using Win32 API (same as AppConfig)
        /// </summary>
        /// <param name="config">Discord configuration to write</param>
        private static void WriteDiscordConfig(DiscordConfig config)
        {
            try
            {
                // Use absolute path for Win32 API
                string absolutePath = Path.GetFullPath(CONFIG_FILE);
                
                // Use Win32 API to write individual keys (same as AppConfig)
                bool result1 = WritePrivateProfileString("Discord", "SelectedGuildId", config.SelectedGuildId, absolutePath);
                bool result2 = WritePrivateProfileString("Discord", "SelectedGuildName", config.SelectedGuildName, absolutePath);
                bool result3 = WritePrivateProfileString("Discord", "MatchThreshold", config.MatchThreshold.ToString(), absolutePath);
                
                // If Win32 API failed, try direct file writing as fallback
                if (!result1 || !result2 || !result3)
                {
                    WriteDiscordConfigDirectly(config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing Discord config: {ex.Message}");
            }
        }

        /// <summary>
        /// Win32 API for writing INI files (same as AppConfig)
        /// </summary>
        [System.Runtime.InteropServices.DllImport("kernel32", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

        /// <summary>
        /// Fallback method to write Discord config directly to file
        /// </summary>
        private static void WriteDiscordConfigDirectly(DiscordConfig config)
        {
            try
            {
                var lines = new List<string>();
                
                if (File.Exists(CONFIG_FILE))
                {
                    var existingLines = File.ReadAllLines(CONFIG_FILE);
                    bool inDiscordSection = false;
                    bool discordSectionFound = false;

                    foreach (var line in existingLines)
                    {
                        var trimmedLine = line.Trim();

                        if (trimmedLine == DISCORD_SECTION)
                        {
                            inDiscordSection = true;
                            discordSectionFound = true;
                            lines.Add(line);
                            // Add our Discord config right after the section header
                            lines.Add($"SelectedGuildId={config.SelectedGuildId}");
                            lines.Add($"SelectedGuildName={config.SelectedGuildName}");
                            lines.Add($"MatchThreshold={config.MatchThreshold}");
                            continue;
                        }

                        if (inDiscordSection)
                        {
                            if (trimmedLine.StartsWith("[") && trimmedLine != DISCORD_SECTION)
                            {
                                // Hit another section, stop skipping
                                inDiscordSection = false;
                            }
                            else
                            {
                                // Skip existing Discord config lines
                                continue;
                            }
                        }

                        lines.Add(line);
                    }

                    // If Discord section wasn't found, add it at the end
                    if (!discordSectionFound)
                    {
                        lines.Add("");
                        lines.Add(DISCORD_SECTION);
                        lines.Add($"SelectedGuildId={config.SelectedGuildId}");
                        lines.Add($"SelectedGuildName={config.SelectedGuildName}");
                        lines.Add($"MatchThreshold={config.MatchThreshold}");
                    }
                }
                else
                {
                    // Create new file
                    lines.Add(DISCORD_SECTION);
                    lines.Add($"SelectedGuildId={config.SelectedGuildId}");
                    lines.Add($"SelectedGuildName={config.SelectedGuildName}");
                    lines.Add($"MatchThreshold={config.MatchThreshold}");
                }

                File.WriteAllLines(CONFIG_FILE, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in direct file write: {ex.Message}");
            }
        }

    }
}