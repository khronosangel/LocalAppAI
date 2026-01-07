using LocalAppAI.Models;
using LocalAppAI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocalAppAI.Components.Pages
{
    public partial class Messages : ComponentBase
    {
        [Inject]
        public AIChatService AIChat { get; set; }

        [Inject]
        public IJSRuntime JS { get; set; }

        public string MessageInput { get; set; }
        public List<MessageModel> MessagesList { get; set; } = new();
        public Dictionary<int,string> Responses { get; set; } = new();
        public string StatusMessage { get; set; }
        public int MessageIndex = 0;

        
        public CancellationTokenSource SpeechThread;

        public bool IsSpeaking = false;

        protected override async Task OnInitializedAsync()
        {
            string AppDataPath = FileSystem.AppDataDirectory;

            if (!Directory.Exists($"{AppDataPath}/FileContext"))
            {
                Directory.CreateDirectory($"{AppDataPath}/FileContext");
            }

            MessageIndex = 0;
            await Task.Delay(1000);
            await AIChat.PrepareAIModel(async (stat) =>
            {
                StatusMessage = stat;
                await InvokeAsync(StateHasChanged);
            },
            async (done) =>
            {
                StatusMessage = done;
                await InvokeAsync(StateHasChanged);
            });

            Responses = new Dictionary<int, string>();
        }

        public async Task SendMessage()
        {
            var transactionDate = DateTime.Now;

            // Add user message
            MessageIndex++;
            MessagesList.Add(new MessageModel
            {
                ID = MessageIndex,
                Content = MessageInput,
                Timestamp = transactionDate,
                IsFromUser = true
            });

            // Add placeholder for AI response
            MessageIndex++;
            MessagesList.Add(new MessageModel
            {
                ID = MessageIndex,
                Content = "(Thinking...)",
                Timestamp = transactionDate,
                IsFromUser = false
            });

            string incomingMessage = MessageInput;
            MessageInput = "";
            await InvokeAsync(StateHasChanged);
            await ScrollToBottomAsync();
            // Enable MCP with a server executable
            await AIChat.EnableMcpSupport("C:\\Projects\\LocalAppAI\\SampleMCPServer\\bin\\Debug\\net10.0\\SampleMCPServer.exe");

            await AIChat.SendMessage(incomingMessage, transactionDate, async (res, tokens) =>
            {
                var aiMsg = MessagesList.FindLast(m => m.ID == MessageIndex);
                if (aiMsg != null)
                {
                    aiMsg.Content = res;
                    aiMsg.Timestamp = transactionDate;
                    aiMsg.TokensGenerated = tokens;
                }
                await ScrollToBottomAsync();
                await InvokeAsync(StateHasChanged);
            }, async (res,tokens) =>
            {
                var processedDate = DateTime.Now;
                // Replace the last "(Thinking...)" message with the actual response
                var aiMsg = MessagesList.FindLast(m => !m.IsFromUser);// && m.Content == "(Thinking...)");
                if (aiMsg != null)
                {
                    aiMsg.Content = res;
                    aiMsg.Timestamp = processedDate;
                    aiMsg.TokensGenerated = tokens;
                }
                await ScrollToBottomAsync();
                await InvokeAsync(StateHasChanged);
                await JS.InvokeVoidAsync("window.transformText", aiMsg.ID);
                Responses.Add(aiMsg.ID, res);

                // Disable MCP when done
                await AIChat.DisableMcpSupport();
            });
        }

        private async Task ScrollToBottomAsync()
        {
            await JS.InvokeVoidAsync("window.scrollToBottom");
        }

        private async Task CancelPrompt()
        {
            await AIChat.CancelPrompt();
        }

        private async Task SpeakOut(int MessageID)
        {
            if (!IsSpeaking)
            {
                var AIResponse = Responses[MessageID];
                //var speechVoiceOptions = new SpeechOptions
                //{
                //    Volume = 1.0f,
                //    Pitch = 1.0f,
                //    //Locale = "en/US"
                //};
                IEnumerable<Locale> locales = await TextToSpeech.Default.GetLocalesAsync();
                // You may need to filter locales to find one with a female voice,
                // or the operating system's default might have a female voice.
                Locale femaleLocale = locales.FirstOrDefault(l => l.Language == "en-US"); // Example for English (US)

                SpeechOptions speechVoiceOptions = new SpeechOptions()
                {
                    Locale = femaleLocale, // Select a locale that uses a female voice
                    Volume = 0.75f,       // 0.0 - 1.0
                    Pitch = 1.0f          // 0.0 - 2.0
                };
                SpeechThread = new CancellationTokenSource();
                IsSpeaking = true;
                await TextToSpeech.Default.SpeakAsync(AIResponse, speechVoiceOptions, SpeechThread.Token);
            }
            else
            {
                if (!(SpeechThread?.IsCancellationRequested ?? true))
                {
                    SpeechThread.Cancel();
                    IsSpeaking = false ;
                }
            }
            
        }

    }
}
