namespace Microsoft.Azure.Cosmos.UT.FakeItEasy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using FakeItEasy;
    using global::FakeItEasy;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ResponseMessageUT
    {
        [TestMethod]
        public void ResponseMessageMock()
        {
            ResponseMessage responseMessage = A.Fake<ResponseMessage>();
            Stream fakeContent = A.Fake<Stream>();
            Headers fakeHeaders = A.Fake<Headers>();
            RequestMessage fakeMessage = A.Fake<RequestMessage>();
            string fakeCt = "some CT";
            string errorMsg = "Some errormsg";

            A.CallTo(() => responseMessage.Content).Returns(fakeContent);
            A.CallTo(() => responseMessage.ContinuationToken).Returns(fakeCt);
            A.CallTo(() => responseMessage.ErrorMessage).Returns(errorMsg);
            A.CallTo(() => responseMessage.Headers).Returns(fakeHeaders);
            A.CallTo(() => responseMessage.IsSuccessStatusCode).Returns(true);
            A.CallTo(() => responseMessage.RequestMessage).Returns(fakeMessage);
            A.CallTo(() => responseMessage.StatusCode).Returns(HttpStatusCode.OK);

            Assert.ReferenceEquals(fakeContent, responseMessage.Content);
            Assert.ReferenceEquals(fakeCt, responseMessage.ContinuationToken);
            Assert.ReferenceEquals(errorMsg, responseMessage.ErrorMessage);
            Assert.ReferenceEquals(fakeHeaders, responseMessage.Headers);
            Assert.IsTrue(responseMessage.IsSuccessStatusCode);
            Assert.ReferenceEquals(fakeMessage, responseMessage.RequestMessage);
            Assert.ReferenceEquals(HttpStatusCode.OK, responseMessage.StatusCode);

            A.CallTo(() => responseMessage.Content).MustHaveHappenedOnceExactly();
            A.CallTo(() => responseMessage.ContinuationToken).MustHaveHappenedOnceExactly();
            A.CallTo(() => responseMessage.ErrorMessage).MustHaveHappenedOnceExactly();
            A.CallTo(() => responseMessage.Headers).MustHaveHappenedOnceExactly();
            A.CallTo(() => responseMessage.IsSuccessStatusCode).MustHaveHappenedOnceExactly();
            A.CallTo(() => responseMessage.RequestMessage).MustHaveHappenedOnceExactly();
            A.CallTo(() => responseMessage.StatusCode).MustHaveHappenedOnceExactly();
        }
    }
}
