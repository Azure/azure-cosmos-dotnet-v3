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

            // Get all types (including internal) defined in the assembly
            IEnumerable<Type> actualClasses = asm
                .GetTypes()
                .Where(type => type.Name.Contains("Response"));

            foreach (Type className in actualClasses)
            {
                Console.WriteLine(className);
                //Assert.IsTrue(OpenTelemetryCoreRecorder.OTelCompatibleExceptions.Keys.Contains(className), $"{className.Name} is not added in {typeof(OpenTelemetryCoreRecorder).Name} Class OTelCompatibleExceptions dictionary");
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
