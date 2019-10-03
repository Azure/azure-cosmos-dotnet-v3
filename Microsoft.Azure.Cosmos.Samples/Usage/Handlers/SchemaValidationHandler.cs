using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cosmos.Samples.Handlers
{
    /// <summary>
    /// Exception thrown when we try to write an item that doesn't match the container's schema
    /// </summary>
    class InvalidItemSchemaException : Exception
    {
        public InvalidItemSchemaException(IList<ValidationError> validationErrors) => ValidationErrors = validationErrors;

        public IList<ValidationError> ValidationErrors { get; }
    }

    /// <summary>
    /// This handler validates items being created or updated against a JSON schema (http://json-schema.org/specification.html) 
    /// declared for the corresponding container
    /// </summary>
    class SchemaValidationHandler : RequestHandler
    {
        public SchemaValidationHandler(params (string database, string container, JSchema schema)[] schemas)
        {
            // Pre-build the request URI from the database and container, and use it to index the schema in a dictionary
            _schemas = schemas.ToDictionary(
                t => $"dbs/{t.database}/colls/{t.container}",
                t => t.schema);
        }

        private readonly Dictionary<string, JSchema> _schemas;

        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri.OriginalString;

            if (request.Method == HttpMethod.Post && _schemas.ContainsKey(requestUri))
            {
                // This is an item being created
                ValidateContent(request, _schemas[requestUri]);
            }
            else if (request.Method == HttpMethod.Put && requestUri.Contains("/docs/"))
            {
                var requestUriRoot = requestUri.Substring(0, requestUri.IndexOf("/docs/"));

                if (_schemas.ContainsKey(requestUriRoot))
                {
                    // This is an item being updated
                    ValidateContent(request, _schemas[requestUriRoot]);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }

        private void ValidateContent(RequestMessage request, JSchema schema)
        {
            // Create a StreamReader with leaveOpen = true so it doesn't close the Stream when disposed
            using (var sr = new StreamReader(request.Content, Encoding.UTF8, true, 1024, true))
            {
                var content = sr.ReadToEnd();

                if (!JObject.Parse(content).IsValid(schema, out IList<ValidationError> errors))
                {
                    request.Dispose();
                    throw new InvalidItemSchemaException(errors);
                }
            }
            request.Content.Position = 0;
        }
    }
}
