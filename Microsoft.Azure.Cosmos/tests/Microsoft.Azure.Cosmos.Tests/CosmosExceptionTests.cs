//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosExceptionTests
    {
        [TestMethod]
        public void EnsureSuccessStatusCode_DontThrowOnSuccess()
        {
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.EnsureSuccessStatusCode();
        }

        [TestMethod]
        public void EnsureSuccessStatusCode_ThrowsOnFailure()
        {
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.NotFound);
            Assert.ThrowsException<CosmosException>(() => responseMessage.EnsureSuccessStatusCode());
        }

        [TestMethod]
        public void EnsureSuccessStatusCode_ThrowsOnFailure_ContainsBody()
        {
            string testContent = "TestContent";
            using (MemoryStream memoryStream = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(memoryStream);
                sw.Write(testContent);
                sw.Flush();
                memoryStream.Seek(0, SeekOrigin.Begin);

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.NotFound) { Content = memoryStream };
                try
                {
                    responseMessage.EnsureSuccessStatusCode();
                    Assert.Fail("Should have thrown");
                }
                catch (CosmosException exception)
                {
                    Assert.IsTrue(exception.Message.Contains(testContent));
                }
            }
        }

        [TestMethod]
        public void EnsureSuccessStatusCode_ThrowsOnFailure_ContainsJsonBody()
        {
            string message = "TestContent";
            Error error = new Error
            {
                Code = "code",
                Message = message
            };
            string testContent = JsonConvert.SerializeObject(error);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                StreamWriter sw = new StreamWriter(memoryStream);
                sw.Write(testContent);
                sw.Flush();
                memoryStream.Seek(0, SeekOrigin.Begin);

                ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.NotFound) { Content = memoryStream };
                try
                {
                    responseMessage.EnsureSuccessStatusCode();
                    Assert.Fail("Should have thrown");
                }
                catch (CosmosException exception)
                {
                    Assert.IsTrue(exception.Message.Contains(message));
                }
            }
        }

        [TestMethod]
        public void VerifyDocumentClientExceptionWithNullHeader()
        {
            string errorMessage = "Test Exception!";

            DocumentClientException dce = new DocumentClientException(
                message: errorMessage,
                innerException: null,
                statusCode: HttpStatusCode.BadRequest);

            string headerValue = "Test" + Guid.NewGuid();
            dce.Headers.Add(headerValue, null);

            ResponseMessage responseMessage = dce.ToCosmosResponseMessage(null);
            Assert.IsNull(responseMessage.Headers.Get(headerValue));
        }

        [TestMethod]
        public void VerifyDocumentClientExceptionToResponseMessage()
        {
            string errorMessage = "Test Exception!";
            DocumentClientException dce = null;
            try
            {
                throw new DocumentClientException(
                    message: errorMessage,
                    statusCode: HttpStatusCode.BadRequest,
                    subStatusCode: SubStatusCodes.WriteForbidden);
            }
            catch (DocumentClientException exception)
            {
                dce = exception;
            }

            ResponseMessage responseMessage = dce.ToCosmosResponseMessage(null);
            Assert.IsFalse(responseMessage.IsSuccessStatusCode);
            Assert.AreEqual(HttpStatusCode.BadRequest, responseMessage.StatusCode);
            Assert.AreEqual(SubStatusCodes.WriteForbidden, responseMessage.Headers.SubStatusCode);
            Assert.IsTrue(responseMessage.ErrorMessage.Contains(errorMessage));
            Assert.IsFalse(responseMessage.ErrorMessage.Contains("VerifyDocumentClientExceptionToResponseMessage"), $"Message should not have the stack trace {responseMessage.ErrorMessage}. StackTrace should be in Diagnostics.");
        }

        [TestMethod]
        public void VerifyTransportExceptionToResponseMessage()
        {
            string errorMessage = "Test Exception!";
            DocumentClientException dce = null;
            TransportException transportException = new TransportException(
                errorCode: TransportErrorCode.ConnectionBroken,
                innerException: null,
                activityId: Guid.NewGuid(),
                requestUri: new Uri("https://localhost"),
                sourceDescription: "The SourceDescription",
                userPayload: true,
                payloadSent: true);

            try
            {
                throw new ServiceUnavailableException(
                    message: errorMessage,
                    innerException: transportException);
            }
            catch (DocumentClientException exception)
            {
                dce = exception;
            }

            ResponseMessage responseMessage = dce.ToCosmosResponseMessage(null);
            Assert.IsFalse(responseMessage.IsSuccessStatusCode);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, responseMessage.StatusCode);
            Assert.IsTrue(responseMessage.ErrorMessage.Contains(errorMessage));
            Assert.IsFalse(responseMessage.ErrorMessage.Contains(transportException.ToString()), "InnerException tracked in Diagnostics");
        }

        [TestMethod]
        public void EnsureCorrectStatusCode()
        {
            string testMessage = "Test" + Guid.NewGuid().ToString();

            List<(HttpStatusCode statusCode, CosmosException exception)> exceptionsToStatusCodes = new List<(HttpStatusCode, CosmosException)>()
            {
                (HttpStatusCode.NotFound, CosmosExceptionFactory.CreateNotFoundException(testMessage, activityId: Guid.NewGuid().ToString())),
                (HttpStatusCode.InternalServerError, CosmosExceptionFactory.CreateInternalServerErrorException(testMessage, activityId: Guid.NewGuid().ToString())),
                (HttpStatusCode.BadRequest, CosmosExceptionFactory.CreateBadRequestException(testMessage, activityId: Guid.NewGuid().ToString())),
                (HttpStatusCode.RequestTimeout,CosmosExceptionFactory.CreateRequestTimeoutException(testMessage, activityId: Guid.NewGuid().ToString())),
                ((HttpStatusCode)429, CosmosExceptionFactory.CreateThrottledException(testMessage, activityId: Guid.NewGuid().ToString())),
            };

            foreach ((HttpStatusCode statusCode, CosmosException exception) item in exceptionsToStatusCodes)
            {
                this.ValidateExceptionInfo(item.exception, item.statusCode, testMessage);
            }
        }

        [TestMethod]
        public void ValidateExceptionStackTraceHandling()
        {
            CosmosException cosmosException = CosmosExceptionFactory.CreateNotFoundException("TestMessage");
            Assert.AreEqual(null, cosmosException.StackTrace);
            Assert.IsFalse(cosmosException.ToString().Contains(nameof(ValidateExceptionStackTraceHandling)));
            try
            {
                throw cosmosException;
            }
            catch (CosmosException ce)
            {
                Assert.IsTrue(ce.StackTrace.Contains(nameof(ValidateExceptionStackTraceHandling)), ce.StackTrace);
            }

            string stackTrace = "OriginalDocumentClientExceptionStackTrace";
            try
            {
                throw CosmosExceptionFactory.CreateNotFoundException("TestMessage", stackTrace: stackTrace);
            }
            catch (CosmosException ce)
            {
                Assert.AreEqual(stackTrace, ce.StackTrace);
            }
        }

        [TestMethod]
        public void ValidateErrorHandling()
        {
            Error error = new Error()
            {
                Code = System.Net.HttpStatusCode.BadRequest.ToString(),
                Message = "Unsupported Query",
                AdditionalErrorInfo = "Additional error info message",

            };

            CosmosException cosmosException = CosmosExceptionFactory.CreateBadRequestException(
                error.ToString(),
                error: error,
                trace: NoOpTrace.Singleton);

            ResponseMessage responseMessage = QueryResponse.CreateFailure(
                statusCode: System.Net.HttpStatusCode.BadRequest,
                cosmosException: cosmosException,
                requestMessage: null,
                trace: NoOpTrace.Singleton,
                responseHeaders: null);

            Assert.AreEqual(error, responseMessage.CosmosException.Error);
            Assert.IsTrue(responseMessage.ErrorMessage.Contains(error.Message));
            Assert.IsTrue(responseMessage.ErrorMessage.Contains(error.AdditionalErrorInfo));

            try
            {
                responseMessage.EnsureSuccessStatusCode();
                Assert.Fail("Should throw exception");
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(ce.Message.Contains(error.Message));
                Assert.IsTrue(ce.ToString().Contains(error.Message));
                Assert.IsTrue(ce.ToString().Contains(error.AdditionalErrorInfo));
            }
        }

        private void ValidateExceptionInfo(
            CosmosException exception,
            HttpStatusCode httpStatusCode,
            string message)
        {
            Assert.AreEqual(message, exception.ResponseBody);
            Assert.AreEqual(httpStatusCode, exception.StatusCode);
            Assert.IsTrue(exception.ToString().Contains(message));
            string expectedMessage = $"Response status code does not indicate success: {httpStatusCode} ({(int)httpStatusCode}); Substatus: 0; ActivityId: {exception.ActivityId}; Reason: ({message});";

            Assert.AreEqual(expectedMessage, exception.Message);
        }
    }
}
