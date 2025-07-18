using System.Text.Json;

namespace EntraGroupsApp
{
    public static class UserDepartmentManager
    {
        private static readonly string _assignmentFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EntraGroupsApp",
            "WebView2",
            "UserDepartmentAssignments.json");
        private static readonly object _fileLock = new object();

        public static void AssignDepartment(string userId, string userDisplayName, string department)
        {
            lock (_fileLock)
            {
                var assignments = GetAllAssignments();
                var existing = assignments.Find(a => a.UserId == userId);
                if (existing != null)
                {
                    existing.Department = department;
                    existing.UserDisplayName = userDisplayName;
                }
                else
                {
                    assignments.Add(new UserDepartmentAssignment
                    {
                        UserId = userId,
                        UserDisplayName = userDisplayName,
                        Department = department
                    });
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_assignmentFilePath));
                    var json = JsonSerializer.Serialize(assignments, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_assignmentFilePath, json);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to save department assignment: {ex.Message}", ex);
                }
            }
        }

        public static void RemoveAssignment(string userId)
        {
            lock (_fileLock)
            {
                var assignments = GetAllAssignments();
                assignments.RemoveAll(a => a.UserId == userId);

                try
                {
                    if (assignments.Count == 0)
                    {
                        if (File.Exists(_assignmentFilePath))
                        {
                            File.Delete(_assignmentFilePath);
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_assignmentFilePath));
                        var json = JsonSerializer.Serialize(assignments, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(_assignmentFilePath, json);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to remove department assignment: {ex.Message}", ex);
                }
            }
        }

        public static string GetAssignedDepartment(string userId)
        {
            lock (_fileLock)
            {
                var assignments = GetAllAssignments();
                var assignment = assignments.Find(a => a.UserId == userId);
                return assignment?.Department ?? string.Empty;
            }
        }

        public static List<UserDepartmentAssignment> GetAllAssignments()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(_assignmentFilePath))
                    {
                        return new List<UserDepartmentAssignment>();
                    }

                    var json = File.ReadAllText(_assignmentFilePath);
                    return JsonSerializer.Deserialize<List<UserDepartmentAssignment>>(json) ?? new List<UserDepartmentAssignment>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading assignments: {ex.Message}");
                    return new List<UserDepartmentAssignment>();
                }
            }
        }

        public class UserDepartmentAssignment
        {
            public string UserId { get; set; }
            public string UserDisplayName { get; set; }
            public string Department { get; set; }
        }
    }
}