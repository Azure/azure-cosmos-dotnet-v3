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
            string infoString = environmentInformation.ToString();

            // Two Backslashes in the same string without a space will cause the HTTP to throw a format exception 
            UserAgentContainer.cosmosBaseUserAgent = infoString.Replace("/", "-");
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
