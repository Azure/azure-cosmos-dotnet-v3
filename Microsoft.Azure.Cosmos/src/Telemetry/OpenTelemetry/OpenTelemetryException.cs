//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    
    internal class OpenTelemetryException : OpenTelemetryAttributes
    {
        internal Exception OriginalException { get; set; }
    }
}
