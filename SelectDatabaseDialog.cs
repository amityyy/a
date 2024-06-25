using AdaptiveCards;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class SelectDatabaseDialog : ComponentDialog
    {
        private const string AppRegData = "app-reg-data";

        public SelectDatabaseDialog() : base(nameof(SelectDatabaseDialog))
        {
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                DatabaseAdaptiveCard,
                ProcessResponse
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        public static Attachment CreateAdaptiveCard(string uniqueId)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock
                    {
                        Text = "Which databases would you like access to? Select ONE",
                        Size = AdaptiveTextSize.Medium
                    },
                    new AdaptiveChoiceSetInput
                    {
                        Id = uniqueId,
                        IsMultiSelect = false,
                        Choices = new List<AdaptiveChoice>
                        {
                            new AdaptiveChoice { Title = "Azure Active Directory", Value = "AzureActiveDirectory" },
                            new AdaptiveChoice { Title = "Azure DevOps", Value = "AzureDevOps" },
                            new AdaptiveChoice { Title = "GitHub", Value = "GitHub" },
                            new AdaptiveChoice { Title = "Stack Overflow @ MSFT", Value = "StackOverflowAtMicrosoft" },
                            new AdaptiveChoice { Title = "GitHub-EMU", Value = "GitHub-EMU" }
                        }
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveSubmitAction
                    {
                        Title = "Submit",
                        Data = new { Action = "submit" }
                    }
                }
            };

            return new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card
            };
        }

        private static async Task<DialogTurnResult> DatabaseAdaptiveCard(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string uniqueId = Guid.NewGuid().ToString();
            var cardAttachment = CreateAdaptiveCard(uniqueId);
            stepContext.Values["uniqueId"] = uniqueId;
            var promptMessage = MessageFactory.Attachment(cardAttachment, "", InputHints.ExpectingInput);

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = (Activity)promptMessage }, cancellationToken);
        }

        private static async Task<DialogTurnResult> ProcessResponse(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string uniqueId = (string)stepContext.Values["uniqueId"];
            var selectedChoicesValue = ((JObject)stepContext.Context.Activity.Value)[uniqueId];

            List<string> selectedChoices = new List<string>();
            if (selectedChoicesValue != null)
            {
                if (selectedChoicesValue is JArray choicesArray)
                {
                    selectedChoices = choicesArray.ToObject<List<string>>();
                }
                else
                {
                    selectedChoices.Add(selectedChoicesValue.ToString());
                }

                if (selectedChoices.Count > 0)
                {
                    string finalChoice = string.Join(", ", selectedChoices);
                    stepContext.Context.Activity.Text = finalChoice;
                    await stepContext.Context.SendActivityAsync(finalChoice, cancellationToken: cancellationToken);
                    return await stepContext.EndDialogAsync(finalChoice);
                }
                else
                {
                    // If the user clicks submit without selecting a database
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text("Please select a valid option."),
                        cancellationToken
                    );
                    return await stepContext.BeginDialogAsync(nameof(SelectDatabaseDialog), null, cancellationToken);
            }

            // If user clicks submit without selecting a database
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please select a valid option."));
            return await stepContext.BeginDialogAsync(nameof(SelectDatabaseDialog), null, cancellationToken);
        }
    }
}
