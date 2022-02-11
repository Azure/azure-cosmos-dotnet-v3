//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using Documents;

    internal class DiagnosticAttributes
    {
        internal string ContainerId { get; set; }

        internal string DatabaseId { get; set; }

        internal double RequestCharge { get; set; }

        internal Uri AccountName { get; set; }

        internal string UserAgent { get; set; }

        internal HttpStatusCode StatusCode { get; set; }

        internal OperationType OperationType { get; set; }
    }
}
