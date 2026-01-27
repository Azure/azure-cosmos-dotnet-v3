// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;

    /// <summary>
    /// Provides HybridRow schema definitions for distributed transaction operations and results.
    /// </summary>
    internal static class DistributedTransactionSchemaProvider
    {
        static DistributedTransactionSchemaProvider()
        {
            string json = DistributedTransactionSchemaProvider.GetEmbeddedResource(@"DistributedTransaction\HybridRowDistributedTransactionSchemas.json");
            DistributedTransactionSchemaProvider.SchemaNamespace = Namespace.Parse(json);
            DistributedTransactionSchemaProvider.LayoutResolver = new LayoutResolverNamespace(DistributedTransactionSchemaProvider.SchemaNamespace);

            DistributedTransactionSchemaProvider.OperationLayout = DistributedTransactionSchemaProvider.LayoutResolver.Resolve(
                DistributedTransactionSchemaProvider.SchemaNamespace.Schemas.Find(x => x.Name == "DistributedTransactionOperation").SchemaId);
            DistributedTransactionSchemaProvider.ResultLayout = DistributedTransactionSchemaProvider.LayoutResolver.Resolve(
                DistributedTransactionSchemaProvider.SchemaNamespace.Schemas.Find(x => x.Name == "DistributedTransactionResult").SchemaId);
        }

        public static Namespace SchemaNamespace { get; private set; }

        public static LayoutResolverNamespace LayoutResolver { get; private set; }

        public static Layout OperationLayout { get; private set; }

        public static Layout ResultLayout { get; private set; }

        private static string GetEmbeddedResource(string resourceName)
        {
            Assembly assembly = Assembly.GetAssembly(typeof(DistributedTransactionSchemaProvider));

            resourceName = DistributedTransactionSchemaProvider.FormatResourceName(typeof(DistributedTransactionSchemaProvider).Namespace, resourceName);

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
