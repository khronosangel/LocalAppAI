using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAppAI.Services
{
    public  class AIChatService
    {
        public AIChatService Instance { get; private set; }

        private OllamaApiClient chatClient;
        private List<Microsoft.Extensions.AI.ChatMessage> chatHistory;
        private Chat chat;
        //private const string SelectedModel = "phi4-mini:latest";
        private const string SelectedModel = "incept5/llama3.1-claude:latest";//"phi4-mini:latest";
        private int tokenCount = 0;
        private const string llmServerURL = "http://localhost:11434/";
        //private const string llmServerURL = "http://192.168.50.191:11434/";
        private CancellationTokenSource PromptThread;


        public AIChatService()
        {
            Instance = this;
            
            chatClient = new OllamaApiClient(new Uri(llmServerURL));
            chatClient.SelectedModel = SelectedModel;
            // Start the conversation with context for the AI model
            chatHistory = new();
            chat = new Chat(chatClient);
        }


        public async Task PrepareAIModel(Func<string, Task> InProgress, Func<string, Task> OnFinish)
        {
            try
            {
                var installedModels = await chatClient.ListLocalModelsAsync();
                var sourceModel = installedModels.Where(r => r.Name == SelectedModel).FirstOrDefault();
                if (sourceModel==null) 
                {
                    await foreach (var status in chatClient.PullModelAsync(chatClient.SelectedModel))
                    {
                        await InProgress($"Preparing Model {chatClient.SelectedModel}:{status.Percent}% {status.Status}");
                    }
                        
                    
                }
                await OnFinish($"SUCCESS: Done pulling Model");
            }
            catch(Exception ex)
            {
                OnFinish?.Invoke($"ERROR: {ex.Message}");
            }
        }

        public async Task SendMessage(
            string Message,
            DateTime SentDate,
            Func<string, int, Task> InProgress,
            Func<string, int, Task> OnFinish
        )
        {
            var userPrompt = Message;
            PromptThread = new CancellationTokenSource();
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userPrompt));
            //Collecting replies
            string response = "";
            tokenCount = 0;
            await foreach (var answerToken in chat.SendAsync(userPrompt, PromptThread.Token))
            {
                tokenCount++;
                response += answerToken;
                //response += stream;
                await InProgress(response, tokenCount);
            }
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, response));

            await OnFinish(response, tokenCount);

        }

        public async Task CancelPrompt()
        {
            if (!(PromptThread?.IsCancellationRequested ?? true))
            {
                PromptThread.Cancel();
            }
        }
    }
}
