using AdaptiveCards;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.CloudMine.Bot.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class SelectTablesDialog : ComponentDialog
    {
        public readonly BotConfiguration BotConfig;
        private const string DataAccessData = "data-access-data";
        private const string TableName = "tableName";

        public SelectTablesDialog(BotConfiguration botconfig) : base(nameof(SelectTablesDialog))
        {
            BotConfig = botconfig;
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetTables,
                TablesAdaptiveCard,
                ProcessResponse
            }));
            InitialDialogId = nameof(WaterfallDialog);
        }

        public static Attachment CreateAdaptiveCard(string[] tableNames)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock
                    {
                        Text = "Please select desired tables.",
                        Size = AdaptiveTextSize.Large,
                        Weight = AdaptiveTextWeight.Bolder
                    },
                    new AdaptiveChoiceSetInput
                    {
                        Id = TableName,
                        IsMultiSelect = true,
                        Choices = new List<AdaptiveChoice>()
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveSubmitAction
                    {
                        Title = "Submit"
                    }
                }
            };

            foreach (string tableName in tableNames)
            {
                ((AdaptiveChoiceSetInput)card.Body[1]).Choices.Add(new AdaptiveChoice { Title = tableName.Trim(), Value = tableName.Trim() });
            }

            string serializedCard = JsonConvert.SerializeObject(card);
            return new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(serializedCard)
            };
        }

        private async Task<DialogTurnResult> GetTables(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string visibility = (string)stepContext.Options;
            string[] tableNames = await KustoUtils.GetTables(visibility, BotConfig).ConfigureAwait(false);
            return await stepContext.NextAsync(tableNames, cancellationToken);
        }

        private async Task<DialogTurnResult> TablesAdaptiveCard(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string[] tableNames = (string[])stepContext.Result;
            var cardAttachment = CreateAdaptiveCard(tableNames);
            var promptMessage = MessageFactory.Attachment(cardAttachment, "", InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = (Activity)promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessResponse(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var choicesValue = ((JObject)stepContext.Context.Activity.Value)[TableName];
            List<string> choices = new List<string>();
            if (choicesValue != null)
            {
                if (choicesValue is JArray choicesArray)
                {
                    choices = choicesArray.ToObject<List<string>>();
                }
                else
                {
                    choices.Add(choicesValue.ToString());
                }

                if (choices.Count > 0)
                {
                    string finalChoice = string.Join(",", choices);
                    stepContext.Context.Activity.Text = finalChoice;
                    await stepContext.Context.SendActivityAsync(finalChoice, cancellationToken);
                    return await stepContext.EndDialogAsync(finalChoice);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please select a valid option."));
                    return await stepContext.BeginDialogAsync(nameof(SelectTablesDialog), null, cancellationToken);
                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please select a valid option."));
                return await stepContext.BeginDialogAsync(nameof(SelectTablesDialog), null, cancellationToken);
            }
        }
    }
}
