//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Net;
    using System.Security.Cryptography;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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
        public void VerifyHeaderAlwaysExists()
        {
            CosmosException cosmosException = new CosmosException(
                statusCode: HttpStatusCode.BadRequest,
                message: "Test",
                stackTrace: null,
                headers: null,
                trace: NoOpTrace.Singleton,
                error: null,
                innerException: null);

            Assert.IsNotNull(cosmosException.Headers, "Header should always be created to avoid null refs caused by users always expecting it to be there");
        }

        [TestMethod]
        public void VerifyDiagnosticsInTimeoutAndServerError()
        {
            ITrace trace = NoOpTrace.Singleton;
            string diagnosticString = new Diagnostics.CosmosTraceDiagnostics(trace).ToString();

            CosmosException cosmosException = new CosmosException(
                statusCode: HttpStatusCode.RequestTimeout,
                message: "Test",
                stackTrace: null,
                headers: null,
                trace: trace,
                error: null,
                innerException: null);

            Assert.IsTrue(cosmosException.Message.EndsWith(diagnosticString));
            Assert.IsTrue(cosmosException.ToString().Contains(diagnosticString));

            cosmosException = new CosmosException(
                statusCode: HttpStatusCode.InternalServerError,
                message: "Test",
                stackTrace: null,
                headers: null,
                trace: trace,
                error: null,
                innerException: null);

            Assert.IsTrue(cosmosException.Message.EndsWith(diagnosticString));
            Assert.IsTrue(cosmosException.ToString().Contains(diagnosticString));

            cosmosException = new CosmosException(
                statusCode: HttpStatusCode.ServiceUnavailable,
                message: "Test",
                stackTrace: null,
                headers: null,
                trace: trace,
                error: null,
                innerException: null);

            Assert.IsTrue(cosmosException.Message.EndsWith(diagnosticString));
            Assert.IsTrue(cosmosException.ToString().Contains(diagnosticString));

            cosmosException = new CosmosException(
                statusCode: HttpStatusCode.NotFound,
                message: "Test",
                stackTrace: null,
                headers: null,
                trace: trace,
                error: null,
                innerException: null);

            Assert.IsFalse(cosmosException.Message.Contains(diagnosticString));
            Assert.IsTrue(cosmosException.ToString().Contains(diagnosticString));
        }

        [TestMethod]
        public void VerifyNullHeaderLogic()
        {
            string testMessage = "Test" + Guid.NewGuid().ToString();

            CosmosException exception = new CosmosException(
                statusCode: HttpStatusCode.BadRequest,
                message: testMessage,
                stackTrace: null,
                headers: null,
                trace: NoOpTrace.Singleton,
                error: null,
                innerException: null);

            Assert.IsNotNull(exception.Headers, "Header should always be created to avoid null refs caused by users always expecting it to be there");
            Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.IsTrue(exception.ToString().Contains(testMessage));

            exception = new CosmosException(
                statusCode: HttpStatusCode.BadRequest,
                message: testMessage,
                subStatusCode: 42,
                activityId: "test",
                requestCharge: 4);

            Assert.IsNotNull(exception.Headers, "Header should always be created to avoid null refs caused by users always expecting it to be there");
            Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.AreEqual(testMessage, exception.ResponseBody);
            Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.AreEqual(42, exception.SubStatusCode);
            Assert.AreEqual("42", exception.Headers.SubStatusCodeLiteral);
            Assert.AreEqual("test", exception.ActivityId);
            Assert.AreEqual("test", exception.Headers.ActivityId);
            Assert.AreEqual(4, exception.RequestCharge);
            Assert.AreEqual(4, exception.Headers.RequestCharge);
            Assert.IsTrue(exception.ToString().Contains(testMessage));
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
        public void EnsureSuccessStatusCode_ThrowsOnFailure_ContainsComplexJsonBody()
        {
            JObject error = new JObject
            {
                { "Code", "code" },
                { "Message", "TestContent" },
                { "Error", new JArray { "msg1", "msg2" }},
                { "Link", "https://www.demolink.com" },
                { "Path", "/demo/path" },
                { "EscapedPath", @"/demo/path/with/escape/character" }
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
                    Assert.IsTrue(exception.Message.Contains("code"));
                    Assert.IsTrue(exception.Message.Contains("TestContent"));
                    Assert.IsTrue(exception.Message.Contains("msg1"));
                    Assert.IsTrue(exception.Message.Contains("msg2"));
                    Assert.IsTrue(exception.Message.Contains("https://www.demolink.com"));
                    Assert.IsTrue(exception.Message.Contains("/demo/path"));
                    Assert.IsTrue(exception.Message.Contains("/demo/path/with/escape/character"));
                    Assert.IsFalse(exception.Message.Contains("}"));
                    Assert.IsFalse(exception.Message.Contains("{"));
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void VerifyDocumentClientExceptionWithNullHeader()
        {
            string errorMessage = "Test Exception!";

            DocumentClientException dce = new DocumentClientException(
                message: errorMessage,
                innerException: null,
                statusCode: HttpStatusCode.BadRequest);

            string headerValue = "Test" + Guid.NewGuid();
            dce.Headers.Add(headerValue, null);
        }

        [TestMethod]
        public void VerifyDocumentClientExceptionToResponseMessage()
        {
            string errorMessage = "Test Exception!";
            DocumentClientException dce;
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
            TransportException transportException = new TransportException(
                errorCode: TransportErrorCode.ConnectionBroken,
                innerException: null,
                activityId: Guid.NewGuid(),
                requestUri: new Uri("https://localhost"),
                sourceDescription: "The SourceDescription",
                userPayload: true,
                payloadSent: true);

            DocumentClientException dce;
            try
            {
                throw new ServiceUnavailableException(
                    message: errorMessage,
                    innerException: transportException,
                    SubStatusCodes.Unknown);
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
            string activityId = Guid.NewGuid().ToString();
            int substatuscode = 9000;
            string substatus = substatuscode.ToString();
            double requestCharge = 42;
            double retryAfter = 9000;
            string retryAfterLiteral = retryAfter.ToString();
            List<(HttpStatusCode statusCode, CosmosException exception)> exceptionsToStatusCodes = new List<(HttpStatusCode, CosmosException)>()
            {
                (HttpStatusCode.NotFound, CosmosExceptionFactory.CreateNotFoundException(testMessage, new Headers() { SubStatusCodeLiteral = substatus, ActivityId = activityId, RequestCharge = requestCharge, RetryAfterLiteral = retryAfterLiteral })),
                (HttpStatusCode.InternalServerError, CosmosExceptionFactory.CreateInternalServerErrorException(testMessage, new Headers() {SubStatusCodeLiteral = substatus, ActivityId = activityId, RequestCharge = requestCharge, RetryAfterLiteral = retryAfterLiteral })),
                (HttpStatusCode.BadRequest, CosmosExceptionFactory.CreateBadRequestException(testMessage, new Headers() {SubStatusCodeLiteral = substatus, ActivityId = activityId, RequestCharge = requestCharge, RetryAfterLiteral = retryAfterLiteral })),
                (HttpStatusCode.RequestTimeout,CosmosExceptionFactory.CreateRequestTimeoutException(testMessage, new Headers() {SubStatusCodeLiteral = substatus, ActivityId = activityId, RequestCharge = requestCharge, RetryAfterLiteral = retryAfterLiteral })),
                ((HttpStatusCode)429, CosmosExceptionFactory.CreateThrottledException(testMessage, new Headers() {SubStatusCodeLiteral = substatus, ActivityId = activityId, RequestCharge = requestCharge, RetryAfterLiteral = retryAfterLiteral })),
            };

            foreach ((HttpStatusCode statusCode, CosmosException exception) in exceptionsToStatusCodes)
            {
                this.ValidateExceptionInfo(
                    exception,
                    statusCode,
                    substatus,
                    testMessage,
                    activityId,
                    requestCharge,
                    retryAfter);
            }

            CosmosException cosmosException = CosmosExceptionFactory.CreateNotFoundException(testMessage, new Headers() { SubStatusCodeLiteral = ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(), ActivityId = activityId, RequestCharge = requestCharge, RetryAfterLiteral = retryAfterLiteral });
            this.ValidateExceptionInfo(
                    cosmosException,
                    HttpStatusCode.NotFound,
                    ((int)SubStatusCodes.ReadSessionNotAvailable).ToString(),
                    testMessage,
                    activityId,
                    requestCharge,
                    retryAfter);
        }

        [TestMethod]
        public void ValidateExceptionStackTraceHandling()
        {
            CosmosException cosmosException = CosmosExceptionFactory.CreateNotFoundException("TestMessage", new Headers());
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
                throw CosmosExceptionFactory.CreateNotFoundException("TestMessage", new Headers(), stackTrace: stackTrace);
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
                headers: new Headers(),
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

        [TestMethod]
        public void GenerateEventWithDiagnosticWithFilterOnStatusCodeTest()
        {
            using (TestEventListener listener = new TestEventListener())
            {
                CosmosException exception = new CosmosException(
                   statusCode: HttpStatusCode.BadRequest,
                   message: "testmessage",
                   stackTrace: null,
                   headers: null,
                   trace: NoOpTrace.Singleton,
                   error: null,
                   innerException: null);

                CosmosException.RecordOtelAttributes(exception, default);

                Assert.AreEqual(1, TestEventListener.CapturedEvents.Count);

                TestEventListener.CapturedEvents.Clear();

                exception = new CosmosException(
                   statusCode: HttpStatusCode.NotFound,
                   message: "testmessage",
                   stackTrace: null,
                   headers: null,
                   trace: NoOpTrace.Singleton,
                   error: null,
                   innerException: null);

                CosmosException.RecordOtelAttributes(exception, default);
                Assert.AreEqual(0, TestEventListener.CapturedEvents.Count);
            }
        }

        private void ValidateExceptionInfo(
            CosmosException exception,
            HttpStatusCode httpStatusCode,
            string substatus,
            string message,
            string activityId,
            double requestCharge,
            double retryAfter)
        {
            Assert.AreEqual(message, exception.ResponseBody);
            Assert.AreEqual(httpStatusCode, exception.StatusCode);
            Assert.AreEqual(int.Parse(substatus), exception.SubStatusCode);
            Assert.AreEqual(substatus, exception.Headers.SubStatusCodeLiteral);
            Assert.AreEqual(activityId, exception.ActivityId);
            Assert.AreEqual(activityId, exception.Headers.ActivityId);
            Assert.AreEqual(requestCharge, exception.RequestCharge);
            Assert.AreEqual(requestCharge, exception.Headers.RequestCharge);
            Assert.AreEqual(TimeSpan.FromMilliseconds(retryAfter), exception.RetryAfter);
            Assert.AreEqual(TimeSpan.FromMilliseconds(retryAfter), exception.Headers.RetryAfter);
            Assert.IsTrue(exception.ToString().Contains(message));
            string expectedMessage = $"Response status code does not indicate success: {httpStatusCode} ({(int)httpStatusCode}); Substatus: {substatus}; ActivityId: {exception.ActivityId}; Reason: ({message});";

            if (httpStatusCode == HttpStatusCode.RequestTimeout
                || httpStatusCode == HttpStatusCode.InternalServerError
                || httpStatusCode == HttpStatusCode.ServiceUnavailable
                || (httpStatusCode == HttpStatusCode.NotFound && exception.Headers.SubStatusCode == SubStatusCodes.ReadSessionNotAvailable))
            {
                expectedMessage += "; Diagnostics:" + new Diagnostics.CosmosTraceDiagnostics(NoOpTrace.Singleton).ToString();
            }

            Assert.AreEqual(expectedMessage, exception.Message);

            // Verify updating the header updates the exception info
            exception.Headers.SubStatusCodeLiteral = "1234";
            Assert.AreEqual(1234, exception.SubStatusCode);
            Assert.AreEqual("1234", exception.Headers.SubStatusCodeLiteral);

            activityId = Guid.NewGuid().ToString();
            exception.Headers.ActivityId = activityId;
            Assert.AreEqual(activityId, exception.ActivityId);
            Assert.AreEqual(activityId, exception.Headers.ActivityId);

            requestCharge = 4321.09;
            exception.Headers.RequestCharge = requestCharge;
            Assert.AreEqual(requestCharge, exception.RequestCharge);
            Assert.AreEqual(requestCharge, exception.Headers.RequestCharge);

            retryAfter = 98754;
            exception.Headers.RetryAfterLiteral = retryAfter.ToString();
            Assert.AreEqual(TimeSpan.FromMilliseconds(retryAfter), exception.RetryAfter);
            Assert.AreEqual(TimeSpan.FromMilliseconds(retryAfter), exception.Headers.RetryAfter);
        }
    }

    internal class TestEventListener : EventListener
    {
        public static List<EventWrittenEventArgs> CapturedEvents { get; } = new List<EventWrittenEventArgs>();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // Add captured event to the list
            CapturedEvents.Add(eventData);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // Enable events from the SampleEventSource
            if (eventSource.Name == "Azure-Cosmos-Operation-Request-Diagnostics")
            {
                this.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
            }
        }
    }
}