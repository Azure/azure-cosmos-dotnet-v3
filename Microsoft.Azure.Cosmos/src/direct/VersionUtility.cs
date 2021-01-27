//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;

    internal static class VersionUtility
    {
        private const string versionDateTimeFormat = "yyyy-MM-dd";
        private const string previewVersionDateTimeFormat = "yyyy-MM-dd-preview";

        private static readonly IReadOnlyDictionary<string, DateTime> KnownDateTimes;

        static VersionUtility()
        {
            Dictionary<string, DateTime> knownDateTimesDict = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            VersionUtility.KnownDateTimes = new ReadOnlyDictionary<string, DateTime>(knownDateTimesDict);
            foreach (string versionString in HttpConstants.Versions.SupportedRuntimeAPIVersions)
            {
                if (VersionUtility.TryParseApiVersion(versionString, out DateTime apiVersionDate))
                {
                    knownDateTimesDict[versionString] = apiVersionDate;
                }
            }
        }

#if !CLIENT // once OSS SDK Move to the DateTime version flip this off everywhere.
        // Format is YYYY-MM-DD
        // true if compareVersion >= baseVersion.
        internal static bool IsLaterThan(string compareVersion, string baseVersion)
        {
            if (baseVersion.ToLowerInvariant().Contains("preview")
                && !compareVersion.ToLowerInvariant().Contains("preview"))
            {
                // Only another preview API version can be considered to be later than a base preview API version
                return false;
            }

            if (!VersionUtility.TryParseApiVersion(baseVersion, out DateTime baseVersionDate))
            {
                string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidVersionFormat, "base", baseVersion);
                throw new BadRequestException(errorMessage);
            }

            return VersionUtility.IsLaterThan(compareVersion, baseVersionDate);
        }
#endif

        internal static bool IsValidApiVersion(string apiVersion)
        {
            return TryParseApiVersion(apiVersion, out _);
        }

        // Format is YYYY-MM-DD
        // true if compareVersion >= baseVersion.
        internal static bool IsLaterThan(string compareVersion, DateTime baseVersion)
        {
            if (!VersionUtility.TryParseApiVersion(compareVersion, out DateTime compareVersionDate))
            {
                string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidVersionFormat, "compare", compareVersion);
                throw new BadRequestException(errorMessage);
            }

            return compareVersionDate.CompareTo(baseVersion) >= 0;
        }

        // Format is YYYY-MM-DD
        // true if compareVersion > baseVersion.
        internal static bool IsLaterThanNotEqualTo(string compareVersion, DateTime baseVersion)
        {
            if (!VersionUtility.TryParseApiVersion(compareVersion, out DateTime compareVersionDate))
            {
                string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.InvalidVersionFormat, "compare", compareVersion);
                throw new BadRequestException(errorMessage);
            }

            return compareVersionDate.CompareTo(baseVersion) > 0;
        }

        internal static DateTime ParseNonPreviewDateTimeExact(string apiVersion)
        {
            return DateTime.ParseExact(
                apiVersion, VersionUtility.versionDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        private static bool TryParseApiVersion(string apiVersion, out DateTime apiVersionDate)
        {
            if (!VersionUtility.KnownDateTimes.TryGetValue(apiVersion, out apiVersionDate))
            {
                return TryParseApiVersionCore(apiVersion, out apiVersionDate);
            }

            return true;
        }

        private static bool TryParseApiVersionCore(string apiVersion, out DateTime apiVersionDate)
        {
            string dateTimeFormat;
            if (apiVersion.ToLowerInvariant().Contains("preview"))
            {
                dateTimeFormat = VersionUtility.previewVersionDateTimeFormat;
            }
            else
            {
                dateTimeFormat = VersionUtility.versionDateTimeFormat;
            }

            return DateTime.TryParseExact(
                apiVersion, dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out apiVersionDate);
        }
    }
 }