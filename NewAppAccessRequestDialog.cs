using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Teams;
using Microsoft.CloudMine.Bot.Configuration;
using Microsoft.CloudMine.Bot.Model;
using Microsoft.CloudMine.Bot.Utilities.Icm;
using Microsoft.CloudMine.Bot.Utilities.Icm.Model;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.Bot.Dialogs.Access
{
    public class NewAppAccessRequestDialog : ComponentDialog
    {
        public readonly BotConfiguration BotConfig;
        public AppRegistrationData RegData { get; private set; }
        private DataSetDetails NewDataSet;

        public NewAppAccessRequestDialog(BotConfiguration botconfig) : base(nameof(NewAppAccessRequestDialog))
        {
            BotConfig = botconfig;
            RegData = new AppRegistrationData();
            NewDataSet = new DataSetDetails(string.Empty, "D", "P");

            AddDialog(new SelectTablesDialog(botconfig));
            AddDialog(new EmailDialog());
            AddDialog(new AppDetailsDialog());
            AddDialog(new ServiceTreeDialog());
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                InitialStep,
                PromptOwner,
                CheckOwner,
                AppDetails,
                ProcessAppDetails,
                BusinessJustification,
                ProcessBusinessJustification,
                DownstreamData,
                ProcessDownstreamData,
                ContactEmail,
                ProcessContactEmail,
                ChooseTables,
                ProcessTables,
                DataResults,
                DataHandling,
                Access,
                ProcessAccess,
                ProvideOrganizationX,
                ProcessOrganizationX,
                CreateIcm
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> InitialStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            AppRegistrationData accessData = (AppRegistrationData)stepContext.Options;
            NewDataSet = new DataSetDetails(accessData.DataSets.First().Database, "[]", "[]");
            RegData.DataSets.Clear();
            RegData.DataSets.Add(NewDataSet);

            RegData.DataVisibility = RegData.DataVisibility.ToLower();
            var sender = await TeamsInfo.GetMemberAsync(stepContext.Context, stepContext.Context.Activity.From.Id, cancellationToken);
            string email = sender.UserPrincipalName;
            string userAlias = Regex.Replace(email, @"@\S+(?:\.\S+)+", "*");

            RegData.Requester = string.Empty;
            RegData.Requester = userAlias;

            return await stepContext.NextAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> PromptOwner(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            List<string> choices = new List<string> { "Yes", "No" };
            string isOwner = "Are you one of the owners of the App(s) or managed service identities (MSI)?";
            return await stepContext.PromptAsync(nameof(ChoicePrompt),
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(isOwner),
                    Choices = ChoiceFactory.ToChoices(choices),
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> CheckOwner(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string ownerChoice = ((FoundChoice)stepContext.Result).Value;
            switch (ownerChoice.ToLower())
            {
                case "yes":
                    return await stepContext.NextAsync(cancellationToken: cancellationToken);
                case "no":
                    string response = "You need to be an owner of an AAD app to request access.";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                default:
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Unknown command!"), cancellationToken);
                    return await stepContext.NextAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> AppDetails(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            RegData.ServicePrincipals.Clear();
            List<ServicePrincipal> appDetails = new List<ServicePrincipal>();
            var dialogTurnResult = await stepContext.BeginDialogAsync(nameof(AppDetailsDialog), appDetails, cancellationToken);
            if (dialogTurnResult.Status == DialogTurnStatus.Complete)
            {
                stepContext.Values["app-details"] = (List<ServicePrincipal>)dialogTurnResult.Result;
                return await stepContext.NextAsync(dialogTurnResult.Result, cancellationToken);
            }
            else
            {
                return dialogTurnResult;
            }
        }

        private async Task<DialogTurnResult> ProcessAppDetails(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var appDetailResult = (List<ServicePrincipal>)stepContext.Result;
            RegData.AddServicePrincipals(appDetailResult);
            return await stepContext.NextAsync(cancellationToken);
        }

        private async Task<DialogTurnResult> BusinessJustification(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string question = "To proceed with your request, we need a comprehensive business justification that explains the purpose, scope and benefits of your project." +
                              "Please include the following information in your justification:" + Environment.NewLine +
                              "- How and what data will be processed by your project, and what methods and tools will be used for data analysis." + Environment.NewLine +
                              "- How results will be stored and secured, and who will have access to them." + Environment.NewLine +
                              "- How your project will contribute to the improvement of our products, services or processes.";
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(question)
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessBusinessJustification(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            RegData.BusinessJustification = (string)stepContext.Result;
            return await stepContext.NextAsync(cancellationToken);
        }

        private async Task<DialogTurnResult> DownstreamData(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.BeginDialogAsync(nameof(NumberOfDataConsumers), null, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessDownstreamData(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            uint response = (uint)stepContext.Result;
            RegData.DownstreamDataConsumers = response;
            return await stepContext.NextAsync(cancellationToken);
        }

        private async Task<DialogTurnResult> ContactEmail(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.BeginDialogAsync(nameof(EmailDialog), null, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessContactEmail(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            RegData.ContactEmail = (string)stepContext.Result;
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ChooseTables(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (Visibility == "p" || Visibility == "x")
            {
                return await stepContext.BeginDialogAsync(nameof(SelectTablesDialog), Visibility, cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> ProcessTables(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (Visibility == "p" || Visibility == "x")
            {
                var selectedTables = stepContext.Result.ToString().Split(',').ToList();
                NewDataSet.Tables.Clear();
                NewDataSet.Tables.AddRange(selectedTables);
            }
            string question = "Briefly explain what your team needs private data access for? (Business objective) In your answer," +
                              Environment.NewLine + "1. What is the problem your team is trying to solve?" + Environment.NewLine +
                              "2. Expected scope and impact on user." + Environment.NewLine +
                              "3. What data will be required and how will it be used to drive actions.";
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(question)
            }, cancellationToken);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> DataHandling(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string result = (string)stepContext.Result;
            if (Visibility == "p" || Visibility == "x")
            {
                string businessJustificationAdditional = RegData.BusinessJustification + " - Business Objective: " + result;
                RegData.BusinessJustification = businessJustificationAdditional;
            }
            string question = "How will the data accessed be processed and used?";
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(question)
            }, cancellationToken);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> DataResults(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string result = (string)stepContext.Result;
            if (Visibility == "p" || Visibility == "x")
            {
                string businessJustificationAdditional = RegData.BusinessJustification + " - DataHandling: " + result;
                RegData.BusinessJustification = businessJustificationAdditional;
            }
            string question = "What do the results of this data processing look like and how will the resulting data sets/reports be handled and stored?";
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(question)
            }, cancellationToken);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> Access(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string result = (string)stepContext.Result;
            if (Visibility == "p" || Visibility == "x")
            {
                string businessJustificationAdditional = RegData.BusinessJustification + " - Data Results: " + result;
                RegData.BusinessJustification = businessJustificationAdditional;
            }
            string question = "Who will have access to view the resulting data/reports/dashboards?";
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text(question)
            }, cancellationToken);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ProcessAccess(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string result = (string)stepContext.Result;
            if (Visibility == "p" || Visibility == "x")
            {
                string businessJustificationAdditional = RegData.BusinessJustification + " - Access: " + result;
                RegData.BusinessJustification = businessJustificationAdditional;
                return await stepContext.NextAsync();
            }
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ProvideOrganizationX(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (Visibility == "x")
            {
                string question = "Please provide the organizations you would like access to as a comma separated list.";
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text(question)
                }, cancellationToken);
            }
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ProcessOrganizationX(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string userOrganization = (string)stepContext.Result;
            HashSet<string> foundOrganizations = new HashSet<string>();
            string[] splitUserOrganizations = userOrganization.Split(',');

            foreach (string currentOrganization in splitUserOrganizations)
            {
                bool organizationExists = await KustoUtils.CheckIfOrganizationExists(currentOrganization, BotConfig).ConfigureAwait(false);
                if (!organizationExists)
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text($"'{currentOrganization}' could not be found. I will continue to check your other organizations."));
                }
                else
                {
                    foundOrganizations.Add(currentOrganization);
                }
            }

            NewDataSet.Organizations.Clear();
            NewDataSet.Organizations.AddRange(foundOrganizations);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Found organizations: " + string.Join(", ", foundOrganizations)));
            return await stepContext.NextAsync();
        }

        public BlobServiceClient GetBlobServiceClient(string accountName, BotConfiguration botConfig)
        {
            Uri storageAccountUri = new Uri($"https://{accountName}.blob.core.windows.net");
            BlobServiceClient client = new BlobServiceClient(storageAccountUri, botConfig.GetCredential());
            return client;
        }

        private async Task<DialogTurnResult> CreateIc(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(RegData);
            JObject jsonObject = JObject.Parse(json);
            string updatedJson = jsonObject.ToString();
            BlobServiceClient blobServiceClient = GetBlobServiceClient("cloudminebotppe", BotConfig);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("msapproval");
            BlobClient blobClient = containerClient.GetBlobClient(Guid.NewGuid().ToString());
            
            DateTime currentDate = DateTime.UtcNow.Date;
            string servicePrincipalString = string.Join(", ", RegData.ServicePrincipals.Select(sp => $"App Id: {sp.AppIds}, Tenant Name: {sp.TenantName}, TenantID: {sp.TenantId}"));
            string dataSetString = string.Join(", ", RegData.DataSets.Select(ds => $"Database: {ds.Database}, Tables: {string.Join(", ", ds.Tables)}, Organizations: {string.Join(", ", ds.Organizations)}"));
            
            // create discussion comment(description entry)
            string formattedJsonString = jsonObject.ToString(Newtonsoft.Json.Formatting.Indented);
            List<DescriptionEntry> htmlDescriptionEntry = new List<DescriptionEntry>
            {
                new DescriptionEntry(currentDate, "cloudminebot", formattedJsonString, RenderType.PlainText)
            };
            
            // Incident information
            Incident newIncident = new Incident
            {
                Title = $"App Access Request {RegData.DataVisibility}/{RegData.Requester}", // app request title
                DescriptionEntries = htmlDescriptionEntry,
                ImpactStartDate = DateTime.Now,
                CorrelationId = string.Empty,
                Keywords = "App Registration",
                RoutingId = "IcM://DSOAlerts/CLOUDMINE/DRI-Cosmos",
                Severity = 4,
                Source = new AlertSourceInfo($"{RegData.Requester}", "CloudMineBot", DateTime.Now, DateTime.Now),
                Status = new IncidentStatus(),
                MonitorId = "CloudMine Bot"
            };
            
            // create incident
            long result = await IcmUtils.CreateIncident(newIncident);
            // upload json attachment
            await IcmUtils.UploadAttachment(result, updatedJson);
            string dataJson = $"{{\"userPrincipalName\": \"{RegData.Requester}@microsoft.com\", \"icmId\": \"{result}\"}}";
            string fileName = Path.GetTempFileName();
            try
            {
                // Upload file for MSApproval - Needs icm id
                File.WriteAllText(fileName, dataJson);
                blobClient.Upload(fileName, true);
                string response = $"Your request has been logged in our system. We have notified your manager for approval. The request will not proceed without manager approval. You will receive an update on the ticket when the approval is done. You can view the ticket through this link. Please monitor the ticket and respond to any comments on it, https://portal.microsofticm.com/imp/v3/incidents/details/{result}";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                // Add telemetry tracking for bot
                var errorMessage = new Exception("ICM creation failed on blob client." + ex.ToString());
                TelemetryClient.TrackException(errorMessage);
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Access request failed - we appreciate your patience while we work to resolve this error."), cancellationToken);
            }
            finally
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception ex)
                {
                    var deleteErrorMessage = new Exception("Failed to delete json file.", ex);
                    TelemetryClient.TrackException(deleteErrorMessage);
                }
            }
            return await stepContext.EndDialogAsync(cancellationToken);
        }


    }
}
