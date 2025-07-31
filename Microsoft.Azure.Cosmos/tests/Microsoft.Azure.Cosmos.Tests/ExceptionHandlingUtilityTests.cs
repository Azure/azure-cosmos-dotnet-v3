//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Metadata;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ExceptionHandlingUtilityTests
    {
        [TestMethod]
        [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method)]
        public void CloneAndRethrow_OperationCanceledExceptionTypes(OperationCanceledException original)
        {
            // Act & Assert
            bool result = ExceptionHandlingUtility.TryCloneException(original, out Exception ex);

            // Assert
            Assert.IsTrue(result, "Expected exception to be cloned.");
            Assert.IsNotNull(ex, "Expected cloned exception to be not null.");
            Assert.IsTrue(ex.GetType() == original.GetType(), $"Expected cloned exception to be of type : '{original.GetType()}'");
            Assert.IsFalse(Object.ReferenceEquals(original, ex)); // Ensure a new exception was created
            Assert.AreEqual(original.ToString(), ex.InnerException.ToString());
            Assert.AreEqual(original.Message, ex.Message);
        }

        [TestMethod]
        [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method)]
        public void CloneAndRethrow_CosmosOperationCanceledExceptionTypes(OperationCanceledException original)
        {
            CosmosOperationCanceledException cosmosOperationCanceledException = new CosmosOperationCanceledException(original, new CosmosTraceDiagnostics(NoOpTrace.Singleton));
            bool result = ExceptionHandlingUtility.TryCloneException(cosmosOperationCanceledException, out Exception cloneResult);

            // Assert
            Assert.IsTrue(result, "Expected exception to be cloned.");
            Assert.IsNotNull(cloneResult, "Expected cloned exception to be not null.");
            Assert.IsTrue(cloneResult.GetType() == cosmosOperationCanceledException.GetType(), $"Expected cloned exception to be of type : '{cosmosOperationCanceledException.GetType()}'");
            Assert.IsFalse(Object.ReferenceEquals(original, cloneResult)); // Ensure a new exception was created
            Assert.AreEqual(cosmosOperationCanceledException.ToString(), cloneResult.ToString()); // IClonable
            Assert.AreEqual(cosmosOperationCanceledException.Message, cloneResult.Message);

            //Assert: Shallow copy
            Assert.IsTrue(object.ReferenceEquals(cosmosOperationCanceledException.InnerException, cloneResult.InnerException));
        }

        private static IEnumerable<object[]> GetTestData()
        {
            return new List<object[]>
            {
                new object[] { new TaskCanceledException("Task was canceled.", new TaskCanceledException("inner")) },
                new object[] { new OperationCanceledException("Operation was canceled.", new OperationCanceledException("inner")) },
            };
        }

        [TestMethod]
        [ExpectedException(typeof(TaskCanceledException))]
        public void CloneAndRethrow_TaskCanceledException()
        {
            // Arrange
            TaskCanceledException originalException = new TaskCanceledException("Task was canceled.", new TaskCanceledException("inner exception"));

            // Act & Assert
            bool result = ExceptionHandlingUtility.TryCloneException(originalException, out Exception ex);

            Assert.IsTrue(result, "Expected exception to be cloned.");
            Assert.IsNotNull(ex, "Expected cloned exception to be not null.");
            Assert.IsTrue(ex is TaskCanceledException, "Expected cloned exception to be of type TaskCanceledException.");
            Assert.IsFalse(Object.ReferenceEquals(originalException, ex)); // Ensure a new exception was created
            Assert.AreEqual(originalException.ToString(), ex.InnerException.ToString());
            Assert.AreEqual(originalException.Message, ex.Message);
            throw ex;
        }

        [TestMethod]
        [ExpectedException(typeof(TimeoutException))]
        public void CloneAndRethrow_TimeoutException()
        {
            // Arrange
            TimeoutException originalException = new TimeoutException("Operation timed out.", new TimeoutException("inner exception"));

            // Act & Assert
            bool result = ExceptionHandlingUtility.TryCloneException(originalException, out Exception ex);

            Assert.IsTrue(result, "Expected exception to be cloned.");
            Assert.IsNotNull(ex, "Expected cloned exception to be not null.");
            Assert.IsTrue(ex is TimeoutException, "Expected cloned exception to be of type TimeoutException.");
            Assert.IsFalse(Object.ReferenceEquals(originalException, ex)); // Ensure a new exception was created
            Assert.AreEqual(originalException.ToString(), ex.InnerException.ToString());
            Assert.AreEqual(originalException.Message, ex.Message);
            throw ex;
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public void CloneAndRethrow_CosmosException()
        {
            // Arrange
            CosmosException originalException = CosmosExceptionFactory.CreateInternalServerErrorException("something's broken", new Headers());

            // Act & Assert
            bool result = ExceptionHandlingUtility.TryCloneException(originalException, out Exception ex);

            Assert.IsTrue(result, "Expected exception to be cloned.");
            Assert.IsNotNull(ex, "Expected cloned exception to be not null.");
            Assert.IsTrue(ex is CosmosException, "Expected cloned exception to be of type CosmosException.");
            Assert.IsFalse(Object.ReferenceEquals(originalException, ex)); // Ensure a new exception was created
            Assert.AreEqual(originalException.ToString(), ex.ToString());
            Assert.AreEqual(originalException.Message, ex.Message);
            throw ex;
        }

        [TestMethod]
        public void DoNotClone_And_Rethrow_OtherException()
        {
            // Arrange
            MalformedContinuationTokenException originalException =
                new MalformedContinuationTokenException("malformed continuation token", new MalformedContinuationTokenException("Inner"));

            // Act & Assert
            bool result = ExceptionHandlingUtility.TryCloneException(originalException, out _);

            Assert.IsFalse(result, "Expected exception to not be cloned.");
        }
    }
}
