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
        public string StatusMessage { get; set; }
        public int MessageIndex = 0;
        protected override async Task OnInitializedAsync()
        {
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
            
            await AIChat.SendMessage(incomingMessage, transactionDate, async (res) =>
            {
                var aiMsg = MessagesList.FindLast(m => m.ID == MessageIndex);
                if (aiMsg != null)
                {
                    aiMsg.Content = res;
                }
                await ScrollToBottomAsync();
                await InvokeAsync(StateHasChanged);
            }, async (res) =>
            {
                var processedDate = DateTime.Now;
                // Replace the last "(Thinking...)" message with the actual response
                var aiMsg = MessagesList.FindLast(m => !m.IsFromUser && m.Content == "(Thinking...)");
                if (aiMsg != null)
                {
                    aiMsg.Content = res;
                    aiMsg.Timestamp = processedDate;
                }
                await ScrollToBottomAsync();
                await InvokeAsync(StateHasChanged);
            });
        }

        private async Task ScrollToBottomAsync()
        {
            await JS.InvokeVoidAsync("window.scrollToBottom");
        }

        public async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (e.Key == "Enter" && !e.ShiftKey)
            {
                await SendMessage();
            }
        }
    }
}
