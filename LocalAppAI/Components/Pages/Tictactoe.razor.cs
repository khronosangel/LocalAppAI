using LocalAppAI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LocalAppAI.Components.Pages
{
    public partial class Tictactoe : ComponentBase
    {
        [Inject]
        public IJSRuntime JS { get; set; }

        [Inject]
        public AIChatService AIChat { get; set; }

        private string[] board = new string[9];
        private List<int> toggleNumbers = new List<int>();
        private bool isCircleTurn = true;
        private string gameMessage = "";
        private bool gameOver = false;
        private List<int> winningCells = new List<int>();
        private bool isAIThinking = false;
        private string aiStatus = "";
        private List<ChatLogEntry> chatLog = new List<ChatLogEntry>();

        public class ChatLogEntry
        {
            public string Role { get; set; }
            public string Message { get; set; }
            public DateTime Timestamp { get; set; }
        }

        protected override async Task OnInitializedAsync()
        {
            aiStatus = "Initializing AI...";
            await InvokeAsync(StateHasChanged);

            await AIChat.PrepareAIModel(async (stat) =>
            {
                aiStatus = stat;
                await InvokeAsync(StateHasChanged);
            },
            async (done) =>
            {
                aiStatus = done;
                await InvokeAsync(StateHasChanged);
            });

            await Task.Delay(500);

            var transactionDate = DateTime.Now;
            string initialPrompt = @"We are playing Tic-Tac-Toe. You are player X (I am player O/Circle). 
The board has 9 positions numbered 0-8 (top-left to bottom-right, row by row):
0 1 2
3 4 5
6 7 8

When I tell you the current board state, respond ONLY with a single number (0-8) indicating where you want to place your X.
Do not explain your move, just respond with the position number only.
the Human player (me) is O/Circle and always goes first.
only respond with valid moves (positions that are not already taken) means only respond with a number.
we start after the human player (me) makes the first move.
also the format response should only be the number, no additional explanation before or after it.
Ready to play? just answer YES initially to start the game";

            chatLog.Add(new ChatLogEntry
            {
                Role = "System",
                Message = initialPrompt,
                Timestamp = transactionDate
            });

            aiStatus = "Setting up game with AI...";
            await InvokeAsync(StateHasChanged);

            await AIChat.SendMessage(initialPrompt, transactionDate, 
                async (res, tokens) => 
                {
                    // Streaming response
                }, 
                async (res, tokens) =>
                {
                    chatLog.Add(new ChatLogEntry
                    {
                        Role = "AI",
                        Message = res,
                        Timestamp = DateTime.Now
                    });
                    
                    aiStatus = "AI is ready!";
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(1000);
                    aiStatus = "";
                    await InvokeAsync(StateHasChanged);
                });

            StartGame();
        }

        private async Task HandleCellClick(int index)
        {
            if (gameOver || !string.IsNullOrEmpty(board[index]) || isAIThinking || !isCircleTurn)
            {
                return;
            }

            board[index] = "O";
            toggleNumbers.Add(index);

            await InvokeAsync(StateHasChanged);

            if (CheckWinner())
            {
                gameMessage = "Circle wins!";
                gameOver = true;
                await InvokeAsync(StateHasChanged);
                return;
            }
            else if (IsBoardFull())
            {
                gameMessage = "It's a draw!";
                gameOver = true;
                await InvokeAsync(StateHasChanged);
                return;
            }

            isCircleTurn = false;
            await InvokeAsync(StateHasChanged);

            await AIPlayerMove();
        }

        private async Task AIPlayerMove()
        {
            isAIThinking = true;
            aiStatus = "AI is thinking...";
            await InvokeAsync(StateHasChanged);

            var transactionDate = DateTime.Now;
            
            string boardState = GetBoardStateString();
            string prompt = $@"Current board state:
{boardState}

Where do you want to place your X? (respond with only a number 0-8 for an empty position and do not use these numbers {string.Join(',', toggleNumbers.ToArray())})";

            chatLog.Add(new ChatLogEntry
            {
                Role = "User",
                Message = prompt,
                Timestamp = transactionDate
            });
            await InvokeAsync(StateHasChanged);

            string aiResponse = "";

            await AIChat.SendMessage(prompt, transactionDate,
                async (res, tokens) =>
                {
                    // Streaming response
                },
                async (res, tokens) =>
                {
                    aiResponse = res;
                    chatLog.Add(new ChatLogEntry
                    {
                        Role = "AI",
                        Message = res,
                        Timestamp = DateTime.Now
                    });
                    await InvokeAsync(StateHasChanged);
                });

            int aiMove = ParseAIMove(aiResponse);

            if (aiMove >= 0 && aiMove < 9 && string.IsNullOrEmpty(board[aiMove]))
            {
                board[aiMove] = "X";
                
                await InvokeAsync(StateHasChanged);

                if (CheckWinner())
                {
                    gameMessage = "X wins!";
                    gameOver = true;
                }
                else if (IsBoardFull())
                {
                    gameMessage = "It's a draw!";
                    gameOver = true;
                }
                else
                {
                    isCircleTurn = true;
                }
            }
            else
            {
                gameMessage = "AI made an invalid move. You win by default!";
                gameOver = true;
            }

            isAIThinking = false;
            aiStatus = "";
            await InvokeAsync(StateHasChanged);
        }

        private string GetBoardStateString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 9; i++)
            {
                if (i > 0 && i % 3 == 0)
                {
                    sb.AppendLine();
                }
                
                string cell = string.IsNullOrEmpty(board[i]) ? i.ToString() : board[i];
                sb.Append(cell);
                
                if (i % 3 < 2)
                {
                    sb.Append(" | ");
                }
            }
            return sb.ToString();
        }

        private int ParseAIMove(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return -1;
            }

            var match = Regex.Match(response, @"\b([0-8])\b");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int move))
            {
                return move;
            }

            if (int.TryParse(response.Trim(), out int directMove) && directMove >= 0 && directMove <= 8)
            {
                return directMove;
            }

            return -1;
        }

        private bool CheckWinner()
        {
            int[,] winPatterns = new int[,]
            {
                { 0, 1, 2 },
                { 3, 4, 5 },
                { 6, 7, 8 },
                { 0, 3, 6 },
                { 1, 4, 7 },
                { 2, 5, 8 },
                { 0, 4, 8 },
                { 2, 4, 6 }
            };

            for (int i = 0; i < winPatterns.GetLength(0); i++)
            {
                int a = winPatterns[i, 0];
                int b = winPatterns[i, 1];
                int c = winPatterns[i, 2];

                if (!string.IsNullOrEmpty(board[a]) &&
                    board[a] == board[b] &&
                    board[a] == board[c])
                {
                    winningCells = new List<int> { a, b, c };
                    return true;
                }
            }

            return false;
        }

        private bool IsBoardFull()
        {
            return board.All(cell => !string.IsNullOrEmpty(cell));
        }

        private bool IsWinningCell(int index)
        {
            return winningCells.Contains(index);
        }

        private async void StartGame()
        {
            board = new string[9];
            toggleNumbers = new List<int>();
            isCircleTurn = true;
            gameMessage = "";
            gameOver = false;
            winningCells = new List<int>();
            isAIThinking = false;
            await InvokeAsync(StateHasChanged);
        }

        private async void ResetGame()
        {
            board = new string[9];
            toggleNumbers = new List<int>();
            isCircleTurn = true;
            gameMessage = "";
            gameOver = false;
            winningCells = new List<int>();
            isAIThinking = false;
            chatLog.Add(new ChatLogEntry
            {
                Role = "System",
                Message = "Now let's reset!, forgot everything and lets start over, say YES to start again",
                Timestamp = DateTime.Now
            });
            await InvokeAsync(StateHasChanged);
        }
    }
}
