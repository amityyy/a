using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.CloudMine.Bot.Configuration;
using Microsoft.CloudMine.Bot.Model;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class AppAccessMainDialog : ComponentDialog
    {
        public readonly BotConfiguration BotConfig;
        public AppRegistrationData RegData { get; private set; }

        private const string extendAccess = "Extend existing access";
        private const string modifyAccess = "Modify existing access";
        private const string newAccess = "New access request";
        private static readonly string[] menuOptions = { newAccess, extendAccess, modifyAccess };

        public AppAccessMainDialog(BotConfiguration botconfig) : base(nameof(AppAccessMainDialog))
        {
            BotConfig = botconfig;
            RegData = new AppRegistrationData();
            
            AddDialog(new NewAppAccessRequestDialog(botconfig));
            AddDialog(new EmailDialog());
            AddDialog(new AppDetailsDialog());
            AddDialog(new ServiceTreeDialog());
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                InitialStepAsync,
                ServiceTreeIdAsync,
                ProcessServiceTreeIdAsync
            }));
            
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Pass information from previous dialog class and save - user information doesn't save automatically
            // New waterfall dialog flow = blank slate of data
            AppRegistrationData accessData = (AppRegistrationData)stepContext.Options;
            string database = accessData.DataSets.First().Database;
            DataSetDetails newDataSet = new DataSetDetails(database);

            RegData.DataVisibility = string.Empty;
            RegData.DataVisibility = accessData.DataVisibility.ToString();
            RegData.DataSets.Clear();
            RegData.DataSets.Add(newDataSet);

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> ServiceTreeIdAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.BeginDialogAsync(nameof(ServiceTreeDialog), null, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessServiceTreeIdAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            RegData.ServiceTreeId = Guid.Empty;
            RegData.ServiceTreeId = (Guid)stepContext.Result;
            return await stepContext.BeginDialogAsync(nameof(NewAppAccessRequestDialog), RegData, cancellationToken);
        }
    }
}
