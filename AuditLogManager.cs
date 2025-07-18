using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace EntraGroupsApp
{
    public class AuditLogEntry
    {
        public string UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; }
        public string GroupName { get; set; }
        public string TargetName { get; set; }
        public string TargetType { get; set; }
        public string Details { get; set; }
    }

    public class AuditLogManager
    {
        private readonly List<AuditLogEntry> _logs;
        private readonly string _logFilePath;
        private readonly object _fileLock = new object();

        public AuditLogManager()
        {
            _logs = new List<AuditLogEntry>();
            // Use a user-writable directory for the log file
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EntraGroupsApp");
            Directory.CreateDirectory(appDataPath); // Ensure directory exists
            _logFilePath = Path.Combine(appDataPath, "AuditLog.json");
            LoadLogsFromFile();
        }

        private void LoadLogsFromFile()
        {
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(_logFilePath))
                    {
                        string json = File.ReadAllText(_logFilePath);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var loadedLogs = JsonSerializer.Deserialize<List<AuditLogEntry>>(json);
                            if (loadedLogs != null)
                            {
                                lock (_logs)
                                {
                                    _logs.AddRange(loadedLogs);
                                }
                                Console.WriteLine($"Loaded {_logs.Count} audit logs from {_logFilePath}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading audit logs from {_logFilePath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                }
            }
        }

        private void SaveLogsToFile()
        {
            lock (_fileLock)
            {
                try
                {
                    string json;
                    lock (_logs)
                    {
                        json = JsonSerializer.Serialize(_logs, new JsonSerializerOptions { WriteIndented = true });
                    }
                    File.WriteAllText(_logFilePath, json);
                    Console.WriteLine($"Saved {_logs.Count} audit logs to {_logFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving audit logs to {_logFilePath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                }
            }
        }

        public async Task LogAction(string userId, string userName, string actionType, string groupName, string targetName, string targetType, string details)
        {
            var entry = new AuditLogEntry
            {
                UserId = userId,
                Timestamp = DateTime.Now,
                ActionType = actionType,
                GroupName = groupName,
                TargetName = targetName ?? userName,
                TargetType = targetType,
                Details = details
            };

            lock (_logs)
            {
                _logs.Add(entry);
            }

            SaveLogsToFile();

            await Task.CompletedTask;
        }

        public List<AuditLogEntry> GetLogsByUserAndDate(string userId, DateTime? date)
        {
            lock (_logs)
            {
                var query = _logs.AsQueryable();

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(l => l.UserId == userId);
                }

                if (date.HasValue)
                {
                    var startDate = date.Value.Date;
                    var endDate = startDate.AddDays(1);
                    query = query.Where(l => l.Timestamp >= startDate && l.Timestamp < endDate);
                }

                return query.ToList();
            }
        }

        public List<AuditLogEntry> GetLogsByUserAndDateRange(string userId, DateTime startDate, DateTime endDate)
        {
            lock (_logs)
            {
                var query = _logs.AsQueryable();

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(l => l.UserId == userId);
                }

                query = query.Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate);

                return query.ToList();
            }
        }

        public int PurgeLogsByUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
            }

            lock (_logs)
            {
                var count = _logs.RemoveAll(l => l.UserId == userId);
                SaveLogsToFile();
                return count;
            }
        }

        public int PurgeLogsByUserAndDateRange(string userId, DateTime startDate, DateTime endDate)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
            }

            if (startDate > endDate)
            {
                throw new ArgumentException("Start date must be before or equal to end date.");
            }

            lock (_logs)
            {
                var count = _logs.RemoveAll(l => l.UserId == userId && l.Timestamp >= startDate && l.Timestamp <= endDate);
                SaveLogsToFile();
                return count;
            }
        }

        public string ExportLogsToJson(List<AuditLogEntry> logs)
        {
            return JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}