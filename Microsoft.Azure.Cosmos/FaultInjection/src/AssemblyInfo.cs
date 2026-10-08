//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.CompilerServices;
#if COSMOS_DIRECT_SOURCE
using Microsoft.Azure.Cosmos.Direct;
#endif

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2" + AssemblyKeys.MoqPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.FaultInjection.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.FaultInjection.Tests" + AssemblyKeys.TestPublicKey)]
