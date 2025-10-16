using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Exports combined guild member and activity data to TSV format
    /// </summary>
    public class GuildRosterExporter
    {
        /// <summary>
        /// Event fired when new guild data is available
        /// </summary>
        public static event EventHandler? GuildDataUpdated;

        /// <summary>
        /// Current joined guild data available for display and export
        /// </summary>
        public static Dictionary<int, JoinedGuildMemberData> CurrentGuildData { get; private set; } = new Dictionary<int, JoinedGuildMemberData>();

        /// <summary>
        /// Joins member data and activity data into a single dictionary and stores it
        /// </summary>
        /// <param name="memberData">Dictionary of guild member data</param>
        /// <param name="activityData">Dictionary of guild member activity data</param>
        public static void UpdateGuildData(
            Dictionary<int, GuildMemberData> memberData, 
            Dictionary<int, GuildMemberActivityData> activityData)
        {
            try
            {
                // Clear existing data
                CurrentGuildData.Clear();

                // Use only IDs from activityData dictionary (as per original logic)
                var allUserIds = memberData.Keys.OrderBy(id => id);

                foreach (var userId in allUserIds)
                {
                    var member = memberData.ContainsKey(userId) ? memberData[userId] : null;
                    var activity = activityData.ContainsKey(userId) ? activityData[userId] : null;

                    // Create joined data object
                    var joinedData = new JoinedGuildMemberData
                    {
                        UserId = userId,
                        UserIdSecondary = member?.UserIdSecondary ?? 0,
                        PlayerName = member?.PlayerName ?? "",
                        CharacterLevel = member?.CharacterLevel ?? 0,
                        GuildName = member?.GuildName ?? "",
                        ClassId = member?.ClassId ?? 0,
                        ClassVariant = member?.ClassVariant ?? 0,
                        GearScore = member?.GearScore ?? 0,
                        RoleId = activity?.RoleId ?? 0,
                        Activity1 = activity?.Activity1 ?? 0,
                        Activity2 = activity?.Activity2 ?? 0,
                        LastLoginTS = member?.LastLoginTS ?? 0,
                        JoinTS = activity?.JoinTS ?? 0
                    };

                    CurrentGuildData[userId] = joinedData;
                }

                Console.WriteLine($"Guild data updated: {CurrentGuildData.Count} members");
                
                // Fire event to notify subscribers
                GuildDataUpdated?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating guild data: {ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// Exports current joined guild data to TSV file
        /// </summary>
        /// <param name="outputDirectory">Directory to save the TSV file (optional, defaults to current directory)</param>
        /// <returns>Path to the created TSV file</returns>
        public static string ExportCurrentDataToTsv(string outputDirectory = "")
        {
            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = Directory.GetCurrentDirectory();
            }

            // Generate filename with current datetime
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"guild_roster_{timestamp}.tsv";
            string filePath = Path.Combine(outputDirectory, filename);

            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write TSV header
                    writer.WriteLine("UserId\tUserIdSecondary\tPlayerName\tCharacterLevel\tGuildName\tClassId\tClassVariant\tGearScore\tRoleId\tActivity1\tActivity2\tLastLogin\tParsedLastLogin\tJoinTS\tParsedJoinTS");

                    foreach (var kvp in CurrentGuildData.OrderBy(x => x.Key))
                    {
                        var data = kvp.Value;
                        
                        // Create combined data row
                        var row = new List<string>
                        {
                            data.UserId.ToString(),
                            data.UserIdSecondary.ToString(),
                            EscapeTsvField(data.PlayerName),
                            data.CharacterLevel.ToString(),
                            EscapeTsvField(data.GuildName),
                            data.ClassId.ToString(),
                            data.ClassVariant.ToString(),
                            data.GearScore.ToString(),
                            data.RoleId.ToString(),
                            data.Activity1.ToString(),
                            data.Activity2.ToString(),
                            data.LastLoginTS.ToString(),
                            data.ParsedLastLoginTS?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                            data.JoinTS.ToString(),
                            data.ParsedJoinTS?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                        };

                        writer.WriteLine(string.Join("\t", row));
                    }
                }

                Console.WriteLine($"Guild roster exported successfully to: {filePath}");
                Console.WriteLine($"Total records: {CurrentGuildData.Count}");
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting guild roster: {ex.Message}");
                throw;
            }
        }

        public static void OnRosterProtobufProcessed(
            Dictionary<int, GuildMemberData> memberData, 
            Dictionary<int, GuildMemberActivityData> activityData,
            string outputDirectory = "")
        {
            // Update the current data first
            UpdateGuildData(memberData, activityData);            
        }

        /// <summary>
        /// Escapes special characters in TSV fields
        /// </summary>
        /// <param name="field">Field value to escape</param>
        /// <returns>Escaped field value</returns>
        private static string EscapeTsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // Replace tabs with spaces and newlines with spaces
            return field.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }
    }

    /// <summary>
    /// Combined guild member data with both member and activity information
    /// </summary>
    public class JoinedGuildMemberData
    {
        // Member data fields
        public int UserId { get; set; }
        public int UserIdSecondary { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int CharacterLevel { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public int ClassVariant { get; set; }
        public int GearScore { get; set; }
        public ulong LastLoginTS { get; set; }

        // Activity data fields
        public ulong RoleId { get; set; }
        public ulong Activity1 { get; set; }
        public ulong Activity2 { get; set; }
        public ulong JoinTS { get; set; }

        // Computed properties for display
        public DateTime? ParsedLastLoginTS => LastLoginTS > 1000000000 && LastLoginTS < 2000000000
            ? DateTimeOffset.FromUnixTimeSeconds((long)LastLoginTS).DateTime
            : null;

        public DateTime? ParsedJoinTS => JoinTS > 1000000000 && JoinTS < 2000000000
            ? DateTimeOffset.FromUnixTimeSeconds((long)JoinTS).DateTime
            : null;

        // Display-friendly properties
        public string ClassDisplay => MessageAnalyzer.GetProfessionNameFromId(ClassId);//$"{ClassId}-{ClassVariant}";
        public string LastLoginDisplay => ParsedLastLoginTS?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown";
        public string JoinDateDisplay => ParsedJoinTS?.ToString("yyyy-MM-dd") ?? "Unknown";
        public string RoleDisplay => RoleId.ToString();
    }
}
