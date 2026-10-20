//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a document attachment in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Each document may contain zero or more attachments containing data of arbitrary formats like images, binary or large text blobs. 
    /// The Attachment class represents the Azure Cosmos DB resource used to store information about the attachment like its location and 
    /// MIME content type. The payload itself ("Media") is referenced through the MediaLink property. The Attachment class is a DynamicObject 
    /// and can contain any custom metadata to be persisted. 
    /// 
    /// Attachments can be created as managed or unmanaged. If attachments are created as managed through Azure Cosmos DB, then it is assigned a system 
    /// generated mediaLink. Azure Cosmos DB then automatically performs garbage collection on the media when parent document is deleted.
    /// 
    /// You can reuse the mediaLink property to store an external location e.g., a file share or an Azure Blob Storage URI. 
    /// Azure Cosmos DB will not perform garbage collection on mediaLinks for external locations.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    class Attachment : Resource, IDynamicMetaObjectProvider
    {
        /// <summary>
        /// Initializes a new instance of an <see cref="Attachment"/> class for the Azure Cosmos DB service.
        /// </summary>
        public Attachment()
        {

        }
        
        /// <summary>
        /// Gets or sets the MIME content type of the attachment in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The MIME content type of the attachment.
        /// </value>
        /// <remarks>For example, set to "text/plain" for text files, "image/jpeg" for images.</remarks>
        [JsonProperty(PropertyName = Constants.Properties.ContentType)]
        public string ContentType
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ContentType);
            }
            set
            {
                base.SetValue(Constants.Properties.ContentType, value);
            }
        }        

        /// <summary>
        /// Gets or sets the media link associated with the attachment content in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The media link associated with the attachment content.
        /// </value>
        /// <remarks>Azure Cosmos DB supports both managed and unmanaged attachments.</remarks>
        [JsonProperty(PropertyName = Constants.Properties.MediaLink)]
        public string MediaLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.MediaLink);
            }
            set
            {
                base.SetValue(Constants.Properties.MediaLink, value);
            }
        }

        //Helper to materialize Attachment from any .NET object.
        internal static Attachment FromObject(object attachment, JsonSerializerSettings settings = null)
        {
            if (attachment != null)
            {
                if (typeof(Attachment).IsAssignableFrom(attachment.GetType()))
                {
                    return (Attachment)attachment;
                }
                else
                {
                    JObject serializedPropertyBag = JObject.FromObject(attachment);
                    Attachment typeAttachment = new Attachment();
                    typeAttachment.propertyBag = serializedPropertyBag;
                    typeAttachment.SerializerSettings = settings;
                    return typeAttachment;
                }
            }
            return null;
        }

        //Property Getter/Setter for Expandable User property.
        private object GetProperty(
            string propertyName, Type returnType)
        {
            if (this.propertyBag != null)
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

        private T AsType<T>() //To convert Attachment to any type.
        {
            if (typeof(T) == typeof(Attachment) || typeof(T) == typeof(object))
            {
                return (T)(object)this;
            }

            if (this.propertyBag == null)
            {
                return default(T);
            }

            //Materialize the type.
            T result = this.SerializerSettings == null ?
                (T)this.propertyBag.ToObject<T>() :
                (T)this.propertyBag.ToObject<T>(JsonSerializer.Create(this.SerializerSettings));

            return result;
        }

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
        {
            return new AttachmentDynamicMetaObject(this, parameter);
        }

        private class AttachmentDynamicMetaObject : DynamicMetaObject
        {
            private readonly Attachment attachment;

            public AttachmentDynamicMetaObject(Attachment attachment, Expression expression)
                : base(expression, BindingRestrictions.Empty, attachment)
            {
                this.attachment = attachment;
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                if (AttachmentDynamicMetaObject.IsResourceProperty(binder.Name))
                {
                    return base.BindGetMember(binder); //Base will bind it statically.
                }

                //For all non-resource property, thunk the call to GetProperty method in Attachment.
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
                    typeof(Attachment).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance),
                    parameters);

                DynamicMetaObject getProperty = new DynamicMetaObject(
                    getPropertyExpression,
                    BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType));

                return getProperty;
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                if (AttachmentDynamicMetaObject.IsResourceProperty(binder.Name))
                {
                    return base.BindSetMember(binder, value); //Base will bind it statically.
                }

                //For all non-resource property, thunk the call to this.SetProperty method in Attachment.
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
                    typeof(Attachment).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance),
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
                    typeof(Attachment).GetMethod("AsType",
                    BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(binder.Type));

                //Create a meta object to invoke AsType later.
                DynamicMetaObject castOperator = new DynamicMetaObject(
                    methodExpression,
                    BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType));

                return castOperator;
            }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                List<string> dynamicMembers = new List<string>();
                foreach (KeyValuePair<string, JToken> pair in this.attachment.propertyBag)
                {
                    // exclude the resource property and static properties
                    if (!IsResourceSerializedProperty(pair.Key))
                    {
                        dynamicMembers.Add(pair.Key);
                    }

                }
                return dynamicMembers;
            }

            internal static bool IsResourceSerializedProperty(string propertyName)
            {
                if (propertyName == Constants.Properties.Id ||
                    propertyName == Constants.Properties.RId ||
                    propertyName == Constants.Properties.ETag ||
                    propertyName == Constants.Properties.LastModified ||
                    propertyName == Constants.Properties.SelfLink ||
                    propertyName == Constants.Properties.ContentType ||
                    propertyName == Constants.Properties.MediaLink)
                {
                    return true;
                }
                return false;
            }

            //List of Static Property in Attachment which we dont want to thunk.
            internal static bool IsResourceProperty(string propertyName)
            {
                if (propertyName == "Id" ||
                    propertyName == "ResourceId" ||
                    propertyName == "ETag" ||
                    propertyName == "Timestamp" ||
                    propertyName == "SelfLink" ||
                    propertyName == "MediaLink" ||
                    propertyName == "ContentType")
                {
                    return true;
                }
                return false;
            }
        }
           
    }
}
