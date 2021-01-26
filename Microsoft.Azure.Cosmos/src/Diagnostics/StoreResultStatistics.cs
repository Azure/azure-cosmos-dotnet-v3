//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal sealed class StoreResultStatistics
    {
        public DocumentClientException Exception { get; }

        public long LSN { get; }

        public string PartitionKeyRangeId { get; }

        public long GlobalCommittedLSN { get; }

        public long ItemLSN { get; }

        public ISessionToken SessionToken { get; }

        public bool UsingLocalLSN { get; }

        public bool IsValid { get; }

        public Uri StorePhysicalAddress { get; }

        public StatusCodes StatusCode { get; }

        public SubStatusCodes SubStatusCode { get; }

        public string ActivityId { get; }

        public double RequestCharge { get; }

        public StoreResultStatistics(
            DocumentClientException exception,
            StatusCodes statusCode,
            SubStatusCodes subStatusCode,
            string partitionKeyRangeId,
            long lsn,
            double requestCharge,
            bool isValid,
            Uri storePhysicalAddress,
            long globalCommittedLSN,
            long itemLSN,
            ISessionToken sessionToken,
            bool usingLocalLSN,
            string activityId)
        {
            this.Exception = exception;
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.PartitionKeyRangeId = partitionKeyRangeId;
            this.LSN = lsn;
            this.RequestCharge = requestCharge;
            this.IsValid = isValid;
            this.StorePhysicalAddress = storePhysicalAddress;
            this.GlobalCommittedLSN = globalCommittedLSN;
            this.ItemLSN = itemLSN;
            this.SessionToken = sessionToken;
            this.UsingLocalLSN = usingLocalLSN;
            this.ActivityId = activityId;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                "StorePhysicalAddress: {0}, LSN: {1}, GlobalCommittedLsn: {2}, PartitionKeyRangeId: {3}, IsValid: {4}, StatusCode: {5}, SubStatusCode: {6}, " +
                "RequestCharge: {7}, ItemLSN: {8}, SessionToken: {9}, UsingLocalLSN: {10}, TransportException: {11}",
                this.StorePhysicalAddress,
                this.LSN,
                this.GlobalCommittedLSN,
                this.PartitionKeyRangeId,
                this.IsValid,
                (int)this.StatusCode,
                (int)this.SubStatusCode,
                this.RequestCharge,
                this.ItemLSN,
                this.SessionToken?.ConvertToString(),
                this.UsingLocalLSN,
                this.Exception?.InnerException is TransportException ? this.Exception.InnerException.Message : "null");

            return stringBuilder.ToString();
        }

    }
}