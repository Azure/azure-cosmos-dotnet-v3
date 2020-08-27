//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2" + AssemblyKeys.MoqPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Extensions" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Extensions" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Extensions.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Extensions.Tests" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends.Tests" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table.Tests" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Encryption.KeyVault" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Encryption.KeyVault" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Encryption" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Encryption" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Performance.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Performance.Tests" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.EmulatorTests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.EmulatorTests" + AssemblyKeys.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.NetFramework.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.NetFramework.Tests" + AssemblyKeys.TestPublicKey)]

internal static class AssemblyKeys
{
    /// <summary>ProductPublicKey is an official MS supported public key for external releases. TestPublicKey is an unsupported strong key for testing and internal use only</summary>
    internal const string ProductPublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9";
    internal const string TestPublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100197c25d0a04f73cb271e8181dba1c0c713df8deebb25864541a66670500f34896d280484b45fe1ff6c29f2ee7aa175d8bcbd0c83cc23901a894a86996030f6292ce6eda6e6f3e6c74b3c5a3ded4903c951e6747e6102969503360f7781bf8bf015058eb89b7621798ccc85aaca036ff1bc1556bb7f62de15908484886aa8bbae";

    // check https://github.com/Moq/moq4/wiki/Quickstart
    internal const string MoqPublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7";
    // check https://msdn.microsoft.com/en-us/library/hh708916.aspx
    internal const string FakesPublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100e92decb949446f688ab9f6973436c535bf50acd1fd580495aae3f875aa4e4f663ca77908c63b7f0996977cb98fcfdb35e05aa2c842002703cad835473caac5ef14107e3a7fae01120a96558785f48319f66daabc862872b2c53f5ac11fa335c0165e202b4c011334c7bc8f4c4e570cf255190f4e3e2cbc9137ca57cb687947bc";
}