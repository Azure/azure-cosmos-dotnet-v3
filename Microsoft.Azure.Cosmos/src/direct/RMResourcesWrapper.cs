//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Wrapper around RMResources that logs any access failures for diagnostics.
    /// Re-throws exceptions so they propagate normally.
    /// </summary>
    internal static class RMResourcesWrapper
    {
        public static string GetResource(string resourceName)
        {
            var caller = new StackTrace(true).GetFrame(1)?.GetMethod();
            var callerInfo = caller != null
                ? $"{caller.DeclaringType?.FullName}.{caller.Name}"
                : "Unknown";
            DefaultTrace.TraceInformation("RMResourcesWrapper.GetResource('{0}') called by {1}", resourceName, callerInfo);
            try
            {
                var property = typeof(RMResources).GetProperty(resourceName, BindingFlags.Static | BindingFlags.NonPublic);
                if (property == null)
                {
                    DefaultTrace.TraceInformation($"RMResourcesWrapper: Resource property '{resourceName}' not found on RMResources type.");
                    throw new MissingFieldException($"RMResources.{resourceName}");
                }
                return (string)property.GetValue(null);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceInformation($"RMResourcesWrapper.GetResource('{resourceName}'): {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }
    }
}