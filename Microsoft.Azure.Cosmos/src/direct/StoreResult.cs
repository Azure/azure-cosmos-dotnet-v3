//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Routing;

    internal sealed class StoreResult
    {
        private readonly StoreResponse storeResponse;

        private static bool UseSessionTokenHeader = VersionUtility.IsLaterThan(HttpConstants.Versions.CurrentVersion, HttpConstants.VersionDates.v2018_06_18);

        public static StoreResult CreateStoreResult(StoreResponse storeResponse, Exception responseException, bool requiresValidLsn, bool useLocalLSNBasedHeaders, Uri storePhysicalAddress = null)
        {
            if (storeResponse == null && responseException == null)
            {
                throw new ArgumentException("storeResponse or responseException must be populated.");
            }

            if (responseException == null)
            {
                string headerValue = null;
                long quorumAckedLSN = -1;
                int currentReplicaSetSize = -1;
                int currentWriteQuorum = -1;
                long globalCommittedLSN = -1;
                int numberOfReadRegions = -1;
                long itemLSN = -1;
                if (storeResponse.TryGetHeaderValue(
                    useLocalLSNBasedHeaders ? WFConstants.BackendHeaders.QuorumAckedLocalLSN : WFConstants.BackendHeaders.QuorumAckedLSN,
                    out headerValue))
                {
                    quorumAckedLSN = long.Parse(headerValue, CultureInfo.InvariantCulture);
                }

                if (storeResponse.TryGetHeaderValue(WFConstants.BackendHeaders.CurrentReplicaSetSize, out headerValue))
                {
                    currentReplicaSetSize = int.Parse(headerValue, CultureInfo.InvariantCulture);
                }

                if (storeResponse.TryGetHeaderValue(WFConstants.BackendHeaders.CurrentWriteQuorum, out headerValue))
                {
                    currentWriteQuorum = int.Parse(headerValue, CultureInfo.InvariantCulture);
                }

                double requestCharge = 0;
                if (storeResponse.TryGetHeaderValue(HttpConstants.HttpHeaders.RequestCharge, out headerValue))
                {
                    requestCharge = double.Parse(headerValue, CultureInfo.InvariantCulture);
                }

                if (storeResponse.TryGetHeaderValue(WFConstants.BackendHeaders.NumberOfReadRegions, out headerValue))
                {
                    numberOfReadRegions = int.Parse(headerValue, CultureInfo.InvariantCulture);
                }

                if (storeResponse.TryGetHeaderValue(WFConstants.BackendHeaders.GlobalCommittedLSN, out headerValue))
                {
                    globalCommittedLSN = long.Parse(headerValue, CultureInfo.InvariantCulture);
                }

                if (storeResponse.TryGetHeaderValue(
                    useLocalLSNBasedHeaders ? WFConstants.BackendHeaders.ItemLocalLSN : WFConstants.BackendHeaders.ItemLSN,
                    out headerValue))
                {
                    itemLSN = long.Parse(headerValue, CultureInfo.InvariantCulture);
                }

                long lsn = -1;
                if (useLocalLSNBasedHeaders)
                {
                    if (storeResponse.TryGetHeaderValue(WFConstants.BackendHeaders.LocalLSN, out headerValue))
                    {
                        lsn = long.Parse(headerValue, CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    lsn = storeResponse.LSN;
                }

                ISessionToken sessionToken = null;
                if (StoreResult.UseSessionTokenHeader)
                {
                    // Session token response header is introduced from version HttpConstants.Versions.v2018_06_18 onwards.
                    // Previously it was only a request header
                    if (storeResponse.TryGetHeaderValue(HttpConstants.HttpHeaders.SessionToken, out headerValue))
                    {
                        sessionToken = SessionTokenHelper.Parse(headerValue);
                    }
                }
                else
                {
                    sessionToken = new SimpleSessionToken(storeResponse.LSN);
                }

                storeResponse.TryGetHeaderValue(HttpConstants.HttpHeaders.ActivityId, out string activityId);

                return new StoreResult(
                    storeResponse: storeResponse,
                    exception: null,
                    partitionKeyRangeId: storeResponse.PartitionKeyRangeId,
                    lsn: lsn,
                    quorumAckedLsn: quorumAckedLSN,
                    requestCharge: requestCharge,
                    currentReplicaSetSize: currentReplicaSetSize,
                    currentWriteQuorum: currentWriteQuorum,
                    isValid: true,
                    storePhysicalAddress: storePhysicalAddress,
                    globalCommittedLSN: globalCommittedLSN,
                    numberOfReadRegions: numberOfReadRegions,
                    itemLSN: itemLSN,
                    sessionToken: sessionToken,
                    usingLocalLSN: useLocalLSNBasedHeaders,
                    activityId: activityId);
            }
            else
            {
                DocumentClientException documentClientException = responseException as DocumentClientException;
                if (documentClientException != null)
                {
                    StoreResult.VerifyCanContinueOnException(documentClientException);
                    long quorumAckedLSN = -1;
                    int currentReplicaSetSize = -1;
                    int currentWriteQuorum = -1;
                    long globalCommittedLSN = -1;
                    int numberOfReadRegions = -1;
                    string headerValue = documentClientException.Headers[useLocalLSNBasedHeaders ? WFConstants.BackendHeaders.QuorumAckedLocalLSN : WFConstants.BackendHeaders.QuorumAckedLSN];
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        quorumAckedLSN = long.Parse(headerValue, CultureInfo.InvariantCulture);
                    }

                    headerValue = documentClientException.Headers[WFConstants.BackendHeaders.CurrentReplicaSetSize];
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        currentReplicaSetSize = int.Parse(headerValue, CultureInfo.InvariantCulture);
                    }

                    headerValue = documentClientException.Headers[WFConstants.BackendHeaders.CurrentWriteQuorum];
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        currentWriteQuorum = int.Parse(headerValue, CultureInfo.InvariantCulture);
                    }

                    double requestCharge = 0;
                    headerValue = documentClientException.Headers[HttpConstants.HttpHeaders.RequestCharge];
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        requestCharge = double.Parse(headerValue, CultureInfo.InvariantCulture);
                    }

                    headerValue = documentClientException.Headers[WFConstants.BackendHeaders.NumberOfReadRegions];
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        numberOfReadRegions = int.Parse(headerValue, CultureInfo.InvariantCulture);
                    }

                    headerValue = documentClientException.Headers[WFConstants.BackendHeaders.GlobalCommittedLSN];
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        globalCommittedLSN = long.Parse(headerValue, CultureInfo.InvariantCulture);
                    }

                    long lsn = -1;
                    if (useLocalLSNBasedHeaders)
                    {
                        headerValue = documentClientException.Headers[WFConstants.BackendHeaders.LocalLSN];
                        if (!string.IsNullOrEmpty(headerValue))
                        {
                            lsn = long.Parse(headerValue, CultureInfo.InvariantCulture);
                        }
                    }
                    else
                    {
                        lsn = documentClientException.LSN;
                    }

                    ISessionToken sessionToken = null;
                    if (StoreResult.UseSessionTokenHeader)
                    {
                        // Session token response header is introduced from version HttpConstants.Versions.v2018_06_18 onwards.
                        // Previously it was only a request header
                        headerValue = documentClientException.Headers[HttpConstants.HttpHeaders.SessionToken];
                        if (!string.IsNullOrEmpty(headerValue))
                        {
                            sessionToken = SessionTokenHelper.Parse(headerValue);
                        }
                    }
                    else
                    {
                        sessionToken = new SimpleSessionToken(documentClientException.LSN);
                    }

                    return new StoreResult(
                        storeResponse: null,
                        exception: documentClientException,
                        partitionKeyRangeId: documentClientException.PartitionKeyRangeId,
                        lsn: lsn,
                        quorumAckedLsn: quorumAckedLSN,
                        requestCharge: requestCharge,
                        currentReplicaSetSize: currentReplicaSetSize,
                        currentWriteQuorum: currentWriteQuorum,
                        isValid: !requiresValidLsn
                            || ((documentClientException.StatusCode != HttpStatusCode.Gone || documentClientException.GetSubStatus() == SubStatusCodes.NameCacheIsStale)
                            && lsn >= 0),
                        storePhysicalAddress: storePhysicalAddress == null ? documentClientException.RequestUri : storePhysicalAddress,
                        globalCommittedLSN: globalCommittedLSN,
                        numberOfReadRegions: numberOfReadRegions,
                        itemLSN: -1,
                        sessionToken: sessionToken,
                        usingLocalLSN: useLocalLSNBasedHeaders,
                        activityId: documentClientException.ActivityId);
                }
                else
                {
                    DefaultTrace.TraceCritical("Unexpected exception {0} received while reading from store.", responseException);
                    return new StoreResult(
                        storeResponse: null,
                        exception: new InternalServerErrorException(RMResources.InternalServerError, responseException),
                        partitionKeyRangeId: null,
                        lsn: -1,
                        quorumAckedLsn: -1,
                        requestCharge: 0,
                        currentReplicaSetSize: 0,
                        currentWriteQuorum: 0,
                        isValid: false,
                        storePhysicalAddress: storePhysicalAddress,
                        globalCommittedLSN: -1,
                        numberOfReadRegions: 0,
                        itemLSN: -1,
                        sessionToken: null,
                        usingLocalLSN: useLocalLSNBasedHeaders,
                        activityId: null);
                }
            }
        }

        public StoreResult(
            StoreResponse storeResponse,
            DocumentClientException exception,
            string partitionKeyRangeId,
            long lsn,
            long quorumAckedLsn,
            double requestCharge,
            int currentReplicaSetSize,
            int currentWriteQuorum,
            bool isValid,
            Uri storePhysicalAddress,
            long globalCommittedLSN,
            int numberOfReadRegions,
            long itemLSN,
            ISessionToken sessionToken,
            bool usingLocalLSN,
            string activityId)
        {
            if (storeResponse == null && exception == null)
            {
                throw new ArgumentException("storeResponse or responseException must be populated.");
            }

            this.storeResponse = storeResponse;
            this.Exception = exception;
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.LSN = lsn;
            this.QuorumAckedLSN = quorumAckedLsn;
            this.RequestCharge = requestCharge;
            this.CurrentReplicaSetSize = currentReplicaSetSize;
            this.CurrentWriteQuorum = currentWriteQuorum;
            this.IsValid = isValid;

            this.StorePhysicalAddress = storePhysicalAddress;
            this.GlobalCommittedLSN = globalCommittedLSN;
            this.NumberOfReadRegions = numberOfReadRegions;
            this.ItemLSN = itemLSN;
            this.SessionToken = sessionToken;
            this.UsingLocalLSN = usingLocalLSN;
            this.ActivityId = activityId;

            this.StatusCode = (StatusCodes) (this.storeResponse != null ? this.storeResponse.StatusCode :
                ((this.Exception != null && this.Exception.StatusCode.HasValue) ? this.Exception.StatusCode : 0));
            
            this.SubStatusCode = this.storeResponse != null ? this.storeResponse.SubStatusCode :
                (this.Exception != null ? this.Exception.GetSubStatus() : SubStatusCodes.Unknown);
        }

        public DocumentClientException Exception { get; }

        public long LSN { get; private set; }

        public string PartitionKeyRangeId { get; private set; }

        public long QuorumAckedLSN { get; private set; }

        public long GlobalCommittedLSN { get; private set; }

        public long NumberOfReadRegions { get; private set; }

        public long ItemLSN { get; private set; }

        public ISessionToken SessionToken { get; private set; }

        public bool UsingLocalLSN { get; private set; }

        public double RequestCharge { get; private set; }

        public int CurrentReplicaSetSize { get; private set; }

        public int CurrentWriteQuorum { get; private set; }

        public bool IsValid { get; private set; }
        
        public Uri StorePhysicalAddress { get; private set; }

        public StatusCodes StatusCode { get; private set; }

        public SubStatusCodes SubStatusCode { get; private set; }

        public string ActivityId { get; private set; }

        public bool IsClientCpuOverloaded
        {
            get
            {
                TransportException transportException = this.Exception?.InnerException as TransportException;
                if (transportException == null)
                {
                    return false;
                }
                return transportException.IsClientCpuOverloaded;
            }
        }

        public DocumentClientException GetException()
        {
            if (this.Exception == null)
            {
                string message = "Exception should be available but found none";
                Debug.Assert(false, message);
                DefaultTrace.TraceCritical(message);
                throw new InternalServerErrorException(RMResources.InternalServerError);
            }

            return this.Exception;
        }

        public StoreResponse ToResponse(RequestChargeTracker requestChargeTracker = null)
        {
            if (!this.IsValid)
            {
                if (this.Exception == null)
                {
                    DefaultTrace.TraceCritical("Exception not set for invalid response");
                    throw new InternalServerErrorException(RMResources.InternalServerError);
                }

                throw this.Exception;
            }

            if (requestChargeTracker != null)
            {
                StoreResult.SetRequestCharge(this.storeResponse, this.Exception, requestChargeTracker.TotalRequestCharge);
            }

            if (this.Exception != null)
            {
                throw Exception;
            }

            return this.storeResponse;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            this.AppendToBuilder(stringBuilder);
            return stringBuilder.ToString();
        }

        public void AppendToBuilder(StringBuilder stringBuilder)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException(nameof(stringBuilder));
            }
            
            stringBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                "StorePhysicalAddress: {0}, LSN: {1}, GlobalCommittedLsn: {2}, PartitionKeyRangeId: {3}, IsValid: {4}, StatusCode: {5}, SubStatusCode: {6}, " +
                "RequestCharge: {7}, ItemLSN: {8}, SessionToken: {9}, UsingLocalLSN: {10}, TransportException: {11}",
                this.StorePhysicalAddress,
                this.LSN,
                this.GlobalCommittedLSN,
                this.PartitionKeyRangeId,
                this.IsValid,
                (int) this.StatusCode,
                (int) this.SubStatusCode,
                this.RequestCharge,
                this.ItemLSN,
                this.SessionToken?.ConvertToString(),
                this.UsingLocalLSN,
                this.Exception?.InnerException is TransportException ? this.Exception.InnerException.Message : "null");
        }

        private static void SetRequestCharge(StoreResponse response, DocumentClientException documentClientException, double totalRequestCharge)
        {
            if (documentClientException != null)
            {
                documentClientException.Headers[HttpConstants.HttpHeaders.RequestCharge] = totalRequestCharge.ToString(CultureInfo.InvariantCulture);
            }
            // Set total charge as final charge for the response.
            else if (response.Headers?.Get(HttpConstants.HttpHeaders.RequestCharge) != null)
            {
                response.Headers[HttpConstants.HttpHeaders.RequestCharge] = totalRequestCharge.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void VerifyCanContinueOnException(DocumentClientException ex)
        {
            if ((ex is PartitionKeyRangeGoneException) ||
                (ex is PartitionKeyRangeIsSplittingException) ||
                (ex is PartitionIsMigratingException))
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }

            string value = ex.Headers[HttpConstants.HttpHeaders.RequestValidationFailure];
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            int result;
            if (int.TryParse(ex.Headers.GetValues(HttpConstants.HttpHeaders.RequestValidationFailure)[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result == 1)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
        } 
    }

}
