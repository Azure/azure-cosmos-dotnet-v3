//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos.FaultInjection;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2" + AssemblyKeys.MoqPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.FaultInjection.Tests" + AssemblyKeys.ProductPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Azure.Cosmos.FaultInjection.Tests" + AssemblyKeys.TestPublicKey)]
