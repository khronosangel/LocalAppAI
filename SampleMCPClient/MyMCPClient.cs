using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace SampleMCPClient
{
    public class MyMCPClient
    {
        public static async Task MCPClientSetup(Kernel kernel)
        {
            // Set up MCP Client
            //await using McpClient mcpClient = await McpClient.CreateAsync(
            //    new StdioClientTransport(new()
            //    {
            //        Command = "dotnet run",
            //        Arguments = ["--project", "C:\\Users\\User\\source\\repos\\McpServer\\McpServer.csproj"],
            //        Name = "McpServer",
            //    }));
            await using McpClient mcpClient = await McpClient.CreateAsync(
                new StdioClientTransport(new()
                {
                    Command = "C:\\Projects\\LocalAppAI\\SampleMCPServer\\bin\\Debug\\net10.0\\SampleMCPServer.exe",
                    Name = "SampleMCPServer",
                }));


            // Retrieve and load tools from the server
            IList<McpClientTool> tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);

            // List all available tools from the MCP server
            Console.WriteLine("\n\nAvailable MCP Tools:");
            foreach (var tool in tools)
            {
                Console.WriteLine($"{tool.Name}: {tool.Description}");
            }

            // Register MCP tools with Semantic Kernel
#pragma warning disable SKEXP0001 // Suppress diagnostics for experimental features
            kernel.Plugins.AddFromFunctions("McpTools", tools.Select(t => t.AsKernelFunction()));
#pragma warning restore SKEXP0001

            // Chat loop
            Console.WriteLine("Chat with the AI. Type 'exit' to stop.");
            var history = new ChatHistory();
            history.AddSystemMessage("You are an assistant that can call MCP tools to process user queries.");

            // Get chat completion service
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            while (true)
            {
                Console.Write("User > ");
                var input = Console.ReadLine();
                if (input?.Trim().ToLower() == "exit") break;

                history.AddUserMessage(input);

                // Enable auto function calling
                OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                // Get the response from the AI
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    history,
                    executionSettings: openAIPromptExecutionSettings,
                    kernel: kernel);

                Console.WriteLine($"Assistant > {result.Content}");
                history.AddMessage(result.Role, result.Content ?? string.Empty);
            }
        }
    }
}
