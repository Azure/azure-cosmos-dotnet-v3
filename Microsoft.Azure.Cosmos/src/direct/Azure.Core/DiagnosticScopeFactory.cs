// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// This File is copied from Azure.Core repo. i.e. https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/src/Shared/DiagnosticScopeFactory.cs

#nullable enable
#if NETSTANDARD2_0_OR_GREATER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Azure.Core
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    internal class DiagnosticScopeFactory
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private static Dictionary<string, DiagnosticListener>? _listeners;
        private readonly string? _resourceProviderNamespace;
        private readonly DiagnosticListener? _source;
        private readonly bool _suppressNestedClientActivities;

        private static readonly ConcurrentDictionary<string, object?> ActivitySources = new();

        public DiagnosticScopeFactory(string clientNamespace, string? resourceProviderNamespace, bool isActivityEnabled, bool suppressNestedClientActivities)
        {
            _resourceProviderNamespace = resourceProviderNamespace;
            IsActivityEnabled = isActivityEnabled;
            _suppressNestedClientActivities = suppressNestedClientActivities;

            if (IsActivityEnabled)
            {
                var listeners = LazyInitializer.EnsureInitialized(ref _listeners);

                lock (listeners!)
                {
                    if (!listeners.TryGetValue(clientNamespace, out _source))
                    {
                        _source = new DiagnosticListener(clientNamespace);
                        listeners[clientNamespace] = _source;
                    }
                }
            }
        }

        public bool IsActivityEnabled { get; }

        public DiagnosticScope CreateScope(string name, DiagnosticScope.ActivityKind kind = DiagnosticScope.ActivityKind.Internal)
        {
            if (_source == null)
            {
                return default;
            }

            var scope = new DiagnosticScope(
                                scopeName: name,
                                source: _source,
                                diagnosticSourceArgs: null,
                                activitySource: GetActivitySource(_source.Name, name),
                                kind: kind,
                                suppressNestedClientActivities: _suppressNestedClientActivities);

            if (_resourceProviderNamespace != null)
            {
                scope.AddAttribute("az.namespace", _resourceProviderNamespace);
            }
            return scope;
        }

        /// <summary>
        /// This method combines client namespace and operation name into an ActivitySource name and creates the activity source.
        /// For example:
        ///     ns: Azure.Storage.Blobs
        ///     name: BlobClient.DownloadTo
        ///     result Azure.Storage.Blobs.BlobClient
        /// </summary>
        private static object? GetActivitySource(string ns, string name)
        {
            if (!ActivityExtensions.SupportsActivitySource())
            {
                return null;
            }

            string clientName = ns;
            int indexOfDot = name.IndexOf(".", StringComparison.OrdinalIgnoreCase);
            if (indexOfDot != -1)
            {
                clientName += "." + name.Substring(0, indexOfDot);
            }
            return ActivitySources.GetOrAdd(clientName, static n => ActivityExtensions.CreateActivitySource(n));
        }
    }
}
#endif