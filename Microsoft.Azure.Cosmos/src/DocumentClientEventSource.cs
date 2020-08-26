//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Net.Http.Headers;
    using Microsoft.Azure.Documents;

    [EventSource(Name = "DocumentDBClient", Guid = "f832a342-0a53-5bab-b57b-d5bc65319768")]
    // Marking it as non-sealed in order to unit test it using Moq framework
    internal class DocumentClientEventSource : EventSource, ICommunicationEventSource
    {
        private static readonly Lazy<DocumentClientEventSource> documentClientEventSourceInstance
            = new Lazy<DocumentClientEventSource>(() => new DocumentClientEventSource());

        public static DocumentClientEventSource Instance => DocumentClientEventSource.documentClientEventSourceInstance.Value;

        internal DocumentClientEventSource()
            : base()
        {
        }

        public class Keywords
        {
            public const EventKeywords HttpRequestAndResponse = (EventKeywords)1;
        }

        [NonEvent]
        private unsafe void WriteEventCoreWithActivityId(Guid activityId, int eventId, int eventDataCount, EventSource.EventData* dataDesc)
        {
            // EventProvider's ActivityId is set on the current thread context (not on the CallContext), so it
            // must be explicitly be set before writing the event.
            CustomTypeExtensions.SetActivityId(ref activityId);

            this.WriteEventCore(eventId, eventDataCount, dataDesc);
        }

        [Event(1,
#pragma warning disable SA1118 // Parameter should not span multiple lines
            Message = "HttpRequest to URI '{2}' with resourceType '{3}' and request headers: accept '{4}', " +
                      "authorization '{5}', consistencyLevel '{6}', contentType '{7}', contentEncoding '{8}', " +
                      "contentLength '{9}', contentLocation '{10}', continuation '{11}', emitVerboseTracesInQuery '{12}', " +
                      "enableScanInQuery '{13}', eTag '{14}', httpDate '{15}', ifMatch '{16}', " +
                      "ifNoneMatch '{17}', indexingDirective '{18}', keepAlive '{19}', offerType '{20}', " +
                      "pageSize '{21}', preTriggerExclude '{22}', preTriggerInclude '{23}', postTriggerExclude '{24}', " +
                      "postTriggerInclude '{25}', profileRequest '{26}', resourceTokenExpiry '{27}', sessionToken '{28}', " +
                      "setCookie '{29}', slug '{30}', userAgent '{31}', xDate'{32}'. " +
                      "ActivityId {0}, localId {1}",
#pragma warning restore SA1118 // Parameter should not span multiple lines
            Keywords = Keywords.HttpRequestAndResponse,
            Level = EventLevel.Verbose)]
        private unsafe void Request(
            Guid activityId,
            Guid localId,
            string uri,
            string resourceType,
            // the following parameters are request headers
            string accept,
            string authorization,
            string consistencyLevel,
            string contentType,
            string contentEncoding,
            string contentLength,
            string contentLocation,
            string continuation,
            string emitVerboseTracesInQuery,
            string enableScanInQuery,
            string eTag,
            string httpDate,
            string ifMatch,
            string ifNoneMatch,
            string indexingDirective,
            string keepAlive,
            string offerType,
            string pageSize,
            string preTriggerExclude,
            string preTriggerInclude,
            string postTriggerExclude,
            string postTriggerInclude,
            string profileRequest,
            string resourceTokenExpiry,
            string sessionToken,
            string setCookie,
            string slug,
            string userAgent,
            string xDate)
        {
            if (uri == null) throw new ArgumentException("uri");
            if (resourceType == null) throw new ArgumentException("resourceType");

            if (accept == null) throw new ArgumentException("accept");
            if (authorization == null) throw new ArgumentException("authorization");
            if (consistencyLevel == null) throw new ArgumentException("consistencyLevel");
            if (contentType == null) throw new ArgumentException("contentType");
            if (contentEncoding == null) throw new ArgumentException("contentEncoding");
            if (contentLength == null) throw new ArgumentException("contentLength");
            if (contentLocation == null) throw new ArgumentException("contentLocation");
            if (continuation == null) throw new ArgumentException("continuation");
            if (emitVerboseTracesInQuery == null) throw new ArgumentException("emitVerboseTracesInQuery");
            if (enableScanInQuery == null) throw new ArgumentException("enableScanInQuery");
            if (eTag == null) throw new ArgumentException("eTag");
            if (httpDate == null) throw new ArgumentException("httpDate");
            if (ifMatch == null) throw new ArgumentException("ifMatch");
            if (ifNoneMatch == null) throw new ArgumentException("ifNoneMatch");
            if (indexingDirective == null) throw new ArgumentException("indexingDirective");
            if (keepAlive == null) throw new ArgumentException("keepAlive");
            if (offerType == null) throw new ArgumentException("offerType");
            if (pageSize == null) throw new ArgumentException("pageSize");
            if (preTriggerExclude == null) throw new ArgumentException("preTriggerExclude");
            if (preTriggerInclude == null) throw new ArgumentException("preTriggerInclude");
            if (postTriggerExclude == null) throw new ArgumentException("postTriggerExclude");
            if (postTriggerInclude == null) throw new ArgumentException("postTriggerInclude");
            if (profileRequest == null) throw new ArgumentException("profileRequest");
            if (resourceTokenExpiry == null) throw new ArgumentException("resourceTokenExpiry");
            if (sessionToken == null) throw new ArgumentException("sessionToken");
            if (setCookie == null) throw new ArgumentException("setCookie");
            if (slug == null) throw new ArgumentException("slug");
            if (userAgent == null) throw new ArgumentException("userAgent");
            if (xDate == null) throw new ArgumentException("xDate");

            byte[] guidBytes = activityId.ToByteArray();
            byte[] localIdBytes = localId.ToByteArray();
            fixed (byte* fixedGuidBytes = guidBytes)
            fixed (byte* fixedLocalIdBytes = localIdBytes)
            fixed (char* fixedUri = uri)
            fixed (char* fixedResourceType = resourceType)
            fixed (char* fixedAccept = accept)
            fixed (char* fixedAuthorization = authorization)
            fixed (char* fixedConsistencyLevel = consistencyLevel)
            fixed (char* fixedContentType = contentType)
            fixed (char* fixedContentEncoding = contentEncoding)
            fixed (char* fixedContentLength = contentLength)
            fixed (char* fixedContentLocation = contentLocation)
            fixed (char* fixedContinuation = continuation)
            fixed (char* fixedEmitVerboseTracesInQuery = emitVerboseTracesInQuery)
            fixed (char* fixedEnableScanInQuery = enableScanInQuery)
            fixed (char* fixedETag = eTag)
            fixed (char* fixedHttpDate = httpDate)
            fixed (char* fixedIfMatch = ifMatch)
            fixed (char* fixedIfNoneMatch = ifNoneMatch)
            fixed (char* fixedIndexingDirective = indexingDirective)
            fixed (char* fixedKeepAlive = keepAlive)
            fixed (char* fixedOfferType = offerType)
            fixed (char* fixedPageSize = pageSize)
            fixed (char* fixedPreTriggerExclude = preTriggerExclude)
            fixed (char* fixedPreTriggerInclude = preTriggerInclude)
            fixed (char* fixedPostTriggerExclude = postTriggerExclude)
            fixed (char* fixedPostTriggerInclude = postTriggerInclude)
            fixed (char* fixedProfileRequest = profileRequest)
            fixed (char* fixedResourceTokenExpiry = resourceTokenExpiry)
            fixed (char* fixedSessionToken = sessionToken)
            fixed (char* fixedSetCookie = setCookie)
            fixed (char* fixedSlug = slug)
            fixed (char* fixedUserAgent = userAgent)
            fixed (char* fixedXDate = xDate)
            {
                const int eventDataCount = 33;
                const int UnicodeEncodingCharSize = CustomTypeExtensions.UnicodeEncodingCharSize;

                EventData* dataDesc = stackalloc EventData[eventDataCount];
                dataDesc[0].DataPointer = (IntPtr)fixedGuidBytes;
                dataDesc[0].Size = guidBytes.Length;

                dataDesc[1].DataPointer = (IntPtr)fixedLocalIdBytes;
                dataDesc[1].Size = localIdBytes.Length;

                dataDesc[2].DataPointer = (IntPtr)fixedUri;
                dataDesc[2].Size = (uri.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[3].DataPointer = (IntPtr)fixedResourceType;
                dataDesc[3].Size = (resourceType.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[4].DataPointer = (IntPtr)fixedAccept;
                dataDesc[4].Size = (accept.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[5].DataPointer = (IntPtr)fixedAuthorization;
                dataDesc[5].Size = (authorization.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[6].DataPointer = (IntPtr)fixedConsistencyLevel;
                dataDesc[6].Size = (consistencyLevel.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[7].DataPointer = (IntPtr)fixedContentType;
                dataDesc[7].Size = (contentType.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[8].DataPointer = (IntPtr)fixedContentEncoding;
                dataDesc[8].Size = (contentEncoding.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[9].DataPointer = (IntPtr)fixedContentLength;
                dataDesc[9].Size = (contentLength.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[10].DataPointer = (IntPtr)fixedContentLocation;
                dataDesc[10].Size = (contentLocation.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[11].DataPointer = (IntPtr)fixedContinuation;
                dataDesc[11].Size = (continuation.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[12].DataPointer = (IntPtr)fixedEmitVerboseTracesInQuery;
                dataDesc[12].Size = (emitVerboseTracesInQuery.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[13].DataPointer = (IntPtr)fixedEnableScanInQuery;
                dataDesc[13].Size = (enableScanInQuery.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[14].DataPointer = (IntPtr)fixedETag;
                dataDesc[14].Size = (eTag.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[15].DataPointer = (IntPtr)fixedHttpDate;
                dataDesc[15].Size = (httpDate.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[16].DataPointer = (IntPtr)fixedIfMatch;
                dataDesc[16].Size = (ifMatch.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[17].DataPointer = (IntPtr)fixedIfNoneMatch;
                dataDesc[17].Size = (ifNoneMatch.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[18].DataPointer = (IntPtr)fixedIndexingDirective;
                dataDesc[18].Size = (indexingDirective.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[19].DataPointer = (IntPtr)fixedKeepAlive;
                dataDesc[19].Size = (keepAlive.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[20].DataPointer = (IntPtr)fixedOfferType;
                dataDesc[20].Size = (offerType.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[21].DataPointer = (IntPtr)fixedPageSize;
                dataDesc[21].Size = (pageSize.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[22].DataPointer = (IntPtr)fixedPreTriggerExclude;
                dataDesc[22].Size = (preTriggerExclude.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[23].DataPointer = (IntPtr)fixedPreTriggerInclude;
                dataDesc[23].Size = (preTriggerInclude.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[24].DataPointer = (IntPtr)fixedPostTriggerExclude;
                dataDesc[24].Size = (postTriggerExclude.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[25].DataPointer = (IntPtr)fixedPostTriggerInclude;
                dataDesc[25].Size = (postTriggerInclude.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[26].DataPointer = (IntPtr)fixedProfileRequest;
                dataDesc[26].Size = (profileRequest.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[27].DataPointer = (IntPtr)fixedResourceTokenExpiry;
                dataDesc[27].Size = (resourceTokenExpiry.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[28].DataPointer = (IntPtr)fixedSessionToken;
                dataDesc[28].Size = (sessionToken.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[29].DataPointer = (IntPtr)fixedSetCookie;
                dataDesc[29].Size = (setCookie.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[30].DataPointer = (IntPtr)fixedSlug;
                dataDesc[30].Size = (slug.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[31].DataPointer = (IntPtr)fixedUserAgent;
                dataDesc[31].Size = (userAgent.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[32].DataPointer = (IntPtr)fixedXDate;
                dataDesc[32].Size = (xDate.Length + 1) * UnicodeEncodingCharSize;

                this.WriteEventCoreWithActivityId(activityId, 1, eventDataCount, dataDesc);
            }
        }

        [NonEvent]
        public void Request(Guid activityId, Guid localId, string uri, string resourceType, HttpRequestHeaders requestHeaders)
        {
            if (this.IsEnabled(EventLevel.Verbose, Keywords.HttpRequestAndResponse))
            {
                string[] keysToExtract =
                {
                    HttpConstants.HttpHeaders.Accept,
                    HttpConstants.HttpHeaders.Authorization,
                    HttpConstants.HttpHeaders.ConsistencyLevel,
                    HttpConstants.HttpHeaders.ContentType,
                    HttpConstants.HttpHeaders.ContentEncoding,
                    HttpConstants.HttpHeaders.ContentLength,
                    HttpConstants.HttpHeaders.ContentLocation,
                    HttpConstants.HttpHeaders.Continuation,
                    HttpConstants.HttpHeaders.EmitVerboseTracesInQuery,
                    HttpConstants.HttpHeaders.EnableScanInQuery,
                    HttpConstants.HttpHeaders.ETag,
                    HttpConstants.HttpHeaders.HttpDate,
                    HttpConstants.HttpHeaders.IfMatch,
                    HttpConstants.HttpHeaders.IfNoneMatch,
                    HttpConstants.HttpHeaders.IndexingDirective,
                    HttpConstants.HttpHeaders.KeepAlive,
                    HttpConstants.HttpHeaders.OfferType,
                    HttpConstants.HttpHeaders.PageSize,
                    HttpConstants.HttpHeaders.PreTriggerExclude,
                    HttpConstants.HttpHeaders.PreTriggerInclude,
                    HttpConstants.HttpHeaders.PostTriggerExclude,
                    HttpConstants.HttpHeaders.PostTriggerInclude,
                    HttpConstants.HttpHeaders.ProfileRequest,
                    HttpConstants.HttpHeaders.ResourceTokenExpiry,
                    HttpConstants.HttpHeaders.SessionToken,
                    HttpConstants.HttpHeaders.SetCookie,
                    HttpConstants.HttpHeaders.Slug,
                    HttpConstants.HttpHeaders.UserAgent,
                    HttpConstants.HttpHeaders.XDate
                };

                string[] headerValues = Helpers.ExtractValuesFromHTTPHeaders(requestHeaders, keysToExtract);
                this.Request(activityId, localId, uri, resourceType, headerValues[0], headerValues[1], headerValues[2], headerValues[3], headerValues[4],
                    headerValues[5], headerValues[6], headerValues[7], headerValues[8], headerValues[9], headerValues[10], headerValues[11], headerValues[12],
                    headerValues[13], headerValues[14], headerValues[15], headerValues[16], headerValues[17], headerValues[18], headerValues[19], headerValues[20],
                    headerValues[21], headerValues[22], headerValues[23], headerValues[24], headerValues[25], headerValues[26], headerValues[27], headerValues[28]);
            }
        }

        [Event(2,
#pragma warning disable SA1118 // Parameter should not span multiple lines
            Message = "HttpResponse took {3}ms with status code {2} and response headers: contentType '{4}', contentEncoding '{5}', " +
                      "contentLength '{6}', contentLocation '{7}', currentMediaStorageUsageInMB '{8}', currentResourceQuotaUsage '{9}', " +
                      "databaseAccountConsumedDocumentStorageInMB '{10}', databaseAccountProvisionedDocumentStorageInMB '{11}', " +
                      "databaseAccountReservedDocumentStorageInMB '{12}', gatewayVersion '{13}', indexingDirective '{14}', itemCount '{15}', " +
                      "lastStateChangeUtc '{16}', maxMediaStorageUsageInMB '{17}', maxResourceQuota '{18}', newResourceId '{19}', " +
                      "ownerFullName '{20}', ownerId '{21}', requestCharge '{22}', requestValidationFailure '{23}', " +
                      "retryAfter '{24}', retryAfterInMilliseconds '{25}', serverVersion '{26}', schemaVersion '{27}', " +
                      "sessionToken '{28}', version '{29}'. ActivityId {0}, localId {1}",
#pragma warning restore SA1118 // Parameter should not span multiple lines
            Keywords = Keywords.HttpRequestAndResponse,
            Level = EventLevel.Verbose)]
        private unsafe void Response(Guid activityId,
            Guid localId,
            short statusCode,
            double milliseconds,
            // the following parameters are response headers
            string contentType,
            string contentEncoding,
            string contentLength,
            string contentLocation,
            string currentMediaStorageUsageInMB,
            string currentResourceQuotaUsage,
            string databaseAccountConsumedDocumentStorageInMB,
            string databaseAccountProvisionedDocumentStorageInMB,
            string databaseAccountReservedDocumentStorageInMB,
            string gatewayVersion,
            string indexingDirective,
            string itemCount,
            string lastStateChangeUtc,
            string maxMediaStorageUsageInMB,
            string maxResourceQuota,
            string newResourceId,
            string ownerFullName,
            string ownerId,
            string requestCharge,
            string requestValidationFailure,
            string retryAfter,
            string retryAfterInMilliseconds,
            string serverVersion,
            string schemaVersion,
            string sessionToken,
            string version)
        {
            if (contentType == null) throw new ArgumentException("contentType");
            if (contentEncoding == null) throw new ArgumentException("contentEncoding");
            if (contentLength == null) throw new ArgumentException("contentLength");
            if (contentLocation == null) throw new ArgumentException("contentLocation");
            if (currentMediaStorageUsageInMB == null) throw new ArgumentException("currentMediaStorageUsageInMB");
            if (currentResourceQuotaUsage == null) throw new ArgumentException("currentResourceQuotaUsage");
            if (databaseAccountConsumedDocumentStorageInMB == null) throw new ArgumentException("databaseAccountConsumedDocumentStorageInMB");
            if (databaseAccountProvisionedDocumentStorageInMB == null) throw new ArgumentException("databaseAccountProvisionedDocumentStorageInMB");
            if (databaseAccountReservedDocumentStorageInMB == null) throw new ArgumentException("databaseAccountReservedDocumentStorageInMB");
            if (gatewayVersion == null) throw new ArgumentException("gatewayVersion");
            if (indexingDirective == null) throw new ArgumentException("indexingDirective");
            if (itemCount == null) throw new ArgumentException("itemCount");
            if (lastStateChangeUtc == null) throw new ArgumentException("lastStateChangeUtc");
            if (maxMediaStorageUsageInMB == null) throw new ArgumentException("maxMediaStorageUsageInMB");
            if (maxResourceQuota == null) throw new ArgumentException("maxResourceQuota");
            if (newResourceId == null) throw new ArgumentException("newResourceId");
            if (ownerFullName == null) throw new ArgumentException("ownerFullName");
            if (ownerId == null) throw new ArgumentException("ownerId");
            if (requestCharge == null) throw new ArgumentException("requestCharge");
            if (requestValidationFailure == null) throw new ArgumentException("requestValidationFailure");
            if (retryAfter == null) throw new ArgumentException("retryAfter");
            if (retryAfterInMilliseconds == null) throw new ArgumentException("retryAfterInMilliseconds");
            if (serverVersion == null) throw new ArgumentException("serverVersion");
            if (schemaVersion == null) throw new ArgumentException("schemaVersion");
            if (sessionToken == null) throw new ArgumentException("sessionToken");
            if (version == null) throw new ArgumentException("version");

            byte[] guidBytes = activityId.ToByteArray();
            byte[] localIdBytes = localId.ToByteArray();
            fixed (byte* fixedGuidBytes = guidBytes)
            fixed (byte* fixedLocalIdBytes = localIdBytes)
            fixed (char* fixedContentType = contentType)
            fixed (char* fixedContentEncoding = contentEncoding)
            fixed (char* fixedContentLength = contentLength)
            fixed (char* fixedContentLocation = contentLocation)
            fixed (char* fixedCurrentMediaStorageUsageInMB = currentMediaStorageUsageInMB)
            fixed (char* fixedCurrentResourceQuotaUsage = currentResourceQuotaUsage)
            fixed (char* fixedDatabaseAccountConsumedDocumentStorageInMB = databaseAccountConsumedDocumentStorageInMB)
            fixed (char* fixedDatabaseAccountProvisionedDocumentStorageInMB = databaseAccountProvisionedDocumentStorageInMB)
            fixed (char* fixedDatabaseAccountReservedDocumentStorageInMB = databaseAccountReservedDocumentStorageInMB)
            fixed (char* fixedGatewayVersion = gatewayVersion)
            fixed (char* fixedIndexingDirective = indexingDirective)
            fixed (char* fixedItemCount = itemCount)
            fixed (char* fixedLastStateChangeUtc = lastStateChangeUtc)
            fixed (char* fixedMaxMediaStorageUsageInMB = maxMediaStorageUsageInMB)
            fixed (char* fixedMaxResourceQuota = maxResourceQuota)
            fixed (char* fixedNewResourceId = newResourceId)
            fixed (char* fixedOwnerFullName = ownerFullName)
            fixed (char* fixedOwnerId = ownerId)
            fixed (char* fixedRequestCharge = requestCharge)
            fixed (char* fixedRequestValidationFailure = requestValidationFailure)
            fixed (char* fixedRetryAfter = retryAfter)
            fixed (char* fixedRetryAfterInMilliseconds = retryAfterInMilliseconds)
            fixed (char* fixedServerVersion = serverVersion)
            fixed (char* fixedSchemaVersion = schemaVersion)
            fixed (char* fixedSessionToken = sessionToken)
            fixed (char* fixedVersion = version)
            {
                const int eventDataCount = 30;
                const int UnicodeEncodingCharSize = CustomTypeExtensions.UnicodeEncodingCharSize;

                EventData* dataDesc = stackalloc EventData[eventDataCount];
                dataDesc[0].DataPointer = (IntPtr)fixedGuidBytes;
                dataDesc[0].Size = guidBytes.Length;

                dataDesc[1].DataPointer = (IntPtr)fixedLocalIdBytes;
                dataDesc[1].Size = localIdBytes.Length;

                dataDesc[2].DataPointer = (IntPtr)(&statusCode);
                dataDesc[2].Size = 2;

                dataDesc[3].DataPointer = (IntPtr)(&milliseconds);
                dataDesc[3].Size = 8;

                dataDesc[4].DataPointer = (IntPtr)fixedContentType;
                dataDesc[4].Size = (contentType.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[5].DataPointer = (IntPtr)fixedContentEncoding;
                dataDesc[5].Size = (contentEncoding.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[6].DataPointer = (IntPtr)fixedContentLength;
                dataDesc[6].Size = (contentLength.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[7].DataPointer = (IntPtr)fixedContentLocation;
                dataDesc[7].Size = (contentLocation.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[8].DataPointer = (IntPtr)fixedCurrentMediaStorageUsageInMB;
                dataDesc[8].Size = (currentMediaStorageUsageInMB.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[9].DataPointer = (IntPtr)fixedCurrentResourceQuotaUsage;
                dataDesc[9].Size = (currentResourceQuotaUsage.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[10].DataPointer = (IntPtr)fixedDatabaseAccountConsumedDocumentStorageInMB;
                dataDesc[10].Size = (databaseAccountConsumedDocumentStorageInMB.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[11].DataPointer = (IntPtr)fixedDatabaseAccountProvisionedDocumentStorageInMB;
                dataDesc[11].Size = (databaseAccountProvisionedDocumentStorageInMB.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[12].DataPointer = (IntPtr)fixedDatabaseAccountReservedDocumentStorageInMB;
                dataDesc[12].Size = (databaseAccountReservedDocumentStorageInMB.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[13].DataPointer = (IntPtr)fixedGatewayVersion;
                dataDesc[13].Size = (gatewayVersion.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[14].DataPointer = (IntPtr)fixedIndexingDirective;
                dataDesc[14].Size = (indexingDirective.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[15].DataPointer = (IntPtr)fixedItemCount;
                dataDesc[15].Size = (itemCount.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[16].DataPointer = (IntPtr)fixedLastStateChangeUtc;
                dataDesc[16].Size = (lastStateChangeUtc.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[17].DataPointer = (IntPtr)fixedMaxMediaStorageUsageInMB;
                dataDesc[17].Size = (maxMediaStorageUsageInMB.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[18].DataPointer = (IntPtr)fixedMaxResourceQuota;
                dataDesc[18].Size = (maxResourceQuota.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[19].DataPointer = (IntPtr)fixedNewResourceId;
                dataDesc[19].Size = (newResourceId.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[20].DataPointer = (IntPtr)fixedOwnerFullName;
                dataDesc[20].Size = (ownerFullName.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[21].DataPointer = (IntPtr)fixedOwnerId;
                dataDesc[21].Size = (ownerId.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[22].DataPointer = (IntPtr)fixedRequestCharge;
                dataDesc[22].Size = (requestCharge.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[23].DataPointer = (IntPtr)fixedRequestValidationFailure;
                dataDesc[23].Size = (requestValidationFailure.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[24].DataPointer = (IntPtr)fixedRetryAfter;
                dataDesc[24].Size = (retryAfter.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[25].DataPointer = (IntPtr)fixedRetryAfterInMilliseconds;
                dataDesc[25].Size = (retryAfterInMilliseconds.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[26].DataPointer = (IntPtr)fixedServerVersion;
                dataDesc[26].Size = (serverVersion.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[27].DataPointer = (IntPtr)fixedSchemaVersion;
                dataDesc[27].Size = (schemaVersion.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[28].DataPointer = (IntPtr)fixedSessionToken;
                dataDesc[28].Size = (sessionToken.Length + 1) * UnicodeEncodingCharSize;

                dataDesc[29].DataPointer = (IntPtr)fixedVersion;
                dataDesc[29].Size = (version.Length + 1) * UnicodeEncodingCharSize;

                this.WriteEventCoreWithActivityId(activityId, 2, eventDataCount, dataDesc);
            }
        }

        [NonEvent]
        // Marking it as virtual in order to unit test it using Moq framework
        public virtual void Response(Guid activityId, Guid localId, short statusCode, double milliseconds, HttpResponseHeaders responseHeaders)
        {
            if (this.IsEnabled(EventLevel.Verbose, Keywords.HttpRequestAndResponse))
            {
                string[] keysToExtract =
                {
                    HttpConstants.HttpHeaders.ContentType,
                    HttpConstants.HttpHeaders.ContentEncoding,
                    HttpConstants.HttpHeaders.ContentLength,
                    HttpConstants.HttpHeaders.ContentLocation,
                    HttpConstants.HttpHeaders.CurrentMediaStorageUsageInMB,
                    HttpConstants.HttpHeaders.CurrentResourceQuotaUsage,
                    HttpConstants.HttpHeaders.DatabaseAccountConsumedDocumentStorageInMB,
                    HttpConstants.HttpHeaders.DatabaseAccountProvisionedDocumentStorageInMB,
                    HttpConstants.HttpHeaders.DatabaseAccountReservedDocumentStorageInMB,
                    HttpConstants.HttpHeaders.GatewayVersion,
                    HttpConstants.HttpHeaders.IndexingDirective,
                    HttpConstants.HttpHeaders.ItemCount,
                    HttpConstants.HttpHeaders.LastStateChangeUtc,
                    HttpConstants.HttpHeaders.MaxMediaStorageUsageInMB,
                    HttpConstants.HttpHeaders.MaxResourceQuota,
                    HttpConstants.HttpHeaders.NewResourceId,
                    HttpConstants.HttpHeaders.OwnerFullName,
                    HttpConstants.HttpHeaders.OwnerId,
                    HttpConstants.HttpHeaders.RequestCharge,
                    HttpConstants.HttpHeaders.RequestValidationFailure,
                    HttpConstants.HttpHeaders.RetryAfter,
                    HttpConstants.HttpHeaders.RetryAfterInMilliseconds,
                    HttpConstants.HttpHeaders.ServerVersion,
                    HttpConstants.HttpHeaders.SchemaVersion,
                    HttpConstants.HttpHeaders.SessionToken,
                    HttpConstants.HttpHeaders.Version
                };

                string[] headerValues = Helpers.ExtractValuesFromHTTPHeaders(responseHeaders, keysToExtract);
                this.Response(activityId, localId, statusCode, milliseconds, headerValues[0], headerValues[1], headerValues[2],
                    headerValues[3], headerValues[4], headerValues[5], headerValues[6], headerValues[7], headerValues[8],
                    headerValues[9], headerValues[10], headerValues[11], headerValues[12], headerValues[13], headerValues[14],
                    headerValues[15], headerValues[16], headerValues[17], headerValues[18], headerValues[19], headerValues[20],
                    headerValues[21], headerValues[22], headerValues[23], headerValues[24], headerValues[25]);
            }
        }
    }
}
