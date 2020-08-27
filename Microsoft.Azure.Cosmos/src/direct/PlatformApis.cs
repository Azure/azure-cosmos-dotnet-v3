//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;

    internal static class PlatformApis
    {
        private class DistroInfo
        {
            public string Id;
            public string VersionId;
        }

        private static readonly Lazy<Platform> _platform = new Lazy<Platform>(DetermineOSPlatform);
        private static readonly Lazy<DistroInfo> _distroInfo = new Lazy<DistroInfo>(LoadDistroInfo);

        public static string GetOSName()
        {
            switch (GetOSPlatform())
            {
                case Platform.Windows:
                    return "Windows";
                case Platform.Linux:
                    return GetDistroId() ?? "Linux";
                case Platform.Darwin:
                    return "Mac OS X";
                default:
                    return "Unknown";
            }
        }

        public static string GetOSVersion()
        {
            switch (GetOSPlatform())
            {
                case Platform.Windows:
                    return GetWindowsVersion(RuntimeInformation.OSDescription) ?? string.Empty;
                case Platform.Linux:
                    return GetDistroVersionId() ?? string.Empty;
                case Platform.Darwin:
                    return GetDarwinVersion() ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        public static string GetWindowsVersion(string osDescipiton)
        {
            Regex regex = new Regex(@"([0-9]+\.*)+");
            Match match = regex.Match(osDescipiton);
            return match.ToString();
        }

        private static string GetDarwinVersion()
        {
            Version version;
            string kernelRelease = NativeMethods.Darwin.GetKernelRelease();
            if (!Version.TryParse(kernelRelease, out version) || version.Major < 5)
            {
                // 10.0 covers all versions prior to Darwin 5
                // Similarly, if the version is not a valid version number, but we have still detected that it is Darwin, we just assume
                // it is OS X 10.0
                return "10.0";
            }
            else
            {
                // Mac OS X 10.1 mapped to Darwin 5.x, and the mapping continues that way
                // So just subtract 4 from the Darwin version.
                // https://en.wikipedia.org/wiki/Darwin_%28operating_system%29
                return $"10.{version.Major - 4}";
            }
        }

        public static Platform GetOSPlatform()
        {
            return _platform.Value;
        }

        private static string GetDistroId()
        {
            return _distroInfo.Value?.Id;
        }

        private static string GetDistroVersionId()
        {
            return _distroInfo.Value?.VersionId;
        }

        private static DistroInfo LoadDistroInfo()
        {
            // Sample os-release file:
            //   NAME="Ubuntu"
            //   VERSION = "14.04.3 LTS, Trusty Tahr"
            //   ID = ubuntu
            //   ID_LIKE = debian
            //   PRETTY_NAME = "Ubuntu 14.04.3 LTS"
            //   VERSION_ID = "14.04"
            //   HOME_URL = "http://www.ubuntu.com/"
            //   SUPPORT_URL = "http://help.ubuntu.com/"
            //   BUG_REPORT_URL = "http://bugs.launchpad.net/ubuntu/"
            // We use ID and VERSION_ID

            if (File.Exists("/etc/os-release"))
            {
                string[] lines = File.ReadAllLines("/etc/os-release");
                DistroInfo result = new DistroInfo();
                foreach (string line in lines)
                {
                    if (line.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        result.Id = line.Substring(3).Trim('"', '\'');
                    }
                    else if (line.StartsWith("VERSION_ID=", StringComparison.Ordinal))
                    {
                        result.VersionId = line.Substring(11).Trim('"', '\'');
                    }
                }
                return result;
            }

            string osType = File.Exists("/proc/sys/kernel/ostype")
                ? File.ReadAllText("/proc/sys/kernel/ostype")
                : null;

            string osRelease = File.Exists("/proc/sys/kernel/osrelease")
                ? File.ReadAllText("/proc/sys/kernel/osrelease")
                : null;

            return new DistroInfo() {Id = osType, VersionId = osRelease};
        }
        private static Platform DetermineOSPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Platform.Windows;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Platform.Linux;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Platform.Darwin;
            }
            return Platform.Unknown;
        }
    }
}