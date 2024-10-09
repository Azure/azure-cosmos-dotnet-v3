//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Patch.Builders
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class PatchOperationBuilderTests
    {
        [TestMethod]
        public void AddPropertyToSimpleProperty()
        {
            // Gherkin:
            // Given I have a ToDoActivity with a simple Id property
            // When I add a PatchOperation to add a new Id
            // Then the patch operation should include the correct path and value

            ToDoActivity toDoActivity = new ToDoActivity { Id = "1" };

            PatchOperationBuilder<ToDoActivity> builder = new PatchOperationBuilder<ToDoActivity>(
                toDoActivity,
                op => op.SetOperationType(PatchOperationType.Add)
                        .Property(p => p.Id)
            );
            
            CosmosJsonDotNetSerializer serializer = new CosmosJsonDotNetSerializer(new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
            });

            List<PatchOperation> patchOperations = builder.Build(serializer);

            Assert.AreEqual(1, patchOperations.Count);
            Assert.AreEqual("/id", patchOperations[0].Path);

            _ = patchOperations[0].TrySerializeValueParameter(serializer, out Stream valueAsStream);

            string value;
            using (StreamReader reader = new StreamReader(valueAsStream))
            {
                value = reader.ReadToEnd();
            }

            Assert.AreEqual("1", value);
        }

        [TestMethod]
        public void AddPropertyToNestedProperty()
        {
            // Gherkin:
            // Given I have a ToDoActivity with a nested Address property
            // When I add a PatchOperation to add a new City to the Address
            // Then the patch operation should include the correct path and value

            ToDoActivity toDoActivity = new ToDoActivity { Address = new Address1 { City = "Seattle" } };

            PatchOperationBuilder<ToDoActivity> builder = new PatchOperationBuilder<ToDoActivity>(
                toDoActivity,
                op => op.SetOperationType(PatchOperationType.Add)
                        .Property(p => p.Address.City)
            );

            CosmosJsonDotNetSerializer serializer = new CosmosJsonDotNetSerializer(new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
            });

            List<PatchOperation> patchOperations = builder.Build(serializer);

            Assert.AreEqual(1, patchOperations.Count);
            Assert.AreEqual("/city", patchOperations[0].Path);

            _ = patchOperations[0].TrySerializeValueParameter(serializer, out Stream valueAsStream);

            string value;
            using (StreamReader reader = new StreamReader(valueAsStream))
            {
                value = reader.ReadToEnd();
            }

            Assert.AreEqual("Seattle", value);
        }

        [TestMethod]
        public void PatchBuildAddOnePropertyTest()
        {
            ToDoActivity toDoActivity = new ToDoActivity()
            {
                Id = Guid.NewGuid().ToString(),
            };

            PatchOperationBuilder<ToDoActivity> builder = new PatchOperationBuilder<ToDoActivity>(
                instance: toDoActivity,
                patches: op => op.SetOperationType(PatchOperationType.Add)
                    .Property(p => p.Id));

            CosmosJsonDotNetSerializer serializer = new CosmosJsonDotNetSerializer(new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
            });

            List<PatchOperation> patchOperations = builder.Build(serializer);

            foreach (PatchOperation patch in patchOperations)
            {
                _ = patch.TrySerializeValueParameter(serializer, out Stream valueAsStream);

                string value;
                using (StreamReader reader = new StreamReader(valueAsStream))
                {
                    value = reader.ReadToEnd();
                }

                Console.WriteLine($"Operation: {patch.OperationType}, Path: {patch.Path}, Value: {value}");
            }
        }
    }
}
