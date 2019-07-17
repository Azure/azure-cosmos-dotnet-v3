//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    /// <summary>
    /// Interface that describes a Node within a JSON document in a <see cref="IJsonNavigator"/>
    /// </summary>
#if INTERNAL
    public interface IJsonNavigatorNode
#else
    internal interface IJsonNavigatorNode
#endif
    {
    }
}
