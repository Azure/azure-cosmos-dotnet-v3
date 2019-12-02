//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Text;

    internal interface ICosmosDiagnosticsJsonWriter
    {
        void AppendJson(StringBuilder stringBuilder);
    }
}
