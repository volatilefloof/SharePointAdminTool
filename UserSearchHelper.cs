using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntraGroupsApp
{
    public static class UserSearchHelper
    {
        public static string BuildGraphSearchQuery(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var tokens = Tokenize(input);
            var searchParts = new List<string>();

            foreach (var token in tokens)
            {
                searchParts.Add($"displayName:{token}");
                searchParts.Add($"givenName:{token}");
                searchParts.Add($"surname:{token}");
                searchParts.Add($"userPrincipalName:{token}");
                searchParts.Add($"mail:{token}");
            }

            return $"\"{string.Join(" OR ", searchParts)}\"";
        }

        public static List<string> Tokenize(string input)
        {
            return input
                .Replace(",", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length > 1)
                .ToList();
        }

        public static List<(User User, int Score)> FilterAndRank(List<User> users, string input)
        {
            var tokens = Tokenize(input);
            var scored = new List<(User User, int Score)>(); // 👈 named tuple fields

            foreach (var user in users)
            {
                int score = 0;
                foreach (var t in tokens)
                {
                    if (!string.IsNullOrEmpty(user.DisplayName) && user.DisplayName.ToLowerInvariant().Contains(t)) score += 5;
                    if (!string.IsNullOrEmpty(user.GivenName) && user.GivenName.ToLowerInvariant().Contains(t)) score += 4;
                    if (!string.IsNullOrEmpty(user.Surname) && user.Surname.ToLowerInvariant().Contains(t)) score += 4;
                    if (!string.IsNullOrEmpty(user.UserPrincipalName) && user.UserPrincipalName.ToLowerInvariant().Contains(t)) score += 3;
                    if (!string.IsNullOrEmpty(user.Mail) && user.Mail.ToLowerInvariant().Contains(t)) score += 3;
                }

                if (score > 0)
                    scored.Add((User: user, Score: score)); // 👈 name fields explicitly
            }

            return scored.OrderByDescending(x => x.Score).ToList();
        }
    }
}
