//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a document in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks> 
    /// A document is a structured JSON document. There is no set schema for the JSON documents, and a document may contain any 
    /// number of custom properties as well as an optional list of attachments. Document is an application resource and can be
    /// authorized using the master key or resource keys.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Document : Resource, IDynamicMetaObjectProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Document"/> class for the Azure Cosmos DB service.
        /// </summary>
        public Document()
        {

        }        
        
        /// <summary>
        /// Gets the self-link corresponding to attachments of the document from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The self-link corresponding to attachments of the document.
        /// </value>
        /// <remarks>
        /// Every document can have between zero and many attachments. The attachments link contains a feed of attachments that belong to 
        /// the document.
        /// </remarks>
        public string AttachmentsLink
        {
            get
            {
                return this.SelfLink.TrimEnd('/') + "/" + base.GetValue<string>(Constants.Properties.AttachmentsLink);
            }
        }

        /// <summary>
        /// Gets or sets the time to live in seconds of the document in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// It is an optional property. 
        /// A valid value must be either a nonzero positive integer, '-1', or <c>null</c>.
        /// By default, TimeToLive is set to null meaning the document inherits the collection's <see cref="DocumentCollection.DefaultTimeToLive"/>.
        /// The unit of measurement is seconds. The maximum allowed value is 2147483647.
        /// When the value is '-1', it means never expire regardless of the collection's <see cref="DocumentCollection.DefaultTimeToLive"/> value.
        /// </value>
        /// <remarks>
        /// <para>
        /// The final time-to-live policy of a document is evaluated after consulting the collection's
        /// <see cref="DocumentCollection.DefaultTimeToLive"/>.
        /// </para>
        /// <para>
        /// When the <see cref="TimeToLive"/> is <c>null</c>, the document inherits the collection's
        /// <see cref="DocumentCollection.DefaultTimeToLive"/>.
        /// If the collection's <see cref="DocumentCollection.DefaultTimeToLive"/> is a nonzero positive integer,
        /// then the document will inherit that value as its time-to-live in seconds, and will be expired
        /// after the default time-to-live in seconds since its last write time. The expired documents will be deleted in background.
        /// Otherwise, the document will never expire.
        /// </para>
        /// <para>
        /// When the <see cref="TimeToLive"/> is '-1', the document will never expire regardless of the collection's
        /// <see cref="DocumentCollection.DefaultTimeToLive"/> value.
        /// </para>
        /// <para>
        /// When the <see cref="TimeToLive"/> is a nonzero positive integer, need to check the collection's
        /// <see cref="DocumentCollection.DefaultTimeToLive"/>.
        /// If the collection's <see cref="DocumentCollection.DefaultTimeToLive"/> is <c>null</c>, which means the time-to-live
        /// has been turned off on the collection, and the document's <see cref="TimeToLive"/> should be disregarded and the document
        /// will never expire.
        /// Otherwise, the document's <see cref="TimeToLive"/> will be honored. The document will be expired
        /// after the default time-to-live in seconds since its last write time. The expired documents will be deleted in background.
        /// </para>
        /// <para>
        /// The table below shows an example of the matrix to evaluate the final time-to-live policy given a collection's
        /// <see cref="DocumentCollection.DefaultTimeToLive"/> and a document's <see cref="TimeToLive"/>.
        /// </para>
        /// <list type="table">
        /// <listheader>
        /// <term>Collection</term>
        /// <description>Matrix</description>
        /// </listheader>
        /// <item>
        /// <term>DefaultTimeToLive = null</term>
        /// <description>
        /// <list type="table">
        /// <listheader>
        /// <term>Document</term>
        /// <description>Result</description>
        /// </listheader>
        /// <item>
        /// <term>TimeToLive = null</term>
        /// <description>TTL is disabled. The document will never expire (default).</description>
        /// </item>
        /// <item>
        /// <term>TimeToLive = -1</term>
        /// <description>TTL is disabled. The document will never expire.</description>
        /// </item>
        /// <item>
        /// <term>TimeToLive = 2000</term>
        /// <description>TTL is disabled. The document will never expire.</description>
        /// </item>
        /// </list>
        /// </description>
        /// </item>
        /// <item>
        /// <term>DefaultTimeToLive = -1</term>
        /// <description>
        /// <list type="table">
        /// <listheader>
        /// <term>Document</term>
        /// <description>Result</description>
        /// </listheader>
        /// <item>
        /// <term>TimeToLive = null</term>
        /// <description>TTL is enabled. The document will never expire (default).</description>
        /// </item>
        /// <item>
        /// <term>TimeToLive = -1</term>
        /// <description>TTL is enabled. The document will never expire.</description>
        /// </item>
        /// <item>
        /// <term>TimeToLive = 2000</term>
        /// <description>TTL is enabled. The document will expire after 2000 seconds.</description>
        /// </item>
        /// </list>
        /// </description>
        /// </item>
        /// <item>
        /// <term>DefaultTimeToLive = 1000</term>
        /// <description>
        /// <list type="table">
        /// <listheader>
        /// <term>Document</term>
        /// <description>Result</description>
        /// </listheader>
        /// <item>
        /// <term>TimeToLive = null</term>
        /// <description>TTL is enabled. The document will expire after 1000 seconds (default).</description>
        /// </item>
        /// <item>
        /// <term>TimeToLive = -1</term>
        /// <description>TTL is enabled. The document will never expire.</description>
        /// </item>
        /// <item>
        /// <term>TimeToLive = 2000</term>
        /// <description>TTL is enabled. The document will expire after 2000 seconds.</description>
        /// </item>
        /// </list>
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <example>
        /// The example below removes 'ttl' from document content.
        /// The document will inherit the collection's <see cref="DocumentCollection.DefaultTimeToLive"/> as its time-to-live value.
        /// <code language="c#">
        /// <![CDATA[
        ///     document.TimeToLive = null;
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below ensures that the document should never expire regardless.
        /// <code language="c#">
        /// <![CDATA[
        ///     document.TimeToLive = -1;
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below sets the time-to-live in seconds on a document.
        /// The document will expire after 1000 seconds since its last write time when the collection's <see cref="DocumentCollection.DefaultTimeToLive"/>
        /// is not <c>null</c>.
        /// <code language="c#">
        /// <![CDATA[
        ///     document.TimeToLive = 1000;
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>     
        [JsonProperty(PropertyName = Constants.Properties.TimeToLive, NullValueHandling = NullValueHandling.Ignore)]
        public int? TimeToLive
        {
            get
            {
                return base.GetValue<int?>(Constants.Properties.TimeToLive);
            }
            set
            {
                base.SetValue(Constants.Properties.TimeToLive, value);
            }
        }

        //Helper to materialize Document from any .NET object.
        internal static Document FromObject(object document, JsonSerializerSettings settings = null)
        {
            if(document != null)
            {
                if (typeof(Document).IsAssignableFrom(document.GetType()))
                {
                    return (Document)document;
                }
                else
                {
                    // FromObject: for dynamics, it only go through JsonProperty attribute decorated properties.
                    //             for poco, it will go through all public properties
                    JObject serializedPropertyBag = (settings == null) ?
                        JObject.FromObject(document) :
                        JObject.FromObject(document, JsonSerializer.Create(settings));
                    Document typeDocument = new Document() { SerializerSettings = settings };

                    typeDocument.propertyBag = serializedPropertyBag;
                    return typeDocument;
                }
            }
            return null;
        }
        
        //Property Getter/Setter for Expandable User property.
        private object GetProperty(
            string propertyName, Type returnType)
        {
            if(this.propertyBag != null)
            {
                JToken token = this.propertyBag[propertyName];
                if (token != null)
                {
                    return this.SerializerSettings == null ? 
                        token.ToObject(returnType) :
                        token.ToObject(returnType, JsonSerializer.Create(this.SerializerSettings));
                }
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
                if (this.propertyBag == null)
                {
                    this.propertyBag = new JObject();
                }
                this.propertyBag[propertyName] = JToken.FromObject(value);
            }
            else
            {
                if (this.propertyBag != null)
                {
                    this.propertyBag.Remove(propertyName);
                }
            }
            return value;
        }

        private T AsType<T>() //To convert Document to any type.
        {
            if (typeof(T) == typeof(Document) || typeof(T) == typeof(object))
            {
                return (T)(object)this;
            }

            if (this.propertyBag == null)
            {
                return default(T);
            }

            //Materialize the type.
            return (this.SerializerSettings == null) ?
                (T)this.propertyBag.ToObject<T>() :
                (T)this.propertyBag.ToObject<T>(JsonSerializer.Create(this.SerializerSettings));
        }
        
        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
        {
            return new DocumentDynamicMetaObject(this, parameter);
        }

        private class DocumentDynamicMetaObject : DynamicMetaObject
        {
            private readonly Document document;
            public DocumentDynamicMetaObject(Document document, Expression expression)             
                : base(expression, BindingRestrictions.Empty, document)
            {
                this.document = document;
            }            

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                if (DocumentDynamicMetaObject.IsResourceProperty(binder.Name))
                {
                    return base.BindGetMember(binder); //Base will bind it statically.
                }

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
                    typeof(Document).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance),
                    parameters);
 
                DynamicMetaObject getProperty = new DynamicMetaObject(
                    getPropertyExpression,
                    BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType));

                return getProperty;
            }      

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                if (DocumentDynamicMetaObject.IsResourceProperty(binder.Name))
                {
                    return base.BindSetMember(binder, value); //Base will bind it statically.
                }

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
                    typeof(Document).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance),
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
                    typeof(Document).GetMethod("AsType",
					BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(binder.Type));

                //Create a meta object to invoke AsType later.
                DynamicMetaObject castOperator = new DynamicMetaObject(
                    methodExpression,
                    BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType));

                return castOperator;         
            }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                // Here we don't enumerate this.document.propertyBag, because in Document.FromObject,
                // we merge the static defined property into propertyBag
                List<string> dynamicMembers = new List<string>();

                foreach (KeyValuePair<string, JToken> pair in this.document.propertyBag)
                {
                    // exclude the resource property
                    if (!IsResourceSerializedProperty(pair.Key))
                    {
                        dynamicMembers.Add(pair.Key);
                    }
                }

                return dynamicMembers.ToList();
            }

            internal static bool IsResourceSerializedProperty(string propertyName)
            {
                if (propertyName == Constants.Properties.Id ||
                    propertyName == Constants.Properties.RId ||
                    propertyName == Constants.Properties.ETag ||
                    propertyName == Constants.Properties.LastModified ||
                    propertyName == Constants.Properties.SelfLink ||
                    propertyName == Constants.Properties.AttachmentsLink ||
                    propertyName == Constants.Properties.TimeToLive)
                {
                    return true;
                }
                return false;
            }

            //List of Static Property in Document which we dont want to thunk.
            internal static bool IsResourceProperty(string propertyName)
            {
                if (propertyName == "Id" ||
                    propertyName == "ResourceId" ||
                    propertyName == "ETag" ||
                    propertyName == "Timestamp" ||
                    propertyName == "SelfLink" ||
                    propertyName == "AttachmentsLink" ||
                    propertyName == "TimeToLive")
                {
                    return true;
                }
                return false;
            }
        }
    }
}
