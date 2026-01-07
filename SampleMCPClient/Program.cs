using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using SampleMCPClient;

// Configure Semantic Kernel
var builder = Kernel.CreateBuilder();
builder.Services.AddOpenAIChatCompletion(
    //modelId: "llama3.2",
    modelId: "incept5/llama3.1-claude:latest",
    apiKey: null, // No API key needed for Ollama
    endpoint: new Uri("http://localhost:11434/v1") // Ollama server endpoint
);
//string OllamaAPI_URL = "https://ollama.com/api/chat";
//string OllamaAPI_KEY = "aee2d891fea94764b7e0bd508bb2f95e.Lv4W9eMkgvelgp_kShQw3JW5";

//builder.Services.AddOpenAIChatCompletion(
//    //modelId: "llama3.2",
//    modelId: "gpt-oss:120b",
//    apiKey: OllamaAPI_KEY, // No API key needed for Ollama
//    endpoint: new Uri(OllamaAPI_URL) // Ollama server endpoint
);
var kernel = builder.Build();

await  MyMCPClient.MCPClientSetup(kernel);