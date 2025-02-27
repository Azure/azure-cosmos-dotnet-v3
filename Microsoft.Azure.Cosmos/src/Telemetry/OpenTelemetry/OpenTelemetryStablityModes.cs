// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    /// <summary>
    /// For More information, Ref https://github.com/open-telemetry/semantic-conventions/blob/main/docs/database/database-spans.md#semantic-conventions-for-database-client-calls
    /// </summary>
    internal sealed class OpenTelemetryStablityModes
    {
        /// <summary>
        /// emit the new, stable database conventions, and stop emitting the old experimental database conventions that the instrumentation emitted previously.
        /// </summary>
        public const string Database = "database";

        /// <summary>
        ///  emit both the old and the stable database conventions, allowing for a seamless transition.
        /// </summary>
        public const string DatabaseDupe = "database/dup";

        /// <summary>
        /// Environment Variable to support the classic AppInsight conventions
        /// </summary>
        public const string ClassicAppInsights = "appinsightssdk";
    }
}
