// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Contains information about the user environment and helps identify requests.
    /// </summary>
    internal sealed class UserAgentContainer : Documents.UserAgentContainer
    {
        private const int MaxOperatingSystemString = 30;
        private string cosmosBaseUserAgent;

        public UserAgentContainer()
            : base()
        {
        }

        internal override string BaseUserAgent
        {
            get
            {
                if (this.cosmosBaseUserAgent == null)
                {
                    this.cosmosBaseUserAgent = this.CreateBaseUserAgentString();
                }

                return this.cosmosBaseUserAgent;
            }
        }

        internal void SetFeatures(string features)
        {
            // Regenerate base user agent to account for features
            this.cosmosBaseUserAgent = this.CreateBaseUserAgentString(features);
        }

        private string CreateBaseUserAgentString(string features = null)
        {
            EnvironmentInformation environmentInformation = new EnvironmentInformation();
            string operatingSystem = environmentInformation.OperatingSystem;
            if (operatingSystem.Length > MaxOperatingSystemString)
            {
                operatingSystem = operatingSystem.Substring(0, MaxOperatingSystemString);
            }

            // Regex replaces all special characters with empty space except . - | since they do not cause format exception for the user agent string.
            // Do not change the cosmos-netstandard-sdk as it is required for reporting
            string baseUserAgent = $"cosmos-netstandard-sdk/{environmentInformation.ClientVersion}" + Regex.Replace($"|{environmentInformation.DirectVersion}|{environmentInformation.ClientId}|{environmentInformation.ProcessArchitecture}|{operatingSystem}|{environmentInformation.RuntimeFramework}|", @"[^0-9a-zA-Z\.\|\-]+", " ");

            if (!string.IsNullOrEmpty(features))
            {
                baseUserAgent += $"F {features}|";
            }

            return baseUserAgent;
        }
    }
}
