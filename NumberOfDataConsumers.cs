using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.CloudMine.Bot.Model;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class NumberOfDataConsumers : ComponentDialog
    {
        public NumberOfDataConsumers() : base(nameof(NumberOfDataConsumers))
        {
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                DownstreamData,
                ProcessDownstreamData
            }));
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> DownstreamData(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("What is the estimated number of downstream data consumers?")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessDownstreamData(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string response = (string)stepContext.Result;
            if (uint.TryParse(response, out uint numberOfConsumers))
            {
                return await stepContext.EndDialogAsync(numberOfConsumers);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please enter a valid number."), cancellationToken);
                return await stepContext.BeginDialogAsync(nameof(NumberOfDataConsumers), null, cancellationToken);
            }
        }
    }
}
