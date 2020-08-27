//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Collections;

    interface ISessionContainer
    {
        /// <summary>
        /// Returns a serialized map of partitionKeyRangeId to session token. If a entity is name based then the method extracts name from
        /// ResourceAddress and use it to identify collection otherwise it uses ResourceId. Returns empty string if collection is unknown
        /// </summary>
        string ResolveGlobalSessionToken(DocumentServiceRequest entity);

        /// <summary>
        /// Returns a session token identified by partitionKeyRangeId(*) from a collection identified either by ResourceAddress (in case
        /// of name based entity) or either by ResourceId.
        /// (*) If partitionKeyRangeId is not in the collection's partitionKeyRangeId -> token map then method
        ///     iterates through request.RequestContext.ResolvedPartitionKeyRange.Parents starting from tail and
        ///     returns a corresponding token if there is a match.
        /// </summary>
        ISessionToken ResolvePartitionLocalSessionToken(DocumentServiceRequest entity, string partitionKeyRangeId);

        /// <summary>
        /// Atomicly: removes partitionKeyRangeId -> token map assosiated with collectionFullname, maps collectionFullname to resourceId and
        /// removes its map as well.
        /// </summary>
        void ClearTokenByCollectionFullname(string collectionFullname);

        /// <summary>
        /// Atomicly: removes partitionKeyRangeId -> token map assosiated with resourceId, maps resourceId to collectionFullname and removes its map as well
        /// </summary>
        void ClearTokenByResourceId(string resourceId);

        /// <summary>
        /// Infers collectionName using responseHeaders[HttpConstants.HttpHeaders.OwnerFullName] or request.ResourceAddress,
        /// infers resourceId using responseHeaders[HttpConstants.HttpHeaders.OwnerId] or request.ResourceId,
        /// and adds responseHeaders[HttpConstants.HttpHeaders.SessionToken] session token to the (collectionName, resourceId)'s
        ///  partitionKeyRangeId -> token map.
        /// NB: Silently does nothing for master queries, or when it's impossible to infer collectionRid and collectionFullname
        ///     from the request, or then SessionToken is missing in responseHeader.
        /// </summary>
        void SetSessionToken(DocumentServiceRequest request, INameValueCollection responseHeader);

        /// <summary>
        /// Adds responseHeaders[HttpConstants.HttpHeaders.SessionToken] session token to the (collectionName, collectionRid)'s
        /// partitionKeyRangeId -> token map.
        /// </summary>
        /// <param name="collectionRid"></param>
        /// <param name="collectionFullname"></param>
        /// <param name="responseHeaders"></param>
        void SetSessionToken(string collectionRid, string collectionFullname, INameValueCollection responseHeaders);
    }
}
