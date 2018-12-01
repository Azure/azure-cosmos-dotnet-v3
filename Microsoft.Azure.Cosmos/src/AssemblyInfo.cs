//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.CompilerServices;

#if !SignAssembly
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.EmulatorTests")]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends")]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.NetFramework.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2" + AssemblyRef.MoqPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests" + AssemblyRef.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.EmulatorTests" + AssemblyRef.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends.Tests" + AssemblyRef.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends" + AssemblyRef.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.NetFramework.Tests" + AssemblyRef.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Tests" + AssemblyRef.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.EmulatorTests" + AssemblyRef.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends.Tests" + AssemblyRef.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Friends" + AssemblyRef.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.NetFramework.Tests" + AssemblyRef.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table" + AssemblyRef.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table" + AssemblyRef.TestPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table.Tests" + AssemblyRef.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.Table.Tests" + AssemblyRef.TestPublicKey)]
