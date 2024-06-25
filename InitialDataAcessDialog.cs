using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.CloudMine.Bot.Configuration;
using Microsoft.CloudMine.Bot.Model;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class InitialDataAccessDialog : ComponentDialog
    {
        private const string RegData = "reg-data";
        public readonly BotConfiguration BotConfig;

        public InitialDataAccessDialog(BotConfiguration botconfig) : base(nameof(InitialDataAccessDialog))
        {
            BotConfig = botconfig;
            AddDialog(new AppAccessMainDialog(botconfig));
            AddDialog(new SelectDatabaseDialog());
            AddDialog(new CloudMineScopeDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                InitialStep,
                SelectIdentityType,
                ProcessIdentityType,
                SelectDatabases,
                ProcessDatabases,
                ProcessScope,
                EndOfConversation
            }));
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> InitialStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values[RegData] = new AppRegistrationData();
            return await stepContext.NextAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> SelectIdentityType(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string introMessage = "You are requesting access to CloudMine data. How would you like to access our data?";
            List<string> identityOptions = new List<string> { "SC-Alt", "AAD App/MSI", "Service Account", "@microsoft.com Account", "Other" };
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(introMessage),
                Choices = ChoiceFactory.ToChoices(identityOptions),
            });
            cancellationToken = cancellationToken
        }

        private async Task<DialogTurnResult> ProcessIdentityType(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string choice = ((FoundChoice)stepContext.Result).Value;
            switch (choice.ToLower())
            {
                case "aad app/msi":
                    string message = "Please ensure you have a valid ServiceTree ID, Tenant Name, and App IDs before you proceed.";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);
                    return await stepContext.NextAsync(cancellationToken: cancellationToken);

                case "service account":
                    string accessMessage = "CloudMine does not provide permissions to service accounts. Create an AAD App and provide your service account access to the AAD app.";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(accessMessage), cancellationToken);
                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);

                default:
                    string urlCorp = "https://aka.ms/cloudmine_access";
                    string accessMessageCorp = $"Apply for the required access group(s) here: {urlCorp}.";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(accessMessageCorp), cancellationToken);
                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SelectDatabases(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.BeginDialogAsync(nameof(SelectDatabaseDialog), null, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessDatabases(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var appRegistrationData = (AppRegistrationData)stepContext.Values[RegData];
            string selectedDatabase = (string)stepContext.Result;
            DataSetDetails newDataSet = new DataSetDetails(selectedDatabase);
            appRegistrationData.DataSets.Clear();
            appRegistrationData.DataSets.Add(newDataSet);

            if (selectedDatabase.Contains("AzureDevOps"))
            {
                return await stepContext.BeginDialogAsync(nameof(CloudMineScopeDialog), null, cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ProcessScope(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var appRegistrationData = (AppRegistrationData)stepContext.Values[RegData];
            if (stepContext.Result == null)
            {
                Dictionary<string, string> myDictionary = new Dictionary<string, string>
                myDictionary.Add( "Environment", BotConfig.Environment.ToString() );
                var errorMessage = new Exception("Unable to process selected scope: " + stepContext.Result.ToString());
                TelemetryClient.TrackException(errorMessage, myDictionary);
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("We could not process the selected CloudMine scope. An internal error occurred."), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

            var selectedScope = (CloudMineScope)stepContext.Result;
            appRegistrationData.DataVisibility = selectedScope.ToString();

            if (selectedScope == CloudMineScope.P || selectedScope == CloudMineScope.X)
            {
                string accessMessageCorp = "Before proceeding, ensure you have valid Table Names for X and P access. If requesting for X, please prepare Organization names as well.";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(accessMessageCorp), cancellationToken);
            }

            return await stepContext.NextAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> EndOfConversation(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var appRegistrationData = (AppRegistrationData)stepContext.Values[RegData];
            return await stepContext.BeginDialogAsync(nameof(AppAccessMainDialog), appRegistrationData, cancellationToken);
        }
    }
}
