// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos.Diagnostics
{
    using Microsoft.Azure.Documents;

    internal static class DiagnosticProperty
    {
        public const string BaseActivityName = "Azure.Cosmos";
        public const string ResourceUri = "resourceUri";
        public const string OperationType = "operationType";
        public const string ResourceType = "resourceType";
        public const string Container = "container";
        public const string Diagnostics = "diagnostics";

        public static string ResourceOperationActivityName(ResourceType resourceType, OperationType operationType) => $"{DiagnosticProperty.BaseActivityName}-{operationType}-{resourceType}";
    }
}
