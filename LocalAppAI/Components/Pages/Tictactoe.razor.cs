using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAppAI.Components.Pages
{
    public partial class Tictactoe : ComponentBase
    {
        [Inject]
        public IJSRuntime JS { get; set; }

        private string[] board = new string[9];
        private bool isCircleTurn = true;
        private string gameMessage = "";
        private bool gameOver = false;
        private List<int> winningCells = new List<int>();

        protected override void OnInitialized()
        {
            ResetGame();
        }

        private async Task HandleCellClick(int index)
        {
            if (gameOver || !string.IsNullOrEmpty(board[index]))
            {
                return;
            }

            board[index] = isCircleTurn ? "O" : "X";
            
            await InvokeAsync(StateHasChanged);

            if (CheckWinner())
            {
                string winner = isCircleTurn ? "Circle" : "X";
                gameMessage = $"{winner} wins!";
                gameOver = true;
            }
            else if (IsBoardFull())
            {
                gameMessage = "It's a draw!";
                gameOver = true;
            }
            else
            {
                isCircleTurn = !isCircleTurn;
            }

            await InvokeAsync(StateHasChanged);
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

        private void ResetGame()
        {
            board = new string[9];
            isCircleTurn = true;
            gameMessage = "";
            gameOver = false;
            winningCells = new List<int>();
            StateHasChanged();
        }
    }
}
