// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using Telemetry;

    internal sealed class OpenTelemetryResponse : OpenTelemetryAttributes
    {
        internal OpenTelemetryResponse(TransactionalBatchResponse responseMessage)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: null,
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  requestMessage: null,
                  subStatusCode: (int)responseMessage.Headers?.SubStatusCode,
                  activityId: responseMessage.Headers?.ActivityId,
                  correlationId: responseMessage.Headers?.CorrelatedActivityId,
                  batchOperations: responseMessage.Operations)
        {
        }

        internal OpenTelemetryResponse(ResponseMessage responseMessage)
           : this(
                  statusCode: responseMessage.StatusCode,
                  requestCharge: responseMessage.Headers?.RequestCharge,
                  responseContentLength: OpenTelemetryResponse.GetPayloadSize(responseMessage),
                  diagnostics: responseMessage.Diagnostics,
                  itemCount: responseMessage.Headers?.ItemCount,
                  requestMessage: responseMessage.RequestMessage,
                  subStatusCode: (int)responseMessage.Headers?.SubStatusCode,
                  activityId: responseMessage.Headers?.ActivityId,
                  correlationId: responseMessage.Headers?.CorrelatedActivityId,
                  operationType: responseMessage is QueryResponse ? Documents.OperationType.Query : Documents.OperationType.Invalid
                 )
        {
        }

        private OpenTelemetryResponse(
            HttpStatusCode statusCode, 
            double? requestCharge,
            string responseContentLength,
            CosmosDiagnostics diagnostics,
            string itemCount,
            RequestMessage requestMessage,
            int subStatusCode,
            string activityId,
            string correlationId,
            Documents.OperationType operationType = Documents.OperationType.Invalid,
            IReadOnlyList<ItemBatchOperation> batchOperations = null)
            : base(requestMessage)
        {
            this.StatusCode = statusCode;
            this.RequestCharge = requestCharge;
            this.ResponseContentLength = responseContentLength;
            this.Diagnostics = diagnostics;
            this.ItemCount = itemCount; 
            this.SubStatusCode = subStatusCode;
            this.ActivityId = activityId;
            this.CorrelatedActivityId = correlationId;
            this.OperationType = operationType;
            if (batchOperations != null) 
            {
                IDictionary<string, int> operationTypeCount = new Dictionary<string, int>();
                foreach (ItemBatchOperation operation in batchOperations)
                {
                    string operationTypeAsString = operation.OperationType.ToString();
                    if (operationTypeCount.ContainsKey(operationTypeAsString))
                    {
                        operationTypeCount[operationTypeAsString]++;
                    }
                    else
                    {
                        operationTypeCount.Add(operationTypeAsString, 1);
                    }
                }
                this.BatchOperations = string.Join(", ", operationTypeCount.Select(pair => $"{pair.Key} : {pair.Value}"));
            }
        }

        private static string GetPayloadSize(ResponseMessage response)
        {
            if (response?.Content != null
                    && response.Content.CanSeek
                    && response.Content is MemoryStream)
            {
                return response.Content.Length.ToString();
            }
            return response?.Headers?.ContentLength;
        }
    }
}
