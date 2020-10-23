//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Contains extensions to the base Exception class. 
    /// </summary>
    internal static class ExceptionExtensions
    {
        // Iterates through the top level properties of the exception object and
        // converts them to a string so output to logging can be more meaningful. 
        public static string ToLoggingString(this Exception exception)
        {
            PropertyInfo[] properties = exception.GetType().GetProperties();
            IEnumerable<string> fields = properties.Select(
                property => new
                {
                    Name = property.Name,
                    Value = property.GetValue(exception, null)
                }).Select(
                    x => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}",
                        x.Name,
                        x.Value != null ? x.Value.ToString() : string.Empty));

            return string.Concat(exception.GetType(), " : ", string.Join(",", fields));
        }

        // System.Exception.ToString() does not inject information from Exception.Data to the exception string.
        // This extension method allows us to pump additional useful information into the exception data
        // and display it.
        public static string ToStringWithData(this Exception exception)
        {
            StringBuilder sb = new StringBuilder(exception.ToString());

            List<string> exceptionData = new List<string>();
            ExceptionExtensions.CaptureExceptionData(exception, exceptionData);

            if (exceptionData.Count() > 0)
            {
                sb.Append(Environment.NewLine);
                sb.Append("AdditionalData:");

                foreach (string data in exceptionData)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(data);
                }
            }

            return sb.ToString();
        }

        public static string ToStringWithMessageAndData(this Exception exception)
        {
            StringBuilder sb = new StringBuilder(exception.Message);

            List<string> exceptionData = new List<string>();
            ExceptionExtensions.CaptureExceptionData(exception, exceptionData);

            if (exceptionData.Count() > 0)
            {
                sb.Append(Environment.NewLine);
                sb.Append("AdditionalData:");

                foreach (string data in exceptionData)
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(data);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the equivalent exception of the SubStatusCode.
        /// Main motivation for this is because the real error code thrown in a stored procedure call is returned as the SubStatusCode of the BadRequestException.
        /// </summary>
        public static DocumentClientException GetTranslatedStoredProcedureException(DocumentClientException dce)
        {
            if (dce == null)
            {
                return dce;
            }

            if (dce.StatusCode.HasValue)
            {
                SubStatusCodes subStatusCode = dce.GetSubStatus();
                switch (dce.StatusCode.Value)
                {
                    case HttpStatusCode.BadRequest:
                        // Error code thrown in sproc gets returned as subStatusCode of BadRequestException.
                        HttpStatusCode responseCode = (HttpStatusCode)subStatusCode;
                        switch (responseCode)
                        {
                            case HttpStatusCode.BadRequest:
                                return new BadRequestException(dce.Message);
                            case HttpStatusCode.Forbidden:
                                return new ForbiddenException(dce.Message);
                            case HttpStatusCode.NotFound:
                                return new NotFoundException(dce.Message);
                            case HttpStatusCode.RequestTimeout:
                                return new RequestTimeoutException(dce.Message);
                            case HttpStatusCode.Conflict:
                                return new ConflictException(dce.Message);
                            case HttpStatusCode.PreconditionFailed:
                                return new PreconditionFailedException(dce.Message);
                            case HttpStatusCode.RequestEntityTooLarge:
                                return new RequestEntityTooLargeException(dce.Message);
                            case (HttpStatusCode)StatusCodes.RetryWith:
                                return new RetryWithException(dce.Message);
                            case (HttpStatusCode)SubStatusCodes.ConfigurationNameNotFound:
                                return new NotFoundException(dce, SubStatusCodes.ConfigurationNameNotFound);
                            case (HttpStatusCode)SubStatusCodes.ConfigurationNameAlreadyExists:
                                return new ConflictException(dce.Message, SubStatusCodes.ConfigurationNameAlreadyExists);
                            case (HttpStatusCode)SubStatusCodes.ConfigurationNameNotEmpty:
                                return new InternalServerErrorException(dce.Message);
                            case HttpStatusCode.ServiceUnavailable:
                                return new ServiceUnavailableException(dce.Message);
                            case HttpStatusCode.Gone:
                                return new GoneException(dce.Message);

                            default:
                                return dce;
                        }

                    default:
                        return dce;
                }
            }

            return dce;
        }

        private static void CaptureExceptionData(Exception exception, List<string> exceptionData)
        {
            if (exception.Data != null && exception.Data.Count > 0)
            {
                foreach (object key in exception.Data.Keys)
                {
                    exceptionData.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", key.ToString(), exception.Data[key].ToString()));
                }
            }

            if (exception.InnerException != null)
            {
                ExceptionExtensions.CaptureExceptionData(exception.InnerException, exceptionData);
            }
        }
    }
}
