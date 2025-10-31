//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Helper class for processing JSON patch paths to handle numeric-looking strings
    /// </summary>
    internal static class PatchPathHelper
    {
        private const int MaxNumericTokenLength = 19;
        private static readonly Regex NumericRegex = new Regex(@"^\d+$", RegexOptions.Compiled);

        /// <summary>
        /// Processes a path to ensure long numeric-looking strings are handled correctly
        /// </summary>
        /// <param name="path">The original path</param>
        /// <returns>The processed path with proper escaping for long numeric strings</returns>
        public static string ProcessPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            // Split the path into segments
            var segments = path.Split('/');
            var result = new StringBuilder();
            bool isFirstSegment = true;
            
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                
                // First segment is empty for paths starting with '/'
                if (i == 0 && string.IsNullOrEmpty(segment))
                {
                    result.Append('/');
                    continue;
                }
                
                if (!isFirstSegment)
                {
                    result.Append('/');
                }
                
                // Check if segment is a long numeric string
                if (IsLongNumericString(segment))
                {
                    // Escape by prefixing with a zero-width character approach
                    // or by using the array index notation to force string interpretation
                    result.Append(EscapeNumericString(segment));
                }
                else
                {
                    result.Append(segment);
                }
                
                isFirstSegment = false;
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Checks if a string is a numeric string longer than the maximum allowed length
        /// </summary>
        /// <param name="segment">The path segment to check</param>
        /// <returns>True if it's a long numeric string that needs escaping</returns>
        private static bool IsLongNumericString(string segment)
        {
            if (string.IsNullOrEmpty(segment))
            {
                return false;
            }

            // Check if it's numeric and longer than the maximum allowed length
            return NumericRegex.IsMatch(segment) && segment.Length > MaxNumericTokenLength;
        }

        /// <summary>
        /// Escapes a numeric string to ensure it's treated as a property name
        /// </summary>
        /// <param name="numericString">The numeric string to escape</param>
        /// <returns>The escaped string</returns>
        private static string EscapeNumericString(string numericString)
        {
            // Use array index notation to force string interpretation
            // This tells the server to treat it as a string property rather than a numeric index
            return $"[\"{numericString}\"]";
        }
    }
}