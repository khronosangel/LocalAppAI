using Microsoft.Extensions.AI;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
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
        private IKernelMemory kernelMemory;
        private bool useRag = false;

        //private const string SelectedModel = "phi4-mini:latest";
        //private const string SelectedModel = "phi3:mini";
        //private const string SelectedModel = "gpt-oss:120b";
        private const string SelectedModel = "incept5/llama3.1-claude:latest";//"phi4-mini:latest";
        private int tokenCount = 4096;
        private const string llmServerURL = "http://localhost:11434/";
        //private const string llmServerURL = "http://192.168.50.191:11434/";
        private CancellationTokenSource PromptThread;

        private const string OllamaAPI_URL = "https://ollama.com/api/chat";
        private string OllamaAPI_KEY = "aee2d891fea94764b7e0bd508bb2f95e.Lv4W9eMkgvelgp_kShQw3JW5";
        private const string RAG_FILES_PATH = @"C:\Temp\RAG_Files";
        
        private const string MCP_REFERENCE_FILES_PATH = "C:/Temp/MCP/ReferenceFiles";

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

            //setting up RAG
            _ = InitializeRagAsync();
        }

        /// <summary>
        /// Initialize RAG support using KernelMemory with Ollama
        /// </summary>
        private async Task InitializeRagAsync()
        {
            try
            {
                // Build Kernel Memory with Ollama configuration
                var memoryBuilder = new KernelMemoryBuilder()
                    .WithOllamaTextGeneration(
                        new OllamaConfig
                        {
                            Endpoint = llmServerURL,
                            TextModel = new OllamaModelConfig(SelectedModel, tokenCount)
                        })
                    .WithOllamaTextEmbeddingGeneration(
                        new OllamaConfig
                        {
                            Endpoint = llmServerURL,
                            EmbeddingModel = new OllamaModelConfig("nomic-embed-text", tokenCount)
                        });

                kernelMemory = memoryBuilder.Build<MemoryServerless>();

                // Import all text files from the RAG directory
                if (Directory.Exists(RAG_FILES_PATH))
                {
                    await ImportRagFilesAsync();
                    useRag = true;
                }
                else
                {
                    Console.WriteLine($"RAG directory not found: {RAG_FILES_PATH}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize RAG: {ex.Message}");
            }
        }

        /// <summary>
        /// Import all text files from the RAG directory
        /// </summary>
        private async Task ImportRagFilesAsync()
        {
            try
            {
                var textFiles = Directory.GetFiles(RAG_FILES_PATH, "*.txt", SearchOption.AllDirectories);

                foreach (var file in textFiles)
                {
                    await kernelMemory.ImportDocumentAsync(file, documentId: Path.GetFileName(file));
                    Console.WriteLine($"Imported: {Path.GetFileName(file)}");
                }

                Console.WriteLine($"Successfully imported {textFiles.Length} text files into RAG memory");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing RAG files: {ex.Message}");
            }
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
            string PromptEngineered =
                $"-----------------------------------------------\n" +
                $"REFERENCE FILES: \n"+
                $"- {MCP_REFERENCE_FILES_PATH}/IZNA_Public_Info.txt \n" +
                $"-----------------------------------------------\n";
            Message = PromptEngineered + Message;
            if (useMcp && chatCompletionService != null && semanticKernelHistory != null)
            {
                await SendMessageWithMcp(Message, SentDate, InProgress, OnFinish);
            }
            else if (useRag && kernelMemory != null)
            {
                await SendMessageWithRag(Message, SentDate, InProgress, OnFinish);
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

        private async Task SendMessageWithRag(
            string Message,
            DateTime SentDate,
            Func<string, int, Task> InProgress,
            Func<string, int, Task> OnFinish
        )
        {
            PromptThread = new CancellationTokenSource();
            string response = "";
            tokenCount = 0;

            try
            {
                // Search for relevant context from RAG memory
                var searchResult = await kernelMemory.SearchAsync(Message, limit: 3);

                // Build context from search results
                var contextBuilder = new StringBuilder();
                contextBuilder.AppendLine("Relevant information from knowledge base:");
                foreach (var result in searchResult.Results)
                {
                    contextBuilder.AppendLine($"\n[Source: {result.SourceName}]");
                    foreach (var partition in result.Partitions)
                    {
                        contextBuilder.AppendLine(partition.Text);
                    }
                }

                // Augment prompt with RAG context
                string augmentedPrompt = $"{contextBuilder}\n\nUser Question: {Message}\n\nAnswer:";

                // Add to chat history
                chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, augmentedPrompt));

                // Stream response
                await foreach (var answerToken in chat.SendAsync(augmentedPrompt, PromptThread.Token))
                {
                    tokenCount++;
                    response += answerToken;
                    await InProgress(response, tokenCount);
                }

                chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, response));
                await OnFinish(response, tokenCount);
            }
            catch (Exception ex)
            {
                response = $"Error during RAG chat: {ex.Message}";
                await OnFinish(response, 0);
            }
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
