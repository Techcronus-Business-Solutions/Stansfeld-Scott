using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stansfeld_Scott_Plugins
{
    public class AccountPlugin : PluginBase
    {
        public AccountPlugin() : base(typeof(AccountPlugin)) { }

        #region Private Variables
        private IOrganizationService service { get; set; }
        private IPluginExecutionContext context { get; set; }
        private ITracingService tracingService { get; set; }
        private Entity targetEntity { get; set; }
        #endregion

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            if (localcontext == null)
                throw new ArgumentNullException(nameof(localcontext));

            InitProperties(localcontext);

            try
            {
                if (context.InputParameters.Contains(CONST_TARGETENTITY) && context.InputParameters[CONST_TARGETENTITY] is Entity)
                {
                    targetEntity = (Entity)context.InputParameters[CONST_TARGETENTITY];
                    if (targetEntity.LogicalName == "entity")
                    {
                        if (context.Stage == PreOperation && context.MessageName == CONST_CREATE)
                        {
                            // apply logic
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.Trace("ColorPlugin Exception: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error in ColorPlugin: {ex.Message}");
            }
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
