//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Text;

    internal class UserAgentContainer
    {
        private static readonly string baseUserAgent;
        private string userAgent;
        private byte[] userAgentUTF8;
        private string suffix;
        private const int maxSuffixLength = 64;

        static UserAgentContainer()
        {
            UserAgentContainer.baseUserAgent = CustomTypeExtensions.GenerateBaseUserAgentString();
        }

        public UserAgentContainer()
        {
            this.userAgent = this.BaseUserAgent;
            this.userAgentUTF8 = Encoding.UTF8.GetBytes(this.BaseUserAgent);
        }

        public UserAgentContainer(string suffix) : this()
        {
            this.Suffix = suffix;
        }

        public string UserAgent
        {
            get
            {
                return this.userAgent;
            }
        }

        public byte[] UserAgentUTF8
        {
            get
            {
                return this.userAgentUTF8;
            }
        }

        public string Suffix
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

                this.userAgent = this.BaseUserAgent + suffix;
                this.userAgentUTF8 = Encoding.UTF8.GetBytes(this.userAgent);
            }
        }

        internal virtual string BaseUserAgent
        {
            get
            {
                return UserAgentContainer.baseUserAgent;
            }
        }
    }
}