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
        private readonly string cosmosBaseUserAgent;

        public UserAgentContainer(
            string features = null,
            string regionConfiguration = "N",
            string suffix = null) 
               : base()
        {
            this.cosmosBaseUserAgent = this.CreateBaseUserAgentString(
                features: features,
                regionConfiguration: regionConfiguration);
            this.Suffix = suffix ?? string.Empty;
        }

        internal override string BaseUserAgent => this.cosmosBaseUserAgent ?? string.Empty;

        protected virtual void GetEnvironmentInformation(
            out string clientVersion,
            out string directVersion,
            out string clientId,
            out string processArchitecture,
            out string operatingSystem,
            out string runtimeFramework)
        {
            EnvironmentInformation environmentInformation = new EnvironmentInformation();
            clientVersion = environmentInformation.ClientVersion;
            directVersion = environmentInformation.DirectVersion;
            clientId = CosmosClient.numberOfClientsCreated.ToString();
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
                out string directVersion,
                out string clientId,
                out string processArchitecture,
                out string operatingSystem,
                out string runtimeFramework);

            if (operatingSystem.Length > MaxOperatingSystemString)
            {
                operatingSystem = operatingSystem.Substring(0, MaxOperatingSystemString);
            }

            // Regex replaces all special characters with empty space except . - | since they do not cause format exception for the user agent string.
            // Do not change the cosmos-netstandard-sdk as it is required for reporting
            string baseUserAgent = $"cosmos-netstandard-sdk/{clientVersion}" + Regex.Replace($"|{directVersion}|{clientId}|{processArchitecture}|{operatingSystem}|{runtimeFramework}|", @"[^0-9a-zA-Z\.\|\-]+", " ");

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
