//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using System;

    internal interface IServiceIdentity
    {
        string GetFederationId();

        Uri GetServiceUri();

        long GetPartitionKey();
    }

    internal sealed class ServiceIdentity : IServiceIdentity 
    {
        /// <summary>
        /// Needed for TestForWhiteListedPersistedTypes to succeed 
        /// </summary>
        private ServiceIdentity()
        {
        }

        public ServiceIdentity(string federationId, Uri serviceName, bool isMasterService)
        {
            this.FederationId = federationId;
            this.ServiceName = serviceName;
            this.IsMasterService = isMasterService;
        }

        public string FederationId
        {
            get;
            private set;
        }

        public Uri ServiceName
        {
            get;
            private set;
        }

        public bool IsMasterService
        {
            get;
            private set;
        }

        public string ApplicationName
        {
            get
            {
                if (this.ServiceName == null)
                {
                    return string.Empty;
                }
                else
                {
                    return this.ServiceName.AbsoluteUri.Substring(0, this.ServiceName.AbsoluteUri.LastIndexOf('/'));
                }
            }
        }

        public string GetFederationId()
        {
            return this.FederationId;
        }
        public Uri GetServiceUri()
        {
            return this.ServiceName;
        }

        public long GetPartitionKey()
        {
            return 0;
        }

        public override bool Equals(object obj)
        {
            ServiceIdentity other = obj as ServiceIdentity;

            return other != null &&
                string.Compare(this.FederationId, other.FederationId, StringComparison.OrdinalIgnoreCase) == 0 &&
                Uri.Compare(this.ServiceName, other.ServiceName, UriComponents.AbsoluteUri, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public override int GetHashCode()
        {
            return (this.FederationId == null ? 0 : this.FederationId.GetHashCode()) ^ 
                (this.ServiceName == null ? 0 : this.ServiceName.GetHashCode());
        }

        public override string ToString()
        {
            return $"FederationId:{this.FederationId},ServiceName:{this.ServiceName},IsMasterService:{this.IsMasterService}";
        }
    }
}