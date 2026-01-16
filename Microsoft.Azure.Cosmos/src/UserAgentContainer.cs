// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Contains information about the user environment and helps identify requests.
    /// </summary>
    internal class UserAgentContainer : Documents.UserAgentContainer
    {
        private const int MaxOperatingSystemString = 30;
        private const int MaxClientId = 10;
        private const string PipeDelimiter = "|";

        private readonly string cosmosBaseUserAgent;
        private readonly string clientId;
        private static readonly Regex regex = new Regex(@"F\d+\|", RegexOptions.Compiled);

        public UserAgentContainer(
            int clientId,
            string features = null,
            string regionConfiguration = "NS",
            string suffix = null) 
               : base()
        {
            this.clientId = System.Math.Min(clientId, UserAgentContainer.MaxClientId).ToString();
            this.cosmosBaseUserAgent = this.CreateBaseUserAgentString(
                features: features,
                regionConfiguration: regionConfiguration);
            this.Suffix = suffix ?? string.Empty;
        }

        public void AppendFeatures(
            string features)
        {
            if (!string.IsNullOrEmpty(features))
            {
                // Here we have 3 scenarios: 
                // 1. Suffix is empty, we just set it to the features.
                // 2. Suffix is not empty, we append the features to the existing suffix.
                // 3. Suffix already contains features, we the new features in the existing suffix.
                this.Suffix = string.IsNullOrEmpty(this.Suffix)
                    ? features
                    : this.HasFeatureFlag()
                        ? $"{features}{this.Suffix.Substring(this.Suffix.IndexOf(UserAgentContainer.PipeDelimiter))}"
                        : $"{features}{UserAgentContainer.PipeDelimiter}{this.Suffix}";
            }
            else
            {
                // Here we have 3 scenarios: 
                // 1. Suffix is empty, we just set it to empty.
                // 2. Suffix is not empty, we remove the features from the existing suffix.
                // 3. Suffix already contains features, we remove the features from the existing suffix.
                this.Suffix = string.IsNullOrEmpty(this.Suffix)
                    ? string.Empty
                    : this.HasFeatureFlag()
                        //if the suffix contains a feature flag we can assume that the first pipe delimiter marks the end of it
                        ? this.Suffix.Substring(this.Suffix.IndexOf(UserAgentContainer.PipeDelimiter) + 1)
                        : this.Suffix;
            }
        }

        private bool HasFeatureFlag()
        {
            if (string.IsNullOrEmpty(this.Suffix))
            {
                return false;
            }

            // Matches 'F' followed by one or more digits, then a pipe '|'
            return regex.IsMatch(this.Suffix);
        }

        internal override string BaseUserAgent => this.cosmosBaseUserAgent ?? string.Empty;

        protected virtual void GetEnvironmentInformation(
            out string clientVersion,
            out string processArchitecture,
            out string operatingSystem,
            out string runtimeFramework)
        {
            EnvironmentInformation environmentInformation = new EnvironmentInformation();
            clientVersion = environmentInformation.ClientVersion;
            processArchitecture = environmentInformation.ProcessArchitecture;
            operatingSystem = environmentInformation.OperatingSystem;
            runtimeFramework = environmentInformation.RuntimeFramework;
        }

        private string CreateBaseUserAgentString(
            string features = null,
            string regionConfiguration = null)
        {
            this.GetEnvironmentInformation(
                out string clientVersion,
                out string processArchitecture,
                out string operatingSystem,
                out string runtimeFramework);

            if (operatingSystem.Length > MaxOperatingSystemString)
            {
                operatingSystem = operatingSystem.Substring(0, MaxOperatingSystemString);
            }

            // Regex replaces all special characters with empty space except . - | since they do not cause format exception for the user agent string.
            // Do not change the cosmos-netstandard-sdk as it is required for reporting
            string previewFlag = string.Empty;
#if PREVIEW
            previewFlag = "P";
#endif
            string baseUserAgent = $"cosmos-netstandard-sdk/{clientVersion}" + previewFlag + Regex.Replace($"|{this.clientId}|{processArchitecture}|{operatingSystem}|{runtimeFramework}|", @"[^0-9a-zA-Z\.\|\-]+", " ");
            if (!string.IsNullOrEmpty(regionConfiguration))
            {
                baseUserAgent += $"{regionConfiguration}|";
            }

            if (!string.IsNullOrEmpty(features))
            {
                baseUserAgent += $"F {features}|";
            }

            return baseUserAgent;
        }
    }
}
