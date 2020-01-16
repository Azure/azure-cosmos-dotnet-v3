//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Text;
    using Newtonsoft.Json;

    internal abstract class CosmosDiagnosticWriter
    {
        internal abstract void WriteJsonObject(JsonWriter jsonWriter);
    }
}
