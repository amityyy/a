using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.CloudMine.Bot.Model;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class CloudMineScopeDialog : ComponentDialog
    {
        public CloudMineScopeDialog() : base(nameof(CloudMineScopeDialog))
        {
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                SelectDataVisibility,
                DataVisibility
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private static async Task<DialogTurnResult> SelectDataVisibility(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string accountAccessChoice = "What CloudMine data scope (https://aka.ms/cloudmine_scope) do you want to get access for?";
            List<string> choices = Enum.GetNames(typeof(CloudMineScope)).ToList();

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(accountAccessChoice),
                Choices = ChoiceFactory.ToChoices(choices)
            }, cancellationToken);
        }

        private static async Task<DialogTurnResult> DataVisibility(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string choice = ((FoundChoice)stepContext.Result).Value;
            try
            {
                CloudMineScope selectedScope = (CloudMineScope)Enum.Parse(typeof(CloudMineScope), choice);
                return await stepContext.EndDialogAsync(selectedScope, cancellationToken);
            }
            catch
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Unknown command!"), cancellationToken);
                return await stepContext.EndDialogAsync(cancellationToken);
            }
        }
    }
}
