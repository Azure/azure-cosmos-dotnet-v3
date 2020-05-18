//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Encryption.Tests" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Encryption.Tests" + AssemblyKeys.ProductPublicKey)]

internal static class AssemblyKeys
{
    /// <summary>ProductPublicKey is an official MS supported public key for external releases. TestPublicKey is an unsupported strong key for testing and internal use only</summary>
    internal const string ProductPublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9";
    internal const string TestPublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100197c25d0a04f73cb271e8181dba1c0c713df8deebb25864541a66670500f34896d280484b45fe1ff6c29f2ee7aa175d8bcbd0c83cc23901a894a86996030f6292ce6eda6e6f3e6c74b3c5a3ded4903c951e6747e6102969503360f7781bf8bf015058eb89b7621798ccc85aaca036ff1bc1556bb7f62de15908484886aa8bbae";
}