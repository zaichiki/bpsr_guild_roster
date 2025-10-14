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
        /// Combines member data and activity data dictionaries and exports to TSV file
        /// </summary>
        /// <param name="memberData">Dictionary of guild member data</param>
        /// <param name="activityData">Dictionary of guild member activity data</param>
        /// <param name="outputDirectory">Directory to save the TSV file (optional, defaults to current directory)</param>
        /// <returns>Path to the created TSV file</returns>
        public static string ExportToTsv(
            Dictionary<int, GuildMemberData> memberData, 
            Dictionary<int, GuildMemberActivityData> activityData,
            string outputDirectory = "")
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
                // Use only IDs from activityData dictionary
                var allUserIds = activityData.Keys.OrderBy(id => id);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write TSV header
                    writer.WriteLine("UserId\tUserIdSecondary\tPlayerName\tCharacterLevel\tGuildName\tClassId\tClassVariant\tGearScore\tRoleId\tActivity1\tActivity2\tLastLogin\tParsedLastLogin\tJoinTS\tParsedJoinTS");

                    foreach (var userId in allUserIds)
                    {
                        var member = memberData.ContainsKey(userId) ? memberData[userId] : null;
                        var activity = activityData.ContainsKey(userId) ? activityData[userId] : null;

                        // Create combined data row
                        var row = new List<string>
                        {
                            userId.ToString(),
                            member?.UserIdSecondary.ToString() ?? "",
                            EscapeTsvField(member?.PlayerName ?? ""),
                            member?.CharacterLevel.ToString() ?? "",
                            EscapeTsvField(member?.GuildName ?? ""),
                            member?.ClassId.ToString() ?? "",
                            member?.ClassVariant.ToString() ?? "",
                            member?.GearScore.ToString() ?? "",
                            activity?.RoleId.ToString() ?? "",
                            activity?.Activity1.ToString() ?? "",
                            activity?.Activity2.ToString() ?? "",
                            member?.LastLoginTS.ToString() ?? "",
                            member?.ParsedLastLoginTS?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                            activity?.JoinTS.ToString() ?? "",
                            activity?.ParsedJoinTS?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
                        };

                        writer.WriteLine(string.Join("\t", row));
                    }
                }

                Console.WriteLine($"Guild roster exported successfully to: {filePath}");
                Console.WriteLine($"Total records: {allUserIds.Count()}");
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting guild roster: {ex.Message}");
                throw;
            }
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
}
