// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CELA_Knowledge_Management_Data_Services.BusinessLogic;
using CELA_Knowledge_Management_Data_Services.DataUtilities;
using CELA_Knowledge_Management_Data_Services.Models;
using Microsoft.Extensions.Configuration;
using System.Text;

// TODO update namespace to match project name
namespace CELA_Knowledge_Management_Agent
{
    /// <summary>
    /// Main entry point and orchestration for bot.
    /// </summary>
    public class BasicBot : IBot, IGraphConfiguration, ITableConfiguration
    {
        // Supported LUIS Intents
        public const string GreetingIntent = "Greeting";
        public const string CancelIntent = "Cancel";
        public const string HelpIntent = "Help";
        public const string NoneIntent = "None";
        public const string GetMattersIntent = "GetMatters";
        public const string GetRecentDocumentsIntent = "GetRecentDocuments";
        public const string GetDocumentsForTagIntent = "GetDocumentsForTag";

        public const string TagUsersIntent = "TagUsers";
        public const string TopTagsIntent = "TopTags";
        public const string TopicExpert = "TopUserForStatedTag";

        private const string HelpIntentMessage = "I currently understand get you your matters and getting you your recent documents.";

        private const string WelcomeText = "Hello. I can provide information about the current usage tags. Ask me something to get started.";


        /// <summary>
        /// Key in the bot config (.bot file) for the LUIS instance.
        /// In the .bot file, multiple instances of LUIS can be configured.
        /// </summary>
        public static readonly string LuisConfiguration = "BasicBotLuisApplication"; // TODO update descriptor name

        private readonly IStatePropertyAccessor<GreetingState> _greetingStateAccessor;
        private readonly IStatePropertyAccessor<DialogState> _dialogStateAccessor;
        private readonly UserState _userState;
        private readonly ConversationState _conversationState;
        private readonly BotServices _services;

        private readonly IConfiguration appConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicBot"/> class.
        /// </summary>
        /// <param name="botServices">Bot services.</param>
        /// <param name="accessors">Bot State Accessors.</param>
        //public BasicBot(BotServices services, UserState userState, ConversationState conversationState, ILoggerFactory loggerFactory)
        public BasicBot(BotServices services, UserState userState, ConversationState conversationState, ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            appConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _userState = userState ?? throw new ArgumentNullException(nameof(userState));
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));

            _greetingStateAccessor = _userState.CreateProperty<GreetingState>(nameof(GreetingState));
            _dialogStateAccessor = _conversationState.CreateProperty<DialogState>(nameof(DialogState));

            // Verify LUIS configuration.
            if (!_services.LuisServices.ContainsKey(LuisConfiguration))
            {
                throw new InvalidOperationException($"The bot configuration does not contain a service type of `luis` with the id `{LuisConfiguration}`.");
            }

            Dialogs = new DialogSet(_dialogStateAccessor);
            Dialogs.Add(new GreetingDialog(_greetingStateAccessor, loggerFactory, appConfiguration));
        }

        private DialogSet Dialogs { get; set; }

        public string GetTableAccessKey()
        {
            return appConfiguration.GetSection("TagulousAzureTableAccessKey")?.Value;
        }

        public string GetTableHostname()
        {
            return appConfiguration.GetSection("TagulousAzureTableHostname")?.Value;
        }

        public string GetTableName()
        {
            return appConfiguration.GetSection("TagulousAzureTableName")?.Value;
        }

        public string GetGraphDatabaseHostname()
        {
            return appConfiguration.GetSection("TagulousGraphDBHostName")?.Value;
        }

        public string GetGraphDatabaseAccessKey()
        {
            return appConfiguration.GetSection("TagulousGraphDBAccessKey")?.Value;
        }

        public string GetGraphDatabaseCollectionName()
        {
            return appConfiguration.GetSection("TagulousGraphDBCollection")?.Value;
        }

        public string GetGraphDatabaseName()
        {
            return appConfiguration.GetSection("TagulousGraphDBDatabase")?.Value;
        }

        public int GetGraphDatabasePort()
        {
            return int.Parse(appConfiguration.GetSection("TagulousGraphDBPort")?.Value);
        }

        /// <summary>
        /// Run every turn of the conversation. Handles orchestration of messages.
        /// </summary>
        /// <param name="turnContext">Bot Turn Context.</param>
        /// <param name="cancellationToken">Task CancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var activity = turnContext.Activity;

            // Create a dialog context
            var dc = await Dialogs.CreateContextAsync(turnContext);

            if (activity.Type == ActivityTypes.Message)
            {
                // Perform a call to LUIS to retrieve results for the current activity message.
                var luisResults = await _services.LuisServices[LuisConfiguration].RecognizeAsync(dc.Context, cancellationToken);

                // If any entities were updated, treat as interruption.
                // For example, "no my name is tony" will manifest as an update of the name to be "tony".
                var topIntent = luisResults?.GetTopScoringIntent();

                if (topIntent != null && topIntent.HasValue && topIntent.Value.intent != "None")
                {
                    switch (topIntent.Value.intent)
                    {
                        case GetDocumentsForTagIntent:
                            var topic = LUISDataUtilities.GetEntityAsString(luisResults, "Topic");
                            if (topic != null && topic.Length > 0)
                            {
                                await turnContext.SendActivityAsync(string.Format("You seem to be looking documents associated with {0}. Let me see what I can find.\n", topic));
                                var documentsGraphModel = GraphAnalysisBusinessLogic.GetDocumentsForTag(this, topic);
                                if (documentsGraphModel.Count > 0)
                                {
                                    await turnContext.SendActivityAsync(string.Format("I found the following documents associated with {0}:", topic));

                                    var reply = turnContext.Activity.CreateReply();
                                    reply.Attachments = new List<Attachment>();

                                    //Put the most recent documents at the head of the list
                                    //documentsGraphModel.Reverse();
                                    foreach (var document in documentsGraphModel)
                                    {
                                        //    var attachment = new Attachment
                                        //    {
                                        //        ContentUrl = string.Format("{0}{1}/{2}?web=1", document.properties.library[0].value, document.properties.path[0].value, document.properties.name[0].value),
                                        //        Name = document.properties.name[0].value,
                                        //    };
                                        //    reply.Attachments.Add(attachment);

                                        //await turnContext.SendActivityAsync(string.Format("{1}{2}/{3}?web=1", document.properties.name[0].value, document.properties.library[0].value, document.properties.path[0].value, document.properties.name[0].value).Replace(" ", "%20"));

                                        List<CardAction> cardButtons = new List<CardAction>();
                                        CardAction plButton = new CardAction()
                                        {
                                            Value = string.Format("{1}{2}/{3}?web=1", document.properties.name[0].value, document.properties.library[0].value, document.properties.path[0].value, document.properties.name[0].value).Replace(" ", "%20"),
                                            Type = "openUrl",
                                            Title = document.properties.name[0].value,
                                        };
                                        cardButtons.Add(plButton);

                                        HeroCard plCard = new HeroCard()
                                        {
                                            //Title = document.properties.name[0].value,
                                            Buttons = cardButtons
                                        };

                                        Attachment plAttachment = plCard.ToAttachment();

                                        reply.Attachments.Add(plAttachment);
                                    }

                                    await turnContext.SendActivityAsync(reply);

                                    //var replyToConversation = turnContext.Activity.CreateReply();
                                    //replyToConversation.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                                    //replyToConversation.Attachments = new List<Attachment>();

                                    //Dictionary<string, string> cardContentList = new Dictionary<string, string>();
                                    //cardContentList.Add("PigLatin", "https://<ImageUrl1>");
                                    //cardContentList.Add("Pork Shoulder", "https://<ImageUrl2>");
                                    //cardContentList.Add("Bacon", "https://<ImageUrl3>");

                                    ////Put the most recent entries first
                                    //foreach (var document in documentsGraphModel)
                                    //{


                                    //}

                                    //foreach (KeyValuePair<string, string> cardContent in cardContentList)
                                    //{



                                    //    List<CardImage> cardImages = new List<CardImage>();
                                    //    cardImages.Add(new CardImage(url: cardContent.Value));

                                    //    List<CardAction> cardButtons = new List<CardAction>();

                                    //    CardAction plButton = new CardAction()
                                    //    {
                                    //        Value = $"https://en.wikipedia.org/wiki/{cardContent.Key}",
                                    //        Type = "openUrl",
                                    //        Title = "WikiPedia Page"
                                    //    };

                                    //    cardButtons.Add(plButton);

                                    //    HeroCard plCard = new HeroCard()
                                    //    {
                                    //        Title = $"I'm a hero card about {cardContent.Key}",
                                    //        Subtitle = $"{cardContent.Key} Wikipedia Page",
                                    //        Images = cardImages,
                                    //        Buttons = cardButtons
                                    //    };

                                    //    Attachment plAttachment = plCard.ToAttachment();
                                    //    replyToConversation.Attachments.Add(plAttachment);
                                    //}

                                    //await turnContext.SendActivityAsync(replyToConversation);
                                }
                                else
                                {
                                    await turnContext.SendActivityAsync(string.Format("I did not find any documents associated with {0}.", topic));
                                }
                            }
                            break;
                        case GetMattersIntent:
                            await turnContext.SendActivityAsync($"Getting your matters. This may take a moment.\n");
                            var matterGraphModel = GraphAnalysisBusinessLogic.GetMatters(this);
                            if (matterGraphModel.Count > 0)
                            {
                                await turnContext.SendActivityAsync("I found the following matters:");

                                foreach (var matter in matterGraphModel)
                                {
                                    await turnContext.SendActivityAsync(matter.properties.name[0].value);
                                }
                            }
                            else
                            {
                                await turnContext.SendActivityAsync("I did not find any matters.");
                            }
                            break;
                        case GetRecentDocumentsIntent:
                            await turnContext.SendActivityAsync($"Getting your recent documents. This may take a moment.\n");
                            break;
                        case GreetingIntent:
                            await turnContext.SendActivityAsync($"Hello, I'm Tagulous, CELA's knowledge management assistant. If you want to know what I can do as a question like \"How can you help me?\"\n");
                            break;
                        case HelpIntent:
                            await turnContext.SendActivityAsync($"You seem to be looking for some help. I am can do some basic things for you.\n");
                            await turnContext.SendActivityAsync($"Specifically, I can do the following, and I provide some examples of how you can ask for that service.\n");
                            await turnContext.SendActivityAsync($"Identifying the person who sends communications with the prescribed tag the most: _Who is our expert on privacy?_\n");
                            await turnContext.SendActivityAsync($"Listing the matters engaged through this system: _Please get my matters._\n");
                            await turnContext.SendActivityAsync($"Listing the documents associated with a tag: _Please get me documents associated with testing._\n");

                            break;
                        case NoneIntent:
                            break;
                        case TagUsersIntent:
                            await turnContext.SendActivityAsync($"Getting recent user activity. This may take a moment.\n");
                            break;
                        case TopTagsIntent:
                            //CommunicationProcessingBL.TransactGraphQuery(client, )
                            //await QueryAzureTableForMostUsedTags(turnContext, recognizerResult);
                            break;
                        case TopicExpert:
                            await FindTopicExpert(turnContext, luisResults);
                            break;
                        default:
                            await turnContext.SendActivityAsync($"==>LUIS Top Scoring Intent: {topIntent.Value.intent}, Score: {topIntent.Value.score}\n");
                            break;
                    }
                }
                else
                {
                    var msg = @"No LUIS intents were found.
                            This sample is about identifying two user intents:
                            'Calendar.Add'
                            'Calendar.Find'
                            Try typing 'Add Event' or 'Show me tomorrow'.";
                    await turnContext.SendActivityAsync(msg);
                }

            }
            else if (activity.Type == ActivityTypes.ConversationUpdate)
            {
                await SendWelcomeMessageAsync(turnContext, cancellationToken);
            }

            await _conversationState.SaveChangesAsync(turnContext);
            await _userState.SaveChangesAsync(turnContext);
        }

        private async Task FindTopicExpert(ITurnContext turnContext, RecognizerResult luisResults)
        {
            var topic = LUISDataUtilities.GetEntityAsString(luisResults, "Topic");
            string topSenders = string.Empty;
            if (topic != null && topic.Length > 0)
            {
                await turnContext.SendActivityAsync(string.Format("You seem to be looking for an someone who can help you with {0}. Let me see who I can find.\n", topic));
                //var tagRecipients = GraphAnalysisBusinessLogic.GetTopicRecipients(topic, this);

                int limit = 10;
                //var tagSenders = GraphAnalysisBusinessLogic.GetTopicSenders(topic, this);

                var tagSenderQueryResult = GraphAnalysisBusinessLogic.GetTopicSendersWithSentValues(topic, this);

                // This sorts descending so the strongest sender appears first
                var tagSenders = tagSenderQueryResult.ToList();
                tagSenders.Sort((kvp1, kvp2) => kvp2.Value.CompareTo(kvp1.Value));

                StringBuilder sb = new StringBuilder();
                int counter = 0;
                if (tagSenders.Count < limit)
                {
                    limit = tagSenders.Count;
                }

                foreach (var tagSender in tagSenders)
                {
                    //Add commas and "and"
                    if (counter > 0)
                    {
                        // Only take the top few
                        if (counter == limit)
                        {
                            break;
                        }

                        if (counter == (limit - 1))
                        {
                            if (limit > 2)
                            {
                                sb.Append(", and ");
                            }
                            else
                            {
                                sb.Append(" and ");
                            }
                        }
                        else
                        {
                            sb.Append(", ");
                        }
                    }

                    sb.Append(tagSender.Key);
                    counter++;
                }

                topSenders = sb.ToString();

                await turnContext.SendActivityAsync(string.Format("The people who send the most communication on the topic of {0} are {1}.\n", topic, topSenders));
            }
            else
            {
                await turnContext.SendActivityAsync("You seem to be looking for an someone who can help you, but I could not determine the topic of interest. Consider asking a question like \"Who can help me with privacy?\" and I will try to find people who communicate about that topic.\n");
            }

            //if (expert != null && expert.Length > 0)
            //{
            //    await turnContext.SendActivityAsync(String.Format("You seem to be looking for an someone who can help you with {0}. You might want to contact {1}.\n", topic, expert));
            //}
            //else
            //{
            //    await turnContext.SendActivityAsync(String.Format("You seem to be looking for an someone who can help you with {0}.\n", topic));
            //}
        }

        // Determine if an interruption has occurred before we dispatch to any active dialog.
        private async Task<bool> IsTurnInterruptedAsync(DialogContext dc, string topIntent)
        {
            // See if there are any conversation interrupts we need to handle.
            if (topIntent.Equals(CancelIntent))
            {
                if (dc.ActiveDialog != null)
                {
                    await dc.CancelAllDialogsAsync();
                    await dc.Context.SendActivityAsync("Ok. I've canceled our last activity.");
                }
                else
                {
                    await dc.Context.SendActivityAsync("I don't have anything to cancel.");
                }

                return true;        // Handled the interrupt.
            }

            if (topIntent.Equals(HelpIntent))
            {
                await dc.Context.SendActivityAsync("Let me try to provide some help.");
                await dc.Context.SendActivityAsync("I understand greetings, being asked for help, or being asked to cancel what I am doing.");
                if (dc.ActiveDialog != null)
                {
                    await dc.RepromptDialogAsync();
                }

                return true;        // Handled the interrupt.
            }

            return false;           // Did not handle the interrupt.
        }

        // Create an attachment message response.
        private Activity CreateResponse(Activity activity, Attachment attachment)
        {
            var response = activity.CreateReply();
            response.Attachments = new List<Attachment>() { attachment };
            return response;
        }

        // Load attachment from file.
        private Attachment CreateAdaptiveCardAttachment()
        {
            var adaptiveCard = File.ReadAllText(@".\Dialogs\Welcome\Resources\welcomeCard.json");
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };
        }

        /// <summary>
        /// Helper function to update greeting state with entities returned by LUIS.
        /// </summary>
        /// <param name="luisResult">LUIS recognizer <see cref="RecognizerResult"/>.</param>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task UpdateGreetingState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            if (luisResult.Entities != null && luisResult.Entities.HasValues)
            {
                // Get latest GreetingState
                var greetingState = await _greetingStateAccessor.GetAsync(turnContext, () => new GreetingState());
                var entities = luisResult.Entities;

                // Supported LUIS Entities
                string[] userNameEntities = { "userName", "userName_patternAny" };
                string[] userLocationEntities = { "userLocation", "userLocation_patternAny" };

                // Update any entities
                // Note: Consider a confirm dialog, instead of just updating.
                foreach (var name in userNameEntities)
                {
                    // Check if we found valid slot values in entities returned from LUIS.
                    if (entities[name] != null)
                    {
                        // Capitalize and set new user name.
                        var newName = (string)entities[name][0];
                        greetingState.Name = char.ToUpper(newName[0]) + newName.Substring(1);
                        break;
                    }
                }

                foreach (var city in userLocationEntities)
                {
                    if (entities[city] != null)
                    {
                        // Capitalize and set new city.
                        var newCity = (string)entities[city][0];
                        greetingState.City = char.ToUpper(newCity[0]) + newCity.Substring(1);
                        break;
                    }
                }

                // Set the new values into state.
                await _greetingStateAccessor.SetAsync(turnContext, greetingState);
            }
        }

        /// <summary>
        /// On a conversation update activity sent to the bot, the bot will
        /// send a message to the any new user(s) that were added.
        /// </summary>
        /// <param name="turnContext">Provides the <see cref="ITurnContext"/> for the turn of the bot.</param>
        /// <param name="cancellationToken" >(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>>A <see cref="Task"/> representing the operation result of the Turn operation.</returns>
        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(
                        $"Welcome to KnowledgeManagementBot {member.Name}. {WelcomeText}",
                        cancellationToken: cancellationToken);
                }
            }
        }
    }
}
