//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class InternalWrapUnwrapRequest
    {
        public InternalWrapUnwrapRequest(string Alg, string Value)
        {
            this.Alg = Alg;
            this.Value = Value;
        }
        public string Alg { get;}

        public string Value { get;}
                
    }
}