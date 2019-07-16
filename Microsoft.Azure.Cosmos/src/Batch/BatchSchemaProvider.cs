//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;

    internal static class BatchSchemaProvider
    {
        static BatchSchemaProvider()
        {
            string json = BatchSchemaProvider.GetEmbeddedResource(@"Batch\HybridRowBatchSchemas.json");
            BatchSchemaProvider.BatchSchemaNamespace = Namespace.Parse(json);
            BatchSchemaProvider.BatchLayoutResolver = new LayoutResolverNamespace(BatchSchemaProvider.BatchSchemaNamespace);

            BatchSchemaProvider.BatchOperationLayout = BatchSchemaProvider.BatchLayoutResolver.Resolve(BatchSchemaProvider.BatchSchemaNamespace.Schemas.Find(x => x.Name == "BatchOperation").SchemaId);
            BatchSchemaProvider.BatchResultLayout = BatchSchemaProvider.BatchLayoutResolver.Resolve(BatchSchemaProvider.BatchSchemaNamespace.Schemas.Find(x => x.Name == "BatchResult").SchemaId);
        }

        public static Namespace BatchSchemaNamespace { get; private set; }

        public static LayoutResolverNamespace BatchLayoutResolver { get; private set; }

        public static Layout BatchOperationLayout { get; private set; }

        public static Layout BatchResultLayout { get; private set; }

        private static string GetEmbeddedResource(string resourceName)
        {
            Assembly assembly = Assembly.GetAssembly(typeof(BatchSchemaProvider));

            // Assumes BatchSchemaProvider is in the default namespace of the assembly.
            resourceName = BatchSchemaProvider.FormatResourceName(typeof(BatchSchemaProvider).Namespace, resourceName);

            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    return null;
                }

                using (StreamReader reader = new StreamReader(resourceStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static string FormatResourceName(string namespaceName, string resourceName)
        {
            return namespaceName + "." + resourceName.Replace(" ", "_").Replace("\\", ".").Replace("/", ".");
        }
    }
}