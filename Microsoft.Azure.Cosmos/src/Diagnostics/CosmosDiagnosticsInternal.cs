//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System.IO;
    using System.Text;

    /// <summary>
    /// Extends <see cref="CosmosDiagnostics"/> to expose internal APIs.
    /// </summary>
    internal abstract class CosmosDiagnosticsInternal : CosmosDiagnostics
    {
        public abstract void Accept(CosmosDiagnosticsInternalVisitor visitor);

        public abstract TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor);

        public override string ToString()
        {
            using (StringWriter stringWriter = new StringWriter())
            {
                this.WriteTo(stringWriter);
                return stringWriter.ToString();
            }
        }

        public void WriteTo(TextWriter textWriter)
        {
            CosmosDiagnosticsSerializerVisitor cosmosDiagnosticsSerializerVisitor = new CosmosDiagnosticsSerializerVisitor(textWriter);
            this.Accept(cosmosDiagnosticsSerializerVisitor);
        }
    }
}
