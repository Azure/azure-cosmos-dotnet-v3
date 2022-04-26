namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal enum ClientTelemetryJobState
    {
        INITIALIZED, 
        RUNNING, 
        STOPPED
    }
}
