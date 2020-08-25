//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;

    /// <summary>
    /// Type resolver based on an input
    /// </summary>
    /// <typeparam name="T">Type T for which the resolver returns correct object of T in T's hierarchy</typeparam>
    internal interface ITypeResolver<T> where T : JsonSerializable
    {
        /// <summary>
        /// Returns a reference of an object in T's hierarchy based on a property bag.
        /// </summary>
        /// <param name="propertyBag">Property bag used to deserialize T</param>
        /// <returns>Object of type T</returns>
        T Resolve(JObject propertyBag);
    }
}
