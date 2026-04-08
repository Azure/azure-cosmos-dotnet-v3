// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Security
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Validates and sanitizes SQL queries to prevent injection and abuse.
    /// </summary>
    internal static class QuerySanitizer
    {
        private static readonly Regex DdlPattern = new(
            @"\b(CREATE|ALTER|DROP|TRUNCATE|EXEC|EXECUTE|INSERT|UPDATE|DELETE|MERGE)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Validates a SQL query string. Returns null if valid, or an error message if rejected.
        /// </summary>
        public static string? Validate(string query, int maxLengthChars)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Query cannot be empty.";
            }

            if (query.Length > maxLengthChars)
            {
                return $"Query exceeds maximum length of {maxLengthChars} characters.";
            }

            string trimmed = query.TrimStart();

            if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return "Only SELECT queries are allowed.";
            }

            if (DdlPattern.IsMatch(trimmed.Substring("SELECT".Length)))
            {
                return "Query contains disallowed keywords (DDL/DML statements are not permitted).";
            }

            return null;
        }
    }
}
