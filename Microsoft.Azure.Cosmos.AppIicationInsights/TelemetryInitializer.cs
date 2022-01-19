//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ApplicationInsights
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;

    public class TelemetryInitializer
    {
        public static TelemetryListener Initialize(string instrumentKey)
        {
            TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
            configuration.InstrumentationKey = instrumentKey;

            return new TelemetryListener(new TelemetryClient(configuration));
        }
    }
}
