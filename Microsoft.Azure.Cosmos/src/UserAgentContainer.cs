// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Contains information about the user environment and helps identify requests.
    /// </summary>
    internal class UserAgentContainer : Documents.UserAgentContainer
    {
        private static readonly string cosmosBaseUserAgent;

        static UserAgentContainer()
        {
            EnvironmentInformation environmentInformation = new EnvironmentInformation();
            UserAgentContainer.cosmosBaseUserAgent = environmentInformation.ToString();
        }

        public UserAgentContainer()
            : base()
        {
        }

        internal override string BaseUserAgent
        {
            get
            {
                return UserAgentContainer.cosmosBaseUserAgent;
            }
        }
    }
}
