//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class OpenTelemetryRecorderTests
    {
        private const string DllName = "Microsoft.Azure.Cosmos.Client";

        private static Assembly GetAssemblyLocally(string name)
        {
            Assembly.Load(name);
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            return loadedAssemblies
                .Where((candidate) => candidate.FullName.Contains(name + ","))
                .FirstOrDefault();
        }

        [TestMethod]
        public void CheckExceptionsCompatibility()
        {
            Assembly asm = OpenTelemetryRecorderTests.GetAssemblyLocally(DllName);

            // Get all types (including internal) defined in the assembly
            IEnumerable<Type> actualClasses = asm
                .GetTypes()
                .Where(type => type.Namespace == "Microsoft.Azure.Cosmos" && type.Name.EndsWith("Exception"));

            foreach(Type className in actualClasses)
            {
                Assert.IsTrue(OpenTelemetryCoreRecorder.OTelCompatibleExceptions.Keys.Contains(className), $"{className.Name} is not added in {typeof(OpenTelemetryCoreRecorder).Name} Class OTelCompatibleExceptions dictionary");
            }
            
        }

        [TestMethod]
        public void CheckResponseCompatibility()
        {
            Assembly asm = OpenTelemetryRecorderTests.GetAssemblyLocally(DllName);

            IReadOnlyList<string> excludedClasses = new List<string>()
            {
                "MediaResponse",
                "CosmosQuotaResponse",
                "OpenTelemetryResponse",
                "ClientEncryptionKeyResponse",
                "ContainerResponse",
                "DatabaseResponse",
                "PermissionResponse",
                "ThroughputResponse",
                "UserResponse",
                "StoredProcedureResponse",
                "TriggerResponse",
                "UserDefinedFunctionResponse",
                "ChangeFeedEstimatorFeedResponse",
                "ChangeFeedEstimatorEmptyFeedResponse"
            };

            // Get all types (including internal) defined in the assembly
            IEnumerable<Type> actualClasses = asm
                .GetTypes()
                .Where(type => type.Name.EndsWith("Response"));

            foreach (Type className in actualClasses)
            {
                if (className.IsAbstract || className.IsInterface || excludedClasses.Contains(className.Name))
                {
                    continue;
                }

                Console.WriteLine("===================> " + className.FullName);
                object instance = CreateInstance(className);
                if (instance is TransactionalBatchResponse transactionInstance)
                {
                    OpenTelemetryResponse oTelResponse = new OpenTelemetryResponse(transactionInstance);
                }
                else if (instance is ResponseMessage responseMessageInstance)
                {
                    OpenTelemetryResponse oTelResponse = new OpenTelemetryResponse(responseMessageInstance);
                }
                else
                {
                    Console.WriteLine($"*********** Response type {className.Name} is not supported");
                }
            }

            excludedClasses = new List<string>()
            {
                "IStoredProcedureResponse`1",
                "IDocumentFeedResponse`1",
                "OpenTelemetryResponse`1",
                "DocumentFeedResponse`1"
            };

            actualClasses = asm
               .GetTypes()
               .Where(type => type.Name.EndsWith("Response`1"));

            foreach (Type className in actualClasses)
            {
                if (className.IsAbstract || className.IsInterface || excludedClasses.Contains(className.Name))
                {
                    continue;
                }

                Console.WriteLine("===================> " + className.FullName);
                object instance = CreateInstance(className);
                if (instance is FeedResponse<object> feedResponse)
                {
                    OpenTelemetryResponse<object> oTelResponse = new OpenTelemetryResponse<object>(feedResponse);
                }
                else if (instance is Response<object> responseMessageInstance)
                {
                    OpenTelemetryResponse<object> oTelResponse = new OpenTelemetryResponse<object>(responseMessageInstance);
                }
                else
                {
                    Console.WriteLine($"*********** Response type {className.Name} is not supported");
                }
            }

        }

        private static object CreateInstance(Type type)
        {
            Console.WriteLine(type.Name);

            if (type != null)
            {
                if (type.Name == "ITrace")
                {
                    Console.WriteLine("Itace type : " + type.Name);
                    return NoOpTrace.Singleton;
                }
                if (type.Name == "CosmosSerializer")
                {
                    Console.WriteLine("CosmosSerializer  : " + type.Name);
                    return new CosmosJsonDotNetSerializer();
                }
                if (type.Name == "CosmosSerializationFormatOptions")
                {
                    Console.WriteLine("CosmosSerializer  : " + type.Name);
                    return null;
                }

                if (type.IsAbstract || type.IsInterface)
                {
                    Console.WriteLine("isabstract or interface");
                    return null;
                }

                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                ConstructorInfo constructor = constructors.FirstOrDefault();

                if (constructor != null)
                {
                    ParameterInfo[] parameters = constructor.GetParameters();

                    Console.WriteLine("   Constructor parameters : " + parameters.Length);

                    object[] parameterValues = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        parameterValues[i] = GetDefault(parameters[i].ParameterType);
                    }

                    return constructor.Invoke(parameterValues);
                    
                }
                else
                {
                    return Activator.CreateInstance(type);
                }
            }

            return null;
        }

        private static object GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                if(type.Name == "IntPtr")
                {
                    return null;
                }

                Console.WriteLine("Value Type : " + type.Name);
                return Activator.CreateInstance(type);
            }
            else if (type.IsArray)
            {
                Console.WriteLine("Array Type : " + type.Name);
                return Array.CreateInstance(type.GetElementType(), 0);
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                Console.WriteLine("List Type : " + type.Name);
                return (IReadOnlyList<object>)Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GetGenericArguments()));
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
            {
                Console.WriteLine("List Type : " + type.Name);
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GetGenericArguments()));
            }
            else
            {
                return CreateInstance(type);
            }
        }

        [TestMethod]
        public void MarkFailedTest()
        {
            Assert.IsFalse(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new Exception(), 
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosNullReferenceException(
                    new NullReferenceException(), 
                    NoOpTrace.Singleton), 
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosObjectDisposedException(
                    new ObjectDisposedException("dummyobject"),
                    MockCosmosUtil.CreateMockCosmosClient(), 
                    NoOpTrace.Singleton), 
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosOperationCanceledException(
                    new OperationCanceledException(), 
                    new CosmosTraceDiagnostics(NoOpTrace.Singleton)), 
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new CosmosException(
                    System.Net.HttpStatusCode.OK, 
                    "dummyMessage", 
                    "dummyStacktrace",
                    null, 
                    NoOpTrace.Singleton, 
                    default, 
                    null), 
                default));

            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
                new ChangeFeedProcessorUserException(
                    new Exception(), 
                    default), 
                default));

            // If there is an unregistered exception is thrown, defaut exception wil be called
            Assert.IsFalse(OpenTelemetryCoreRecorder.IsExceptionRegistered(
              new NewCosmosException(),
              default));

            // If there is an child exception of non sealed exception, corresponding parent exception class will be called
            Assert.IsTrue(OpenTelemetryCoreRecorder.IsExceptionRegistered(
             new NewCosmosObjectDisposedException(
                 new ObjectDisposedException("dummyobject"),
                 MockCosmosUtil.CreateMockCosmosClient(),
                 NoOpTrace.Singleton),
             default));
        }
    }

    internal class NewCosmosException : System.Exception
    {
        /// <inheritdoc/>
        public override string Message => "dummy exception message";
    }

    internal class NewCosmosObjectDisposedException : CosmosObjectDisposedException
    {
        internal NewCosmosObjectDisposedException(ObjectDisposedException originalException, CosmosClient cosmosClient, ITrace trace) 
            : base(originalException, cosmosClient, trace)
        {
        }

        /// <inheritdoc/>
        public override string Message => "dummy CosmosObjectDisposedException message";
    }

}
