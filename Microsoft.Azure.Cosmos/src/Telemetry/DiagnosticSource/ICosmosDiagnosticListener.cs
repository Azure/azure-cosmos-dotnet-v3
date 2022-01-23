//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.DiagnosticSource
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Diagnostics;

    /// <summary>
    /// ICosmosDiagnosticListener
    /// </summary>
    public interface ICosmosDiagnosticListener
    {
        /// <summary>
        /// Use default Filter type DiagnosticSourceFilterType
        /// </summary>
        public DiagnosticSourceFilterType? DefaultFilter { get; }

        /// <summary>
        /// Listener
        /// </summary>
        public IObserver<KeyValuePair<string, object>> Listener { get; }

        /// <summary>
        /// User defined filter technique 
        /// </summary>
        public Func<CosmosDiagnostics, bool> Filter { get; }
    }
}
