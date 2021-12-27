//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Telemetry.Payloads;

    internal class ClientTelemetryCollector : IObserver<KeyValuePair<string, object>>
    {
        public void OnCompleted()
        {
            Console.WriteLine("collection recorded");
            //throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            Console.WriteLine("collection recorded with error " + error.Message);
           // throw new NotImplementedException();
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            Console.WriteLine("Request Listestener => " + value.Key);

            if (value.Key.Equals(ClientTelemetryOptions.RequestPayloadKey))
            {
                RequestPayload payload = (RequestPayload)value.Value;

                Console.WriteLine("Request Listestener, payload id: " + payload.Id);
 
                ClientTelemetry.Collect(payload.cosmosDiagnostics,
                    payload.statusCode,
                    payload.responseSizeInBytes,
                    payload.containerId,
                    payload.databaseId,
                    payload.operationType,
                    payload.resourceType,
                    payload.consistencyLevel,
                    payload.requestCharge);
            }
        }
    }
}
