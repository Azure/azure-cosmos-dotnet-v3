//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Text;

    internal interface ICosmosDiagnosticWriter
    {
        void WriteJsonObject(StringBuilder stringBuilder);
    }
}
