//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class TelemetryRequestInfo
    {
        internal String ContainerId { get; set; }
        internal String DatabaseId { get; set; }
    }
}
