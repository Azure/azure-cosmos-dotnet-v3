namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal interface ICosmosInstrumentation : IDisposable
    {
        public void MarkFailed(Exception ex);
    }
}
