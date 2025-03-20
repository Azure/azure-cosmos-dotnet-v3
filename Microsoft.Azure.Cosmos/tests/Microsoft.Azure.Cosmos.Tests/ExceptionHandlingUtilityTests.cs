//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ExceptionHandlingUtilityTests
    {
        [TestMethod]
        [ExpectedException(typeof(TaskCanceledException))]
        public void CloneAndRethrow_TaskCanceledException()
        {
            // Arrange
            TaskCanceledException originalException = new TaskCanceledException("Task was canceled.", new TaskCanceledException("inner exception"));

            // Act & Assert
            try
            {
                ExceptionHandlingUtility.CloneAndRethrowException(originalException);
            }
            catch (TaskCanceledException ex)
            {
                Assert.IsFalse(Object.ReferenceEquals(originalException, ex)); // Ensure a new exception was created
                Assert.AreEqual(originalException.ToString(), ex.InnerException.ToString());
                Assert.AreEqual(originalException.Message, ex.Message);
                throw;
            }
        }
        [TestMethod]
        [ExpectedException(typeof(TimeoutException))]
        public void CloneAndRethrow_TimeoutException()
        {
            // Arrange
            TimeoutException originalException = new TimeoutException("Operation timed out.", new TimeoutException("inner exception"));

            // Act & Assert
            try
            {
                ExceptionHandlingUtility.CloneAndRethrowException(originalException);
            }
            catch (TimeoutException ex)
            {
                Assert.IsFalse(Object.ReferenceEquals(originalException, ex)); // Ensure a new exception was created
                Assert.AreEqual(originalException.ToString(), ex.InnerException.ToString());
                Assert.AreEqual(originalException.Message, ex.Message);
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public void CloneAndRethrow_CosmosException()
        {
            // Arrange
            CosmosException originalException = CosmosExceptionFactory.CreateInternalServerErrorException("something's broken", new Headers());

            // Act & Assert
            try
            {
                ExceptionHandlingUtility.CloneAndRethrowException(originalException);
            }
            catch (CosmosException ex)
            {
                Assert.IsFalse(Object.ReferenceEquals(originalException, ex)); // Ensure a new exception was createdAssert.AreNotSame(originalException, ex);
                Assert.AreEqual(originalException.StatusCode, ex.StatusCode);
                Assert.AreEqual(originalException.Message, ex.Message);
                throw;
            }
        }

        [TestMethod]
        public void DoNotClone_And_Rethrow_OtherException()
        {
            // Arrange
            MalformedContinuationTokenException originalException =
                new MalformedContinuationTokenException("malformed continuation token", new MalformedContinuationTokenException("Inner"));

            try
            {
                // Act & Assert
                ExceptionHandlingUtility.CloneAndRethrowException(originalException);
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected no exception to be thrown, but got: " + ex.Message);
            }

        }
    }
}
