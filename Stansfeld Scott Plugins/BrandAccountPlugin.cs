using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stansfeld_Scott_Plugins
{
    public class BrandAccountPlugin : PluginBase
    {
        public BrandAccountPlugin() : base(typeof(BrandAccountPlugin)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            if (localcontext == null)
                throw new ArgumentNullException(nameof(localcontext));

            InitProperties(localcontext);

            try
            {
                if (context.MessageName == "Associate")
                {
                    HandleAssociate();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }

        private void HandleAssociate()
        {
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is EntityReference target))
            {
                tracingService.Trace("Target not found.");
                return;
            }

            if (!context.InputParameters.Contains("RelatedEntities") || !(context.InputParameters["RelatedEntities"] is EntityReferenceCollection relatedEntities))
            {
                tracingService.Trace("RelatedEntities not found.");
                return;
            }

            if (!context.InputParameters.Contains("Relationship") || !(context.InputParameters["Relationship"] is Relationship relationship))
            {
                tracingService.Trace("Relationship not found.");
                return;
            }

            if (relationship.SchemaName != "and_account_and_brand")
            {
                tracingService.Trace("Ignoring relationship: {0}", relationship.SchemaName);
                return;
            }

            EntityReference accountRef = null;
            EntityReference brandRef = null;

            // Identify Account and Brand
            if (target.LogicalName == "account")
            {
                accountRef = target;

                foreach (EntityReference related in relatedEntities)
                {
                    if (related.LogicalName == "and_brand")
                    {
                        brandRef = related;
                        break;
                    }
                }
            }
            else if (target.LogicalName == "and_brand")
            {
                brandRef = target;

                foreach (EntityReference related in relatedEntities)
                {
                    if (related.LogicalName == "account")
                    {
                        accountRef = related;
                        break;
                    }
                }
            }

            if (accountRef == null || brandRef == null)
            {
                tracingService.Trace("Account or Brand reference could not be identified.");
                return;
            }

            Entity account = service.Retrieve("account", accountRef.Id, new ColumnSet("ownerid", "and_country"));
            Entity brand = service.Retrieve("and_brand", brandRef.Id, new ColumnSet("and_adiv"));

            EntityReference currentOwner = account.GetAttributeValue<EntityReference>("ownerid");

            if (currentOwner == null)
            {
                tracingService.Trace("Account owner is empty.");
                return;
            }

            tracingService.Trace("Current Owner: {0}, ID: {1}", currentOwner.LogicalName, currentOwner.Id);

            // GET COUNTRY FROM ACCOUNT
            EntityReference countryRef = account.GetAttributeValue<EntityReference>("and_country");

            if (countryRef == null)
            {
                tracingService.Trace("Country is empty on Account.");
                return;
            }

            // GET ADIV FROM BRAND

            OptionSetValue adiv = brand.GetAttributeValue<OptionSetValue>("and_adiv");

            if (adiv == null)
            {
                tracingService.Trace("ADIV is empty on Brand.");
                return;
            }

            // RETRIEVE COUNTRY EMAILS

            Entity country = service.Retrieve("and_country", countryRef.Id, new ColumnSet("and_logisticsemail", "and_chlogisticsemail", "and_wineemail", "and_spiritsemail", "and_sperepemail", "and_chcemail"));

            // GET EMAIL BASED ON ADIV
            string userEmail = GetEmailByADIV(country, adiv);

            tracingService.Trace("Selected User Email: {0}", userEmail ?? "NULL");

            if (string.IsNullOrWhiteSpace(userEmail))
            {
                tracingService.Trace("No user email found for ADIV: {0}", adiv.Value);
                return;
            }

            // GET USER BY EMAIL
            EntityReference userRef = GetUserByEmail(userEmail);

            if (userRef == null)
            {
                tracingService.Trace("No System User found for email: {0}", userEmail);
                return;
            }

            tracingService.Trace("Target User found: {0}", userRef.Id);

            if (IsCRMAdmin(currentOwner))
            {
                tracingService.Trace("Current owner is CRM Admin. Assigning Account.");

                AssignAccount(accountRef, userRef);
            }
            else
            {
                tracingService.Trace("Current owner is NOT CRM Admin. Sharing Account.");

                ShareAccount(accountRef, userRef);
            }
        }

        private string GetEmailByADIV(Entity country, OptionSetValue adiv)
        {
            switch (adiv.Value)
            {
                case 118790000: // CHC
                    return country.GetAttributeValue<string>("and_chcemail");

                //case 118790001: // DIS
                //return country.GetAttributeValue<string>("YOUR_LOGISTICS_EMAIL");

                //case 118790005: // RTI
                //return country.GetAttributeValue<string>("YOUR_LOGISTICS_EMAIL");

                case 118790002: // SPE
                    return country.GetAttributeValue<string>("and_sperepemail");

                case 118790003: // SPI
                    return country.GetAttributeValue<string>("and_spiritsemail");

                case 118790004: // WIN
                    return country.GetAttributeValue<string>("and_wineemail");

                default:
                    return null;
            }
        }

        private EntityReference GetUserByEmail(string email)
        {
            QueryExpression query = new QueryExpression("systemuser");

            query.ColumnSet = new ColumnSet("systemuserid", "fullname", "internalemailaddress");

            query.Criteria.AddCondition("internalemailaddress", ConditionOperator.Equal, email);

            query.Criteria.AddCondition("isdisabled", ConditionOperator.Equal, false);

            EntityCollection users = service.RetrieveMultiple(query);

            if (users.Entities.Count == 0)
            {
                tracingService.Trace("No active user found with email: {0}", email);
                return null;
            }

            Entity user = users.Entities[0];

            tracingService.Trace("User found: {0} ({1})", user.GetAttributeValue<string>("fullname"), user.Id);

            return user.ToEntityReference();
        }

        private bool IsCRMAdmin(EntityReference ownerRef)
        {
            if (ownerRef == null)
                return false;

            if (ownerRef.LogicalName != "systemuser")
                return false;

            Entity owner = service.Retrieve("systemuser", ownerRef.Id, new ColumnSet("fullname", "internalemailaddress"));

            string ownerEmail = owner.GetAttributeValue<string>("internalemailaddress");

            tracingService.Trace("Owner Email: {0}", ownerEmail ?? "NULL");

            if (string.IsNullOrWhiteSpace(ownerEmail))
                return false;

            // Replace with actual CRM Admin email
            return ownerEmail.Equals("crmadmin@stansfeldscott.onmicrosoft.com", StringComparison.OrdinalIgnoreCase);
        }

        private void AssignAccount(EntityReference accountRef, EntityReference userRef)
        {
            tracingService.Trace("Assigning Account {0} to User {1}", accountRef.Id, userRef.Id);

            AssignRequest request = new AssignRequest
            {
                Assignee = userRef,
                Target = accountRef
            };

            service.Execute(request);

            tracingService.Trace("Account successfully assigned.");
        }

        private void ShareAccount(EntityReference accountRef, EntityReference userRef)
        {
            tracingService.Trace("Sharing Account {0} with User {1}", accountRef.Id, userRef.Id);

            GrantAccessRequest grantAccessRequest = new GrantAccessRequest
            {
                PrincipalAccess = new PrincipalAccess
                {
                    Principal = userRef,

                    AccessMask =
                            AccessRights.ReadAccess |
                            AccessRights.WriteAccess |
                            AccessRights.AppendAccess |
                            AccessRights.AppendToAccess
                },

                Target = accountRef
            };

            service.Execute(grantAccessRequest);

            tracingService.Trace("Account successfully shared.");
        }

        private void InitProperties(LocalPluginContext localcontext)
        {
            // Obtain the execution context service from the LocalContext.
            context = localcontext.PluginExecutionContext;
            if (context == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Plugin Execution Context !");
            }

            //Get the Organization Service from the LocalContext
            service = localcontext.OrganizationService;
            if (service == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Organization Service !");
            }

            //Get the Tracing Service from the LocalContext
            tracingService = localcontext.TracingService;
            if (tracingService == null)
            {
                throw new InvalidPluginExecutionException("Failed to retrieve Tracing Service !");
            }
        }
    }
}
