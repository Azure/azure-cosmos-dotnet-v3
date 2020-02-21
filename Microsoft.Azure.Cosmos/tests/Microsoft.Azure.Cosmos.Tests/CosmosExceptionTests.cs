//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
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
            Assert.ThrowsException<CosmosNotFoundException>(() => responseMessage.EnsureSuccessStatusCode());
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
                catch(CosmosException exception)
                {
                    Assert.IsTrue(exception.Message.Contains(testContent));
                }
            }
        }

        [TestMethod]
        public void EnsureSuccessStatusCode_ThrowsOnFailure_ContainsJsonBody()
        {
            string message = "TestContent";
            Error error = new Error();
            error.Code = "code";
            error.Message = message;
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
            Assert.IsTrue(responseMessage.ErrorMessage.Contains("VerifyDocumentClientExceptionToResponseMessage"), $"Message should have method name for the stack trace {responseMessage.ErrorMessage}");
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
            Assert.IsTrue(responseMessage.ErrorMessage.Contains(transportException.ToString()));
            Assert.IsTrue(responseMessage.ErrorMessage.Contains("VerifyTransportExceptionToResponseMessage"), $"Message should have method name for the stack trace {responseMessage.ErrorMessage}");
        }

        [TestMethod]
        public void EnsureCorrectStatusCode()
        {
            string testMessage = "Test" + Guid.NewGuid().ToString();

            List<(HttpStatusCode statusCode, CosmosException exception)> exceptionsToStatusCodes = new List<(HttpStatusCode, CosmosException)>()
            {
                (HttpStatusCode.NotFound, new CosmosNotFoundException(testMessage)),
                (HttpStatusCode.InternalServerError, new CosmosInternalServerErrorException(testMessage)),
                (HttpStatusCode.BadRequest, new CosmosBadRequestException(testMessage)),
                (HttpStatusCode.RequestTimeout, new CosmosRequestTimeoutException(testMessage)),
                ((HttpStatusCode)429, new CosmosThrottledException(testMessage)),
            };

            foreach((HttpStatusCode statusCode, CosmosException exception) item in exceptionsToStatusCodes)
            {
                this.ValidateExceptionInfo(item.exception, item.statusCode, testMessage);
            }
        }

        [TestMethod]
        public void ValidateExceptionStackTraceHandling()
        {
            CosmosException cosmosException = new CosmosNotFoundException("TestMessage");
            Assert.AreEqual(null, cosmosException.StackTrace);
            Assert.IsFalse(cosmosException.ToString().Contains(nameof(ValidateExceptionStackTraceHandling)));
            try
            {
                throw cosmosException;
            }
            catch(CosmosException ce)
            {
                Assert.IsTrue(ce.StackTrace.Contains(nameof(ValidateExceptionStackTraceHandling)), ce.StackTrace);
            }

            string stackTrace = "OriginalDocumentClientExceptionStackTrace";
            try
            {
                throw new CosmosNotFoundException("TestMessage", stackTrace: stackTrace);
            }
            catch (CosmosException ce)
            {
                Assert.AreEqual(stackTrace, ce.StackTrace);
            }
        }

        private void ValidateExceptionInfo(
            CosmosException exception,
            HttpStatusCode httpStatusCode,
            string message)
        {
            Assert.AreEqual(httpStatusCode, exception.StatusCode);
            Assert.IsTrue(exception.ToString().Contains(message));
        }
    }
}
