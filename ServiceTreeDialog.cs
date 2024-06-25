using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class ServiceTreeDialog : ComponentDialog
    {
        public ServiceTreeDialog() : base(nameof(ServiceTreeDialog))
        {
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ServiceTreeIdDetails,
                ServiceTreeIdResult
            }));
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ServiceTreeIdDetails(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Please provide your ServiceTree Id:")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ServiceTreeIdResult(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            Guid serviceTreeId = Guid.Empty;
            bool isServiceTreeId = Guid.TryParse((string)stepContext.Result, out serviceTreeId);
            if (!isServiceTreeId)
            {
                return await stepContext.ReplaceDialogAsync(nameof(ServiceTreeDialog), null, cancellationToken);
            }
            else
            {
                // Return the valid ServiceTree ID and end the dialog.
                return await stepContext.EndDialogAsync(serviceTreeId, cancellationToken);
            }
        }
    }
}
