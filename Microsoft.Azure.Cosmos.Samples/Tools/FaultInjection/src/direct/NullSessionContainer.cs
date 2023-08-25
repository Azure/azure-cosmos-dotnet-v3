//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;

    internal class NullSessionContainer : ISessionContainer
    {
        public string ResolveGlobalSessionToken(DocumentServiceRequest entity)
        {
            return string.Empty;
        }

        public ISessionToken ResolvePartitionLocalSessionToken(DocumentServiceRequest entity, string partitionKeyRangeId)
        {
            return null;
        }

        public void ClearTokenByResourceId(string resourceId)
        {
        }

        public void ClearTokenByCollectionFullname(string collectionFullname)
        {
        }

        public void SetSessionToken(DocumentServiceRequest request, INameValueCollection header)
        {
        }

        public void SetSessionToken(string collectionRid, string collectionFullname, INameValueCollection responseHeaders)
        {
        }

    }
}
