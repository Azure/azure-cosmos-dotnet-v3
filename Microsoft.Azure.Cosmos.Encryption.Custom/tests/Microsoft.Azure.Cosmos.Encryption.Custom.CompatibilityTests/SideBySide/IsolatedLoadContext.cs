using System;
using System.Runtime.Loader;

namespace Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests.SideBySide
{
    /// <summary>
    /// Isolated AssemblyLoadContext for loading different versions of the library side-by-side.
    /// Each context maintains its own assembly dependency graph, preventing version conflicts.
    /// </summary>
    internal class IsolatedLoadContext : AssemblyLoadContext
    {
        private readonly string assemblyPath;

        public IsolatedLoadContext(string assemblyPath, string name) : base(name, isCollectible: true)
        {
            this.assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
        }

        protected override System.Reflection.Assembly Load(System.Reflection.AssemblyName assemblyName)
        {
            // Let the default context handle framework and shared assemblies
            // Only load our specific assembly and its unique dependencies in isolation
            return null;
        }
    }
}
