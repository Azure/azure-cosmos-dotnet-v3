//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class InternalWrapUnwrapResponse
    {
        public InternalWrapUnwrapResponse(string Kid, string Value)
        {
            this.Kid = Kid;
            this.Value = Value;
        }
        public string Kid { get; }
        public string Value { get; }

    }
}