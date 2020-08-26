//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal sealed class PathParser
    {
        private static readonly char segmentSeparator = '/';
        private static readonly string errorMessageFormat = "Invalid path, failed at {0}";

        /// <summary>
        /// Extract parts from path
        /// </summary>
        /// <remarks>
        /// This code doesn't do as much validation as the backend, as it assumes that IndexingPolicy path coming from the backend is valid.
        /// </remarks>
        /// <param name="path">A path string</param>
        /// <returns>An array of parts of path</returns>
        public static string[] GetPathParts(string path)
        {
            List<string> tokens = new List<string>();
            int currentIndex = 0;

            while (currentIndex < path.Length)
            {
                if (path[currentIndex] != segmentSeparator)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, errorMessageFormat, currentIndex));
                }

                if (++currentIndex == path.Length) break;

                if (path[currentIndex] == '\"' || path[currentIndex] == '\'')
                {
                    tokens.Add(GetEscapedToken(path, ref currentIndex));
                }
                else
                {
                    tokens.Add(GetToken(path, ref currentIndex));
                }
            }

            return tokens.ToArray();
        }

        private static string GetEscapedToken(string path, ref int currentIndex)
        {
            char quote = path[currentIndex];
            int newIndex = ++currentIndex;

            while (true)
            {
                newIndex = path.IndexOf(quote, newIndex);
                if (newIndex == -1)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, errorMessageFormat, currentIndex));
                }

                if (path[newIndex - 1] != '\\')
                {
                    break;
                }

                ++newIndex;
            }

            string token = path.Substring(currentIndex, newIndex - currentIndex);
            currentIndex = newIndex + 1;
            return token;
        }

        private static string GetToken(string path, ref int currentIndex)
        {
            int newIndex = path.IndexOf(segmentSeparator, currentIndex);
            string token;
            if (newIndex == -1)
            {
                token = path.Substring(currentIndex);
                currentIndex = path.Length;
            }
            else
            {
                token = path.Substring(currentIndex, newIndex - currentIndex);
                currentIndex = newIndex;
            }

            token = token.Trim();
            return token;
        }
    }
}
