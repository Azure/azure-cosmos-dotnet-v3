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

        private string CreateBaseUserAgentString()
        {
            EnvironmentInformation environmentInformation = new EnvironmentInformation();
            string operatingSystem = environmentInformation.OperatingSystem;
            if (operatingSystem.Length > MaxOperatingSystemString)
            {
                operatingSystem = operatingSystem.Substring(0, MaxOperatingSystemString);
            }

            // Regex replaces all special characters with empty space except . - | since they do not cause format exception for the user agent string.
            return Regex.Replace($"cosmos-net|{environmentInformation.ClientVersion}|{environmentInformation.DirectVersion}|{environmentInformation.ClientId}|{environmentInformation.ProcessArchitecture}|{operatingSystem}|", @"[^0-9a-zA-Z\.\|\-]+", " ");
        }
    }
}
