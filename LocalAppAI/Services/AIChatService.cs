using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAppAI.Services
{
    public class AIChatService
    {
        public AIChatService Instance { get; private set; }

        private OllamaApiClient chatClient;
        private List<Microsoft.Extensions.AI.ChatMessage> chatHistory;
        private Chat chat;
        private Kernel kernel;
        private IChatCompletionService chatCompletionService;
        private ChatHistory semanticKernelHistory;
        private bool useMcp = false;
        private McpClient mcpClient;

        //private const string SelectedModel = "phi4-mini:latest";
        //private const string SelectedModel = "phi3:mini";
        //private const string SelectedModel = "gpt-oss:120b";
        private const string SelectedModel = "incept5/llama3.1-claude:latest";//"phi4-mini:latest";
        private int tokenCount = 0;
        private const string llmServerURL = "http://localhost:11434/";
        //private const string llmServerURL = "http://192.168.50.191:11434/";
        private CancellationTokenSource PromptThread;

        private const string OllamaAPI_URL = "https://ollama.com/api/chat";
        private string OllamaAPI_KEY = "aee2d891fea94764b7e0bd508bb2f95e.Lv4W9eMkgvelgp_kShQw3JW5";

        public AIChatService()
        {
            Instance = this;

            chatClient = new OllamaApiClient(new Uri(llmServerURL));

            //var httpClient = new HttpClient();
            //httpClient.BaseAddress = new Uri(llmServerURL);
            ////httpClient.BaseAddress = new Uri(OllamaAPI_URL);
            //httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", OllamaAPI_KEY);
            //chatClient = new OllamaApiClient(httpClient);

            chatClient.SelectedModel = SelectedModel;
            // Start the conversation with context for the AI model
            chatHistory = new();
            chat = new Chat(chatClient);
        }

        /// <summary>
        /// Enable MCP support by connecting to an MCP server executable
        /// </summary>
        /// <param name="mcpServerPath">Path to the MCP server executable</param>
        public async Task EnableMcpSupport(string mcpServerPath)
        {
            try
            {
                // Initialize Semantic Kernel
                var builder = Kernel.CreateBuilder();
                builder.Services.AddOpenAIChatCompletion(
                    modelId: SelectedModel,
                    apiKey: null,
                    endpoint: new Uri($"{llmServerURL}v1")
                );
                kernel = builder.Build();

                // Set up MCP Client - Connect to the MCP server executable
                mcpClient = await McpClient.CreateAsync(
                    new StdioClientTransport(new()
                    {
                        Command = mcpServerPath,
                        Name = "McpServer",
                    }));

                // Retrieve and load tools from the server
                IList<McpClientTool> tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);

                // Register MCP tools with Semantic Kernel
#pragma warning disable SKEXP0001
                kernel.Plugins.AddFromFunctions("McpTools", tools.Select(t => t.AsKernelFunction()));
#pragma warning restore SKEXP0001

                // Initialize Semantic Kernel chat history
                semanticKernelHistory = new ChatHistory();
                semanticKernelHistory.AddSystemMessage("You are an assistant that can call MCP tools to process user queries.");

                // Get chat completion service
                chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

                useMcp = true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to enable MCP support: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Disable MCP support and revert to standard chat
        /// </summary>
        public async Task DisableMcpSupport()
        {
            useMcp = false;
            if (mcpClient != null)
            {
                await mcpClient.DisposeAsync();
                mcpClient = null;
            }
            semanticKernelHistory = null;
            chatCompletionService = null;
            kernel = null;
        }

        public async Task PrepareAIModel(Func<string, Task> InProgress, Func<string, Task> OnFinish)
        {
            try
            {
                var installedModels = await chatClient.ListLocalModelsAsync();
                var sourceModel = installedModels.Where(r => r.Name == SelectedModel).FirstOrDefault();
                if (sourceModel == null)
                {
                    await foreach (var status in chatClient.PullModelAsync(chatClient.SelectedModel))
                    {
                        await InProgress($"Preparing Model {chatClient.SelectedModel}:{status.Percent}% {status.Status}");
                    }


                }
                await OnFinish($"SUCCESS: Done pulling Model");
            }
            catch (Exception ex)
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
            if (useMcp && chatCompletionService != null && semanticKernelHistory != null)
            {
                await SendMessageWithMcp(Message, SentDate, InProgress, OnFinish);
            }
            else
            {
                await SendMessageStandard(Message, SentDate, InProgress, OnFinish);
            }
        }

        private async Task SendMessageStandard(
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

        private async Task SendMessageWithMcp(
            string Message,
            DateTime SentDate,
            Func<string, int, Task> InProgress,
            Func<string, int, Task> OnFinish
        )
        {
            PromptThread = new CancellationTokenSource();
            semanticKernelHistory.AddUserMessage(Message);

            // Enable auto function calling
            OpenAIPromptExecutionSettings executionSettings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            string response = "";
            tokenCount = 0;

            try
            {
                // Get the response from the AI with MCP tool support
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    semanticKernelHistory,
                    executionSettings: executionSettings,
                    kernel: kernel,
                    cancellationToken: PromptThread.Token);

                response = result.Content ?? string.Empty;
                tokenCount = response.Length; // Approximate token count

                await InProgress(response, tokenCount);

                // Add assistant response to history
                semanticKernelHistory.AddMessage(result.Role, response);

                await OnFinish(response, tokenCount);
            }
            catch (Exception ex)
            {
                response = $"Error during MCP chat: {ex.Message}";
                await OnFinish(response, 0);
            }
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
