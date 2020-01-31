//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal sealed class StoreResponseStatistics : CosmosDiagnosticsInternal
    {
        public readonly DateTime RequestResponseTime;
        public readonly StoreResult StoreResult;
        public readonly ResourceType RequestResourceType;
        public readonly OperationType RequestOperationType;
        public readonly Uri LocationEndpoint;
        public readonly bool IsSupplementalResponse;

        public StoreResponseStatistics(
            DateTime requestResponseTime,
            StoreResult storeResult,
            ResourceType resourceType,
            OperationType operationType,
            Uri locationEndpoint)
        {
            this.RequestResponseTime = requestResponseTime;
            this.StoreResult = storeResult;
            this.RequestResourceType = resourceType;
            this.RequestOperationType = operationType;
            this.LocationEndpoint = locationEndpoint;
            this.IsSupplementalResponse = operationType == OperationType.Head || operationType == OperationType.HeadFeed;
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

            stringBuilder.Append($"ResponseTime: {this.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture)}, ");

            stringBuilder.Append("StoreResult: ");
            if (this.StoreResult != null)
            {
                this.StoreResult.AppendToBuilder(stringBuilder);
            }

            stringBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                ", ResourceType: {0}, OperationType: {1}",
                this.RequestResourceType,
                this.RequestOperationType);
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
