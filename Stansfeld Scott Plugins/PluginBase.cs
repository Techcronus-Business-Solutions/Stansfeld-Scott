using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Globalization;
using System.ServiceModel;

namespace Stansfeld_Scott_Plugins
{
    /// <summary>
    /// Base class for all plug-in classes.
    /// </summary>    
    public abstract class PluginBase : IPlugin
    {
        protected const string CONST_TARGETENTITY = "Target";
        /// <summary>
        /// Alias of the image registered for the snapshot of the 
        /// primary entity's attributes after the core platform operation executes.
        /// 
        /// Note: Only synchronous post-event and asynchronous registered plug-ins
        /// have PostEntityImages populated.
        /// </summary>
        protected const string CONST_POSTIMAGENAME = "PostImage";
        /// <summary>
        /// Alias of the image registered for the snapshot of the 
        /// primary entity's attributes before the core platform operation executes.
        /// </summary>
        protected const string CONST_PREIMAGENAME = "PreImage";

        #region Messages
        protected const string CONST_CREATE = "Create";
        protected const string CONST_UPDATE = "Update";
        protected const string CONST_DELETE = "Delete";
        protected const string CONST_ASSOCIATE = "Associate";
        protected const string CONST_DISASSOCIATE = "Disassociate";
        protected const string CONST_RETRIEVE = "Retrieve";
        protected const string CONST_RETRIEVEMULTIPLE = "RetrieveMultiple";
        protected const string CONST_ASSIGN = "Assign";
        #endregion

        public const int PreValidation = 10;
        public const int PreOperation = 20;
        public const int PostOperation = 40;

        /// <summary>
        /// Plug-in context object. 
        /// </summary>
        protected class LocalPluginContext
        {
            internal IServiceProvider ServiceProvider { get; private set; }

            /// <summary>
            /// The Microsoft Dynamics CRM organization service.
            /// </summary>
            internal IOrganizationService OrganizationService { get; private set; }

            /// <summary>
            /// IPluginExecutionContext contains information that describes the run-time environment in which the plug-in executes, information related to the execution pipeline, and entity business information.
            /// </summary>
            internal IPluginExecutionContext PluginExecutionContext { get; private set; }

            /// <summary>
            /// Synchronous registered plug-ins can post the execution context to the Microsoft Azure Service Bus. <br/> 
            /// It is through this notification service that synchronous plug-ins can send brokered messages to the Microsoft Azure Service Bus.
            /// </summary>
            internal IServiceEndpointNotificationService NotificationService { get; private set; }

            /// <summary>
            /// Provides logging run-time trace information for plug-ins. 
            /// </summary>
            internal ITracingService TracingService { get; private set; }

            private LocalPluginContext() { }

            /// <summary>
            /// Helper object that stores the services available in this plug-in.
            /// </summary>
            /// <param name="serviceProvider"></param>
            internal LocalPluginContext(IServiceProvider serviceProvider)
            {
                if (serviceProvider == null)
                {
                    throw new ArgumentNullException("serviceProvider");
                }

                // Obtain the execution context service from the service provider.
                PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

                // Obtain the tracing service from the service provider.
                TracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                // Get the notification service from the service provider.
                NotificationService = (IServiceEndpointNotificationService)serviceProvider.GetService(typeof(IServiceEndpointNotificationService));

                // Obtain the organization factory service from the service provider.
                IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

                // Use the factory to generate the organization service.
                OrganizationService = factory.CreateOrganizationService(PluginExecutionContext.UserId);
            }

            /// <summary>
            /// Writes a trace message to the CRM trace log.
            /// </summary>
            /// <param name="message">Message name to trace.</param>
            internal void Trace(string message)
            {
                if (string.IsNullOrWhiteSpace(message) || TracingService == null)
                {
                    return;
                }

                if (PluginExecutionContext == null)
                {
                    TracingService.Trace(message);
                }
                else
                {
                    TracingService.Trace(
                        "{0}, Correlation Id: {1}, Initiating User: {2}",
                        message,
                        PluginExecutionContext.CorrelationId,
                        PluginExecutionContext.InitiatingUserId);
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the child class.
        /// </summary>
        /// <value>The name of the child class.</value>
        protected string ChildClassName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginBase"/> class.
        /// </summary>
        /// <param name="childClassName">The <see cref=" cred="Type"/> of the derived class.</param>
        internal PluginBase(Type childClassName)
        {
            ChildClassName = childClassName.ToString();
        }

        /// <summary>
        /// Main entry point for he business logic that the plug-in is to execute.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <remarks>
        /// For improved performance, Microsoft Dynamics CRM caches plug-in instances. 
        /// The plug-in's Execute method should be written to be stateless as the constructor 
        /// is not called for every invocation of the plug-in. Also, multiple system threads 
        /// could execute the plug-in at the same time. All per invocation state information 
        /// is stored in the context. This means that you should not use global variables in plug-ins.
        /// </remarks>
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            // Construct the local plug-in context.
            LocalPluginContext localcontext = new LocalPluginContext(serviceProvider);

            localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Entered {0}.Execute()", this.ChildClassName));

            try
            {
                // Invoke the custom implementation 
                ExecuteCrmPlugin(localcontext);
                // now exit - if the derived plug-in has incorrectly registered overlapping event registrations,
                // guard against multiple executions.
                return;
            }
            catch (FaultException<OrganizationServiceFault> e)
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", e.ToString()));

                // Handle the exception.
                throw;
            }
            finally
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exiting {0}.Execute()", this.ChildClassName));
            }
        }

        /// <summary>
        /// Placeholder for a custom plug-in implementation. 
        /// </summary>
        /// <param name="localcontext">Context for the current plug-in.</param>
        protected virtual void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            // Do nothing. 
        }

        /// <summary>
        /// Gets formatted exception string for the trace
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <returns></returns>
        public static string GetFormattedExceptionTraceString(Exception ex)
        {
            return string.Format("Exception name = {0}, Message = {1}, StackTrace = {2}.", ex.GetType().FullName, ex.Message, ex.ToString());
        }

        public string GetStringAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = string.Empty;
            if (entity.Contains(attributeName))
                attributeValue = entity[attributeName] as string;
            return attributeValue;
        }

        public static int GetIntAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = 0;
            if (entity.Contains(attributeName) && entity[attributeName] is int)
                attributeValue = (int)entity[attributeName];
            return attributeValue;
        }
        public double GetDoubleAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = 0.00;
            if (entity.Contains(attributeName) && entity[attributeName] is double)
                attributeValue = (double)entity[attributeName];
            return attributeValue;
        }

        public static decimal GetDecimalAttributeValue(Entity entity, string attributeName)
        {
            decimal attributeValue = 0;
            if (entity.Contains(attributeName) && entity[attributeName] is decimal)
                attributeValue = (decimal)entity[attributeName];
            return attributeValue;
        }
        public float GetFloatAttributeValue(Entity entity, string attributeName)
        {
            float attributeValue = 0;
            //if (entity.Contains(attributeName) && entity[attributeName] is float)
            //    attributeValue = (float)entity[attributeName];
            if (entity.Contains(attributeName))
                attributeValue = Convert.ToInt32(entity[attributeName]);
            return attributeValue;
        }

        public static decimal GetMoneyAttributeValue(Entity entity, string attributeName)
        {
            decimal attributeValue = 0.0m;
            if (entity.Contains(attributeName) && entity[attributeName] is Money)
                return ((Money)entity[attributeName]).Value;
            return attributeValue;
        }

        public DateTime GetDateTimeAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = new DateTime();
            if (entity.Contains(attributeName))
                attributeValue = entity[attributeName] is DateTime ? (DateTime)entity[attributeName] : new DateTime();
            return attributeValue;
        }

        public bool GetBoolAttributeValue(Entity entity, string attributeName)
        {
            var attributeValue = false;
            if (entity.Contains(attributeName) && (entity[attributeName] is bool && (bool)entity[attributeName]))
                attributeValue = (bool)entity[attributeName];
            return attributeValue;
        }

        public static EntityReference GetLookupAttributeValue(Entity entity, string attributeName)
        {
            if (entity == null) return null;
            EntityReference attributeValue = null;
            if (entity.Contains(attributeName) && (entity[attributeName] is EntityReference))
                attributeValue = (EntityReference)entity[attributeName];
            return attributeValue;
        }

        public Guid GetPrimaryKeyAttributeValue(Entity entity)
        {
            return entity != null ? entity.Id : new Guid();
        }

        public int? GetOptionSetAttributeValue(Entity entity, string attributeName)
        {
            int? attributeValue = null;
            if (!entity.Contains(attributeName) || (!(entity[attributeName] is OptionSetValue))) return attributeValue;
            var optionSetValue = entity[attributeName] as OptionSetValue;
            attributeValue = optionSetValue.Value;
            return attributeValue;
        }

        public ExecuteMultipleRequest GetExeculteMultipleRequest()
        {
            var multipleExecuteRequest = new ExecuteMultipleRequest
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = true,
                    ReturnResponses = true
                },
                Requests = new OrganizationRequestCollection()
            };

            return multipleExecuteRequest;
        }
    }
}
