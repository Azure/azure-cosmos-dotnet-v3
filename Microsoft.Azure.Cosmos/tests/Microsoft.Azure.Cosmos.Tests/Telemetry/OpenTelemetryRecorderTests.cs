//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Telemetry.Diagnostics;
    using Microsoft.Azure.Cosmos.Tests.Contracts;
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

            IEnumerable<Type> actualClasses = asm
                .GetTypes()
                .Where(type => type.Namespace == "Microsoft.Azure.Cosmos" && type.Name.EndsWith("Exception"));

            foreach(Type className in actualClasses)
            {
                Assert.IsTrue(OpenTelemetryCoreRecorder.oTelCompatibleExceptions.Keys.Contains(className), $"{className.Name} is not added in {typeof(OpenTelemetryCoreRecorder).Name} Class oTelCompatibleExceptions dictionary");
            }
            
        }
    }
}
