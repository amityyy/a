using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.CloudMine.Bot.Configuration;
using Microsoft.CloudMine.Bot.Model;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class AppDetailsDialog : ComponentDialog
    {
        private const string TenantName = "app-tenant-name";
        private const string AppId = "app-id";
        private const string Details = "app-details";
        private List<ServicePrincipal> appDetails = new List<ServicePrincipal>();

        public AppDetailsDialog() : base(nameof(AppDetailsDialog))
        {
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                Tenant,
                TenantSave,
                AppDetails,
                MoreApps,
                FinalStep
            }));
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> Tenant(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var prevDetails = (List<ServicePrincipal>)stepContext.Options;
            stepContext.Values["app-details"] = appDetails;
            string tenantOption = "Which tenant does your app reside in?";
            List<string> choices = new List<string> { "MSIT", "AME", "PME", "Torus" };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(tenantOption),
                Choices = ChoiceFactory.ToChoices(choices),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> TenantSave(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values[TenantName] = ((FoundChoice)stepContext.Result).Value;
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Provide your App IDs in a comma-separated list. " +
                                             "If you have additional App IDs in another tenant, you will be given an option to " +
                                             "provide additional tenants later."),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> AppDetails(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var appDetails = (List<ServicePrincipal>)stepContext.Values["app-details"];
            var appIdList = ((string)stepContext.Result).Split(',').ToList();
            List<Guid> currentAppIds = appIdList.Select(id => Guid.Parse(id)).ToList();

            foreach (var id in currentAppIds)
            {
                string idString = id.ToString();
                if (!Guid.TryParse(idString, out Guid validGuid))
                {
                    var errorMessage = new Exception("User entered invalid App ID: '" + id + "'");
                    TelemetryClient.TrackException(errorMessage);
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please enter a valid App ID."));
                    // Begin a new App details dialog
                    return await stepContext.BeginDialogAsync(nameof(AppDetailsDialog), null, cancellationToken);
                }
                else
                {
                    string currentTenantName = (string)stepContext.Values[TenantName];
                    var appDetail = new ServicePrincipal(currentTenantName, currentAppIds);
                    appDetails.Add(appDetail);
                    string additionalAppIds = "Do you have additional App IDs for another tenant?";
                    List<string> choices = new List<string> { "Yes", "No" };

                    return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                    {
                        Prompt = MessageFactory.Text(additionalAppIds),
                        Choices = ChoiceFactory.ToChoices(choices),
                    }, cancellationToken);
                }
            }
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> MoreApps(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var choice = (FoundChoice)stepContext.Result;
            string response = choice.Value.ToLower();
            switch (response)
            {
                case "yes":
                    return await stepContext.ReplaceDialogAsync(InitialDialogId, stepContext.Values["app-details"], cancellationToken);
                case "no":
                    return await stepContext.NextAsync(stepContext.Values["app-details"], cancellationToken);
                default:
                    return await stepContext.NextAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FinalStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["app-details"] = appDetails;
            var result = await stepContext.EndDialogAsync(appDetails);
            appDetails = new List<ServicePrincipal>();  // Reset the list for future interactions
            return result;
        }
    }
}
