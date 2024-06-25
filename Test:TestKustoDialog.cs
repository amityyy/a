using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.CloudMine.Bot.Configuration;
using Microsoft.CloudMine.Bot.Utilities.Kusto;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.Bot.Dialogs.Test
{
    public class TestKustoDialog : ComponentDialog
    {
        private const string serviceId = "5e8efbe2-88da-4bf8-86eb-2b013c315106";
        public BotConfiguration BotConfig { get; }

        public TestKustoDialog(BotConfiguration botConfig)
            : base(nameof(TestKustoDialog))
        {
            BotConfig = botConfig;
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                TestKusto,
                TestServiceTree
            }));
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> TestKusto(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string[] managementChain = await KustoUtils.GetEmployeeManagementAliasHierarchy("kimh", BotConfig).ConfigureAwait(false);
            string response = $"Management chain for kimh: {string.Join("/", managementChain)}";
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
            return await stepContext.NextAsync(cancellationToken);
        }

        private async Task<DialogTurnResult> TestServiceTree(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            Guid id = new Guid(serviceId);
            List<Tuple<Guid, string>> servicesFound = await KustoUtils.ValidateServiceTreeId(id, BotConfig).ConfigureAwait(false);
            string response = "";

            if (servicesFound.Count < 1)
            {
                response = $"Could not find ServiceTree associated with ID {id}. Please ensure you have the correct ID.";
            }
            else if (servicesFound.Count > 1)
            {
                response = $"Found multiple services associated with ID {id}. We are unable to process your request at this time.";
            }
            else
            {
                response = $"Service name is {servicesFound[0].Item2}.";
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
            return await stepContext.NextAsync(cancellationToken);
        }
    }
}
