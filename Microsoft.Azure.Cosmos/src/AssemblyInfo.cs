//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.CompilerServices;

#if SignAssembly
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

[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Performance.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.EmulatorTests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.NetFramework.Tests" + AssemblyKeys.ProductPublicKey)]
#else
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.EmulatorTests")]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.NetFramework.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Performance.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif