// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Text;

    /// <summary>
    /// Contains information about the user environment and helps identify requests.
    /// </summary>
    internal class UserAgentContainer : Documents.UserAgentContainer
    {
        internal const string Delimiter = " ";
        internal new static readonly string baseUserAgent;
        private const int maxSuffixLength = 64;
        private string suffix;

        static UserAgentContainer()
        {
            EnvironmentInformation environmentInformation = new EnvironmentInformation();
            UserAgentContainer.baseUserAgent = environmentInformation.ToString();
        }

        public UserAgentContainer()
        {
            this.UserAgent = UserAgentContainer.baseUserAgent;
            this.UserAgentUTF8 = Encoding.UTF8.GetBytes(this.UserAgent);
        }

        public new string UserAgent { get; private set; }

        public new byte[] UserAgentUTF8 { get; private set; }

        public new string Suffix
        {
            get
            {
                return this.suffix;
            }
            set
            {
                this.suffix = value;

                // Take only the first 64 characters of the user-agent.
                if (this.suffix.Length > maxSuffixLength)
                {
                    this.suffix = this.suffix.Substring(0, 64);
                }

                this.UserAgent = UserAgentContainer.baseUserAgent + UserAgentContainer.Delimiter + this.suffix;
                this.UserAgentUTF8 = Encoding.UTF8.GetBytes(this.UserAgent);
            }
        }
    }
}
