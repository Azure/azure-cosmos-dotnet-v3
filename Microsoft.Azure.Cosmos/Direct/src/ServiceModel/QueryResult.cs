//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    //Each object record in Query response is materialized as QueryResult.
    //This allows us to convert them to any type via dynamic cast.
    internal sealed class QueryResult : IDynamicMetaObjectProvider
    {
        private readonly JContainer jObject;
        private readonly string ownerFullName;
        private JsonSerializer jsonSerializer;

        public QueryResult(JContainer jObject, string ownerFullName, JsonSerializer jsonSerializer)
        {
            this.jObject = jObject;
            this.ownerFullName = ownerFullName;
            this.jsonSerializer = jsonSerializer;
        }

        public QueryResult(JContainer jObject, string ownerFullName, JsonSerializerSettings serializerSettings = null)
            : this(jObject, ownerFullName, serializerSettings != null ? JsonSerializer.Create(serializerSettings) : JsonSerializer.Create())
        {
        }

        /// <summary>
        /// Gets the raw payload of this object.
        /// To avoid double deserializations.
        /// </summary>
        public JContainer Payload
        {
            get
            {
                return this.jObject;
            }
        }

        public string OwnerFullName
        {
            get
            {
                return this.ownerFullName;
            }
        }

        public JsonSerializer JsonSerializer
        {
            get
            {
                return this.jsonSerializer;
            }
        }

        public override string ToString()
        {
            using (StringWriter writer = new StringWriter())
            {
                jsonSerializer.Serialize(writer, jObject);
                return writer.ToString();
            }
        }

        // Summary:
        //     Returns the enumeration of all dynamic member names.
        //
        // Returns:
        //     A sequence that contains dynamic member names.
        private IEnumerable<string> GetDynamicMemberNames()
        {
            // Here we don't enumerate this.document.propertyBag, because in Document.FromObject,
            // we merge the static defined property into propertyBag
            List<string> dynamicMembers = new List<string>();

            JObject jObjectLocal = this.jObject as JObject;
            if (jObjectLocal != null)
            {
                foreach (KeyValuePair<string, JToken> pair in jObjectLocal)
                {
                    dynamicMembers.Add(pair.Key);
                }
            }
            return dynamicMembers.ToList();
        }

        private object Convert(Type type)
        {
            object result;
            if (type == typeof(object))
            {
                return this;
            }

            if (type == typeof(Database))
            {
                result = new Database() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(DocumentCollection))
            {
                result = new DocumentCollection() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(User))
            {
                result = new User() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(UserDefinedType))
            {
                result = new UserDefinedType() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(Permission))
            {
                result = new Permission() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(Attachment))
            {
                result = new Attachment() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(Document))
            {
                result = new Document() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(Conflict))
            {
                result = new Conflict() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(Trigger))
            {
                result = new Trigger() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(Offer))
            {
                result = OfferTypeResolver.ResponseOfferTypeResolver.Resolve(this.jObject as JObject);
            }
            else if (typeof(Document).IsAssignableFrom(type))
            {
                result = (Resource)((this.jsonSerializer == null) ?
                    this.jObject.ToObject(type) :
                    this.jObject.ToObject(type, this.jsonSerializer));
                ((Document)result).propertyBag = this.jObject as JObject;
            }
            else if (typeof(Attachment).IsAssignableFrom(type))
            {
                result = (Resource)this.jObject.ToObject(type);
                ((Attachment)result).propertyBag = this.jObject as JObject;
            }
            else if (type == typeof(Schema))
            {
                result = new Schema() { propertyBag = this.jObject as JObject };
            }
            else if (type == typeof(Snapshot))
            {
                result = new Snapshot() { propertyBag = this.jObject as JObject };
            }
            else
            {
                // result 
                result = (this.jsonSerializer == null) ?
                    this.jObject.ToObject(type) :
                    this.jObject.ToObject(type, this.jsonSerializer);
            }

            Resource resource = result as Resource;
            if (resource != null)
            {
                resource.AltLink = PathsHelper.GeneratePathForNameBased(type, this.ownerFullName, resource.Id);
            }
            return result;
        }

        //Property Getter/Setter for Expandable User property.
        private object GetProperty(
            string propertyName, Type returnType)
        {
            JToken token = this.jObject[propertyName];
            if (token != null)
            {
                return token.ToObject(returnType);
            }

            //Any property not in JSON throw exception rather than returning null.
            throw new DocumentClientException(
                string.Format(CultureInfo.CurrentUICulture,
                RMResources.PropertyNotFound,
                propertyName), null, null);
        }

        private object SetProperty(string propertyName, object value)
        {
            if (value != null)
            {
                this.jObject[propertyName] = JToken.FromObject(value);
                return true;
            }

            return value;
        }

        private T AsType<T>() //To convert Document to any type.
        {
            return (T)this.Convert(typeof(T));
        }

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
        {
            return new DocumentDynamicMetaObject(this, parameter);
        }

        private class DocumentDynamicMetaObject : DynamicMetaObject
        {
            private readonly QueryResult queryResult;

            public DocumentDynamicMetaObject(QueryResult queryResult, Expression expression)
                : base(expression, BindingRestrictions.Empty, queryResult)
            {
                this.queryResult = queryResult;
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                //For all non-resource property, thunk the call to GetProperty method in Document.
                string methodName = "GetProperty";

                //Two parameters.
                Expression[] parameters = new Expression[]
                {
                    Expression.Constant(binder.Name), //PropertyName
                    Expression.Constant(binder.ReturnType) //Expected Return Type.
                };

                //Expression: this.
                Expression thisExpression = Expression.Convert(this.Expression, this.LimitType);

                //Expression: this.GetProperty(name, returnType);
                Expression getPropertyExpression = Expression.Call(
                    thisExpression,
                    typeof(QueryResult).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance),
                    parameters);

                DynamicMetaObject getProperty = new DynamicMetaObject(
                    getPropertyExpression,
                    BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType));

                return getProperty;
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                //For all non-resource property, thunk the call to this.SetProperty method in Document.
                string methodName = "SetProperty";

                // setup the binding restrictions.
                BindingRestrictions restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

                //Two params, Name & Value.
                Expression[] args = new Expression[2];
                args[0] = Expression.Constant(binder.Name);
                args[1] = Expression.Convert(value.Expression, typeof(object));

                // Setup the 'this' reference
                Expression self = Expression.Convert(this.Expression, this.LimitType);

                // Setup the method call expression
                Expression methodCall = Expression.Call(self,
                    typeof(QueryResult).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance),
                    args);

                // Create a meta object to invoke Set later:
                DynamicMetaObject setProperty = new DynamicMetaObject(
                    methodCall,
                    restrictions);
                // return that dynamic object
                return setProperty;
            }

            public override DynamicMetaObject BindConvert(ConvertBinder binder)
            {
                // Setup the 'this' reference
                Expression self = Expression.Convert(this.Expression, this.LimitType);

                MethodCallExpression methodExpression = Expression.Call(
                    self,
                    typeof(QueryResult).GetMethod("AsType",
                    BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(binder.Type));

                //Create a meta object to invoke AsType later.
                DynamicMetaObject castOperator = new DynamicMetaObject(
                    methodExpression,
                    BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType));

                return castOperator;
            }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                return this.queryResult.GetDynamicMemberNames();
            }
        }

    }
}
