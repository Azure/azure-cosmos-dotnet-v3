// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Contains information about the user environment and helps identify requests.
    /// </summary>
    internal class UserAgentContainer : Documents.UserAgentContainer
    {
        private const int MaxOperatingSystemString = 30;
        private const int MaxClientId = 10;
        private readonly string cosmosBaseUserAgent;
        private readonly string clientId;
        private CosmosClientOptionsFeatures features;

        public UserAgentContainer(
            int clientId,
            CosmosClientOptionsFeatures features,
            string regionConfiguration = "NS",
            string suffix = null) 
               : base()
        {
            this.clientId = System.Math.Min(clientId, UserAgentContainer.MaxClientId).ToString();
            this.features = features;

            string featureString = null;
            if (features != CosmosClientOptionsFeatures.NoFeatures)
            {
                featureString = Convert.ToString((int)features, 4).PadLeft(8, '0');
            }

            this.cosmosBaseUserAgent = this.CreateBaseUserAgentString(
                features: featureString,
                regionConfiguration: regionConfiguration);

            this.Suffix = suffix ?? string.Empty;
        }

        internal override string BaseUserAgent => this.cosmosBaseUserAgent ?? string.Empty;

        public void UpdateFeatureFlag(CosmosClientOptionsFeatures cosmosClientOptionsFeatures)
        {
            this.features |= cosmosClientOptionsFeatures;
        }

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
