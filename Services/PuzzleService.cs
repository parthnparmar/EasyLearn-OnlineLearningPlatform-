using EasyLearn.Models;
using EasyLearn.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyLearn.Services;

public interface IPuzzleService
{
    Task<List<PuzzleGame>> GetAvailableGamesAsync();
    Task<PuzzleGame?> GetGameAsync(int gameId);
    Task<PuzzleAttempt?> StartGameAsync(string userId, int gameId);
    Task<PuzzleAttempt?> GetActiveAttemptAsync(string userId, int gameId);
    Task<bool> MakeMoveAsync(int attemptId, string moveData);
    Task<string> GetHintAsync(int attemptId);
    Task<PuzzleAttempt?> AutoSolveAsync(int attemptId);
    Task<PuzzleAttempt?> CompleteGameAsync(int attemptId);
    Task<PuzzleStats> GetUserStatsAsync(string userId);
    Task<List<PuzzleLeaderboardEntry>> GetLeaderboardAsync(int gameId, string type = "score");
    
    // Sudoku specific methods
    SudokuGrid GenerateSudoku(DifficultyLevel difficulty);
    bool ValidateSudokuMove(SudokuGrid grid, int row, int col, int value);
    SudokuGrid SolveSudoku(SudokuGrid grid);
    string GetSudokuHint(SudokuGrid grid);
    
    // Word Puzzle specific methods
    WordPuzzleGrid GenerateWordPuzzle(DifficultyLevel difficulty);
    bool ValidateWordSelection(WordPuzzleGrid grid, int startRow, int startCol, int endRow, int endCol);
    List<string> GetWordPuzzleHints(WordPuzzleGrid grid);
    
    // Logic Puzzle specific methods
    LogicPuzzleState GenerateLogicPuzzle(DifficultyLevel difficulty);
    bool ValidateLogicMove(LogicPuzzleState state, string moveData);
    LogicPuzzleState SolveLogicPuzzle(LogicPuzzleState state);
}

public class PuzzleService : IPuzzleService
{
    private readonly ApplicationDbContext _context;
    private readonly Random _random;

    public PuzzleService(ApplicationDbContext context)
    {
        _context = context;
        _random = new Random();
    }

    public async Task<List<PuzzleGame>> GetAvailableGamesAsync()
    {
        return await _context.PuzzleGames
            .Where(pg => pg.IsActive)
            .OrderBy(pg => pg.Type)
            .ThenBy(pg => pg.Difficulty)
            .ToListAsync();
    }

    public async Task<PuzzleGame?> GetGameAsync(int gameId)
    {
        return await _context.PuzzleGames
            .FirstOrDefaultAsync(pg => pg.Id == gameId && pg.IsActive);
    }

    public async Task<PuzzleAttempt?> StartGameAsync(string userId, int gameId)
    {
        var game = await GetGameAsync(gameId);
        if (game == null) return null;

        // Check for existing active attempt
        var existingAttempt = await GetActiveAttemptAsync(userId, gameId);
        if (existingAttempt != null) return existingAttempt;

        var attempt = new PuzzleAttempt
        {
            UserId = userId,
            PuzzleGameId = gameId,
            CurrentState = game.InitialState,
            Status = GameStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };

        _context.PuzzleAttempts.Add(attempt);
        await _context.SaveChangesAsync();

        return attempt;
    }

    public async Task<PuzzleAttempt?> GetActiveAttemptAsync(string userId, int gameId)
    {
        return await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.UserId == userId && 
                                     pa.PuzzleGameId == gameId && 
                                     pa.Status == GameStatus.InProgress);
    }

    public async Task<bool> MakeMoveAsync(int attemptId, string moveData)
    {
        var attempt = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.Id == attemptId);

        if (attempt == null || attempt.Status != GameStatus.InProgress) return false;

        // Validate move based on puzzle type
        bool isValidMove = attempt.PuzzleGame.Type switch
        {
            PuzzleType.Sudoku => ValidateSudokuMoveFromJson(attempt.CurrentState, moveData),
            PuzzleType.WordPuzzle => ValidateWordPuzzleMoveFromJson(attempt.CurrentState, moveData),
            PuzzleType.LogicPuzzle => ValidateLogicPuzzleMoveFromJson(attempt.CurrentState, moveData),
            _ => true
        };

        if (!isValidMove) return false;

        // Update attempt state
        attempt.CurrentState = UpdateGameState(attempt.CurrentState, moveData, attempt.PuzzleGame.Type);
        attempt.MovesCount++;

        // Record the move
        var move = new PuzzleMove
        {
            PuzzleAttemptId = attemptId,
            MoveData = moveData,
            MadeAt = DateTime.UtcNow
        };

        _context.PuzzleMoves.Add(move);
        await _context.SaveChangesAsync();

        // Check if puzzle is completed
        if (IsPuzzleCompleted(attempt.CurrentState, attempt.PuzzleGame.Solution, attempt.PuzzleGame.Type))
        {
            await CompleteGameAsync(attemptId);
        }

        return true;
    }

    public async Task<string> GetHintAsync(int attemptId)
    {
        var attempt = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.Id == attemptId);

        if (attempt == null || attempt.Status != GameStatus.InProgress) return string.Empty;

        attempt.HintsUsed++;
        await _context.SaveChangesAsync();

        return attempt.PuzzleGame.Type switch
        {
            PuzzleType.Sudoku => GetSudokuHintFromJson(attempt.CurrentState),
            PuzzleType.WordPuzzle => GetWordPuzzleHintFromJson(attempt.CurrentState),
            PuzzleType.LogicPuzzle => GetLogicPuzzleHintFromJson(attempt.CurrentState),
            _ => "No hints available for this puzzle type."
        };
    }

    public async Task<PuzzleAttempt?> AutoSolveAsync(int attemptId)
    {
        var attempt = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.Id == attemptId);

        if (attempt == null || attempt.Status != GameStatus.InProgress) return null;

        attempt.CurrentState = attempt.PuzzleGame.Solution;
        attempt.Status = GameStatus.AutoSolved;
        attempt.CompletedAt = DateTime.UtcNow;
        attempt.TimeTakenSeconds = (int)(DateTime.UtcNow - attempt.StartedAt).TotalSeconds;
        attempt.Score = 0; // No score for auto-solved puzzles

        await _context.SaveChangesAsync();
        await UpdateLeaderboardAsync(attempt);

        return attempt;
    }

    public async Task<PuzzleAttempt?> CompleteGameAsync(int attemptId)
    {
        var attempt = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.Id == attemptId);

        if (attempt == null) return null;

        attempt.Status = GameStatus.Completed;
        attempt.CompletedAt = DateTime.UtcNow;
        attempt.TimeTakenSeconds = (int)(DateTime.UtcNow - attempt.StartedAt).TotalSeconds;

        // Calculate score
        attempt.Score = CalculateScore(attempt);

        await _context.SaveChangesAsync();
        await UpdateLeaderboardAsync(attempt);

        return attempt;
    }

    public async Task<PuzzleStats> GetUserStatsAsync(string userId)
    {
        var attempts = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .Where(pa => pa.UserId == userId)
            .ToListAsync();

        var completedAttempts = attempts.Where(a => a.Status == GameStatus.Completed).ToList();

        return new PuzzleStats
        {
            TotalGamesPlayed = attempts.Count,
            TotalGamesCompleted = completedAttempts.Count,
            TotalScore = completedAttempts.Sum(a => a.Score),
            AverageScore = completedAttempts.Any() ? (int)completedAttempts.Average(a => a.Score) : 0,
            TotalTimePlayed = attempts.Sum(a => a.TimeTakenSeconds),
            FavoriteGameType = attempts.GroupBy(a => a.PuzzleGame.Type)
                                     .OrderByDescending(g => g.Count())
                                     .FirstOrDefault()?.Key.ToString() ?? "None"
        };
    }

    public async Task<List<PuzzleLeaderboardEntry>> GetLeaderboardAsync(int gameId, string type = "score")
    {
        var query = _context.PuzzleLeaderboards
            .Include(pl => pl.User)
            .Where(pl => pl.PuzzleGameId == gameId);

        var leaderboard = type.ToLower() switch
        {
            "time" => await query.OrderBy(pl => pl.BestTimeSeconds).Take(10).ToListAsync(),
            _ => await query.OrderByDescending(pl => pl.BestScore).Take(10).ToListAsync()
        };

        return leaderboard.Select(pl => new PuzzleLeaderboardEntry
        {
            UserName = $"{pl.User.FirstName} {pl.User.LastName}".Trim(),
            Score = pl.BestScore,
            TimeSeconds = pl.BestTimeSeconds,
            Attempts = pl.TotalAttempts,
            LastPlayed = pl.LastPlayedAt
        }).ToList();
    }

    // Sudoku Implementation
    public SudokuGrid GenerateSudoku(DifficultyLevel difficulty)
    {
        var grid = new SudokuGrid();
        
        // Generate a complete valid Sudoku
        GenerateCompleteSudoku(grid.Grid);
        
        // Remove numbers based on difficulty
        int cellsToRemove = difficulty switch
        {
            DifficultyLevel.Easy => 35,
            DifficultyLevel.Medium => 45,
            DifficultyLevel.Hard => 55,
            DifficultyLevel.Expert => 65,
            _ => 40
        };

        RemoveCells(grid, cellsToRemove);
        return grid;
    }

    public bool ValidateSudokuMove(SudokuGrid grid, int row, int col, int value)
    {
        if (row < 0 || row >= 9 || col < 0 || col >= 9) return false;
        if (value < 1 || value > 9) return false;
        if (grid.IsFixed[row, col]) return false;

        // Check row
        for (int c = 0; c < 9; c++)
        {
            if (c != col && grid.Grid[row, c] == value) return false;
        }

        // Check column
        for (int r = 0; r < 9; r++)
        {
            if (r != row && grid.Grid[r, col] == value) return false;
        }

        // Check 3x3 box
        int boxRow = (row / 3) * 3;
        int boxCol = (col / 3) * 3;
        for (int r = boxRow; r < boxRow + 3; r++)
        {
            for (int c = boxCol; c < boxCol + 3; c++)
            {
                if ((r != row || c != col) && grid.Grid[r, c] == value) return false;
            }
        }

        return true;
    }

    public SudokuGrid SolveSudoku(SudokuGrid grid)
    {
        var solvedGrid = new SudokuGrid();
        Array.Copy(grid.Grid, solvedGrid.Grid, 81);
        Array.Copy(grid.IsFixed, solvedGrid.IsFixed, 81);

        SolveSudokuRecursive(solvedGrid.Grid);
        return solvedGrid;
    }

    public string GetSudokuHint(SudokuGrid grid)
    {
        // Find the first empty cell that has only one possible value
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                if (grid.Grid[row, col] == 0)
                {
                    var possibleValues = new List<int>();
                    for (int val = 1; val <= 9; val++)
                    {
                        if (ValidateSudokuMove(grid, row, col, val))
                        {
                            possibleValues.Add(val);
                        }
                    }

                    if (possibleValues.Count == 1)
                    {
                        return $"Try placing {possibleValues[0]} in row {row + 1}, column {col + 1}";
                    }
                    else if (possibleValues.Count <= 3)
                    {
                        return $"Row {row + 1}, column {col + 1} can only be: {string.Join(", ", possibleValues)}";
                    }
                }
            }
        }

        return "Look for cells with the fewest possible values";
    }

    // Word Puzzle Implementation
    public WordPuzzleGrid GenerateWordPuzzle(DifficultyLevel difficulty)
    {
        var grid = new WordPuzzleGrid();
        var words = GetWordList(difficulty);
        
        // Initialize grid with random letters
        for (int i = 0; i < 15; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                grid.Grid[i, j] = (char)('A' + _random.Next(26));
            }
        }

        // Place words in the grid
        foreach (var word in words.Take(difficulty switch
        {
            DifficultyLevel.Easy => 8,
            DifficultyLevel.Medium => 12,
            DifficultyLevel.Hard => 16,
            DifficultyLevel.Expert => 20,
            _ => 10
        }))
        {
            if (PlaceWordInGrid(grid, word))
            {
                grid.Words.Add(new WordPuzzleWord { Word = word });
            }
        }

        return grid;
    }

    public bool ValidateWordSelection(WordPuzzleGrid grid, int startRow, int startCol, int endRow, int endCol)
    {
        var selectedWord = ExtractWordFromSelection(grid, startRow, startCol, endRow, endCol);
        return grid.Words.Any(w => w.Word.Equals(selectedWord, StringComparison.OrdinalIgnoreCase) && !w.IsFound);
    }

    public List<string> GetWordPuzzleHints(WordPuzzleGrid grid)
    {
        var hints = new List<string>();
        var unFoundWords = grid.Words.Where(w => !w.IsFound).Take(3);
        
        foreach (var word in unFoundWords)
        {
            hints.Add($"Look for: {word.Word.Substring(0, Math.Min(3, word.Word.Length))}...");
        }

        return hints;
    }

    // Logic Puzzle Implementation
    public LogicPuzzleState GenerateLogicPuzzle(DifficultyLevel difficulty)
    {
        // Generate a simple number sequence puzzle
        var sequence = GenerateNumberSequence(difficulty);
        
        return new LogicPuzzleState
        {
            PuzzleType = "NumberSequence",
            State = new Dictionary<string, object>
            {
                ["sequence"] = sequence.Take(sequence.Count - 1).ToList(),
                ["answer"] = sequence.Last(),
                ["pattern"] = GetSequencePattern(sequence)
            },
            Rules = new List<string> { "Find the next number in the sequence" },
            Clues = new List<string> { "Look for mathematical patterns", "Consider arithmetic or geometric progressions" }
        };
    }

    public bool ValidateLogicMove(LogicPuzzleState state, string moveData)
    {
        if (state.PuzzleType == "NumberSequence")
        {
            if (int.TryParse(moveData, out int answer))
            {
                return answer == (int)state.State["answer"];
            }
        }
        return false;
    }

    public LogicPuzzleState SolveLogicPuzzle(LogicPuzzleState state)
    {
        var solvedState = new LogicPuzzleState
        {
            PuzzleType = state.PuzzleType,
            State = new Dictionary<string, object>(state.State),
            Rules = state.Rules,
            Clues = state.Clues
        };

        solvedState.State["userAnswer"] = solvedState.State["answer"];
        return solvedState;
    }

    // Helper Methods
    private bool ValidateSudokuMoveFromJson(string currentState, string moveData)
    {
        try
        {
            var grid = JsonSerializer.Deserialize<SudokuGrid>(currentState);
            var move = JsonSerializer.Deserialize<SudokuMove>(moveData);
            return ValidateSudokuMove(grid!, move!.Row, move.Col, move.Value);
        }
        catch
        {
            return false;
        }
    }

    private bool ValidateWordPuzzleMoveFromJson(string currentState, string moveData)
    {
        try
        {
            var grid = JsonSerializer.Deserialize<WordPuzzleGrid>(currentState);
            var selection = JsonSerializer.Deserialize<Dictionary<string, int>>(moveData);
            return ValidateWordSelection(grid!, 
                selection!["startRow"], selection["startCol"], 
                selection["endRow"], selection["endCol"]);
        }
        catch
        {
            return false;
        }
    }

    private bool ValidateLogicPuzzleMoveFromJson(string currentState, string moveData)
    {
        try
        {
            var state = JsonSerializer.Deserialize<LogicPuzzleState>(currentState);
            return ValidateLogicMove(state!, moveData);
        }
        catch
        {
            return false;
        }
    }

    private string UpdateGameState(string currentState, string moveData, PuzzleType type)
    {
        // Update the game state based on the move and puzzle type
        // This is a simplified implementation
        return currentState; // In a real implementation, this would update the state
    }

    private bool IsPuzzleCompleted(string currentState, string solution, PuzzleType type)
    {
        // Check if the current state matches the solution
        return currentState == solution; // Simplified check
    }

    private string GetSudokuHintFromJson(string currentState)
    {
        try
        {
            var grid = JsonSerializer.Deserialize<SudokuGrid>(currentState);
            return GetSudokuHint(grid!);
        }
        catch
        {
            return "Unable to generate hint";
        }
    }

    private string GetWordPuzzleHintFromJson(string currentState)
    {
        try
        {
            var grid = JsonSerializer.Deserialize<WordPuzzleGrid>(currentState);
            var hints = GetWordPuzzleHints(grid!);
            return hints.FirstOrDefault() ?? "No hints available";
        }
        catch
        {
            return "Unable to generate hint";
        }
    }

    private string GetLogicPuzzleHintFromJson(string currentState)
    {
        try
        {
            var state = JsonSerializer.Deserialize<LogicPuzzleState>(currentState);
            return state!.Clues.FirstOrDefault() ?? "No hints available";
        }
        catch
        {
            return "Unable to generate hint";
        }
    }

    private int CalculateScore(PuzzleAttempt attempt)
    {
        var baseScore = attempt.PuzzleGame.MaxScore;
        var timePenalty = Math.Min(attempt.TimeTakenSeconds / 10, baseScore / 2);
        var hintPenalty = attempt.HintsUsed * 10;
        
        return Math.Max(0, baseScore - timePenalty - hintPenalty);
    }

    private async Task UpdateLeaderboardAsync(PuzzleAttempt attempt)
    {
        var leaderboard = await _context.PuzzleLeaderboards
            .FirstOrDefaultAsync(pl => pl.UserId == attempt.UserId && pl.PuzzleGameId == attempt.PuzzleGameId);

        if (leaderboard == null)
        {
            leaderboard = new PuzzleLeaderboard
            {
                UserId = attempt.UserId,
                PuzzleGameId = attempt.PuzzleGameId,
                BestScore = attempt.Score,
                BestTimeSeconds = attempt.TimeTakenSeconds,
                TotalAttempts = 1,
                CompletedAttempts = attempt.Status == GameStatus.Completed ? 1 : 0
            };
            _context.PuzzleLeaderboards.Add(leaderboard);
        }
        else
        {
            if (attempt.Score > leaderboard.BestScore)
                leaderboard.BestScore = attempt.Score;
            
            if (attempt.Status == GameStatus.Completed && 
                (leaderboard.BestTimeSeconds == 0 || attempt.TimeTakenSeconds < leaderboard.BestTimeSeconds))
                leaderboard.BestTimeSeconds = attempt.TimeTakenSeconds;

            leaderboard.TotalAttempts++;
            if (attempt.Status == GameStatus.Completed)
                leaderboard.CompletedAttempts++;
        }

        leaderboard.LastPlayedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    // Sudoku helper methods
    private void GenerateCompleteSudoku(int[,] grid)
    {
        // Simple backtracking algorithm to generate a complete Sudoku
        SolveSudokuRecursive(grid);
    }

    private bool SolveSudokuRecursive(int[,] grid)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                if (grid[row, col] == 0)
                {
                    var numbers = Enumerable.Range(1, 9).OrderBy(x => _random.Next()).ToList();
                    foreach (int num in numbers)
                    {
                        if (IsValidSudokuPlacement(grid, row, col, num))
                        {
                            grid[row, col] = num;
                            if (SolveSudokuRecursive(grid))
                                return true;
                            grid[row, col] = 0;
                        }
                    }
                    return false;
                }
            }
        }
        return true;
    }

    private bool IsValidSudokuPlacement(int[,] grid, int row, int col, int num)
    {
        // Check row
        for (int c = 0; c < 9; c++)
            if (grid[row, c] == num) return false;

        // Check column
        for (int r = 0; r < 9; r++)
            if (grid[r, col] == num) return false;

        // Check 3x3 box
        int boxRow = (row / 3) * 3;
        int boxCol = (col / 3) * 3;
        for (int r = boxRow; r < boxRow + 3; r++)
            for (int c = boxCol; c < boxCol + 3; c++)
                if (grid[r, c] == num) return false;

        return true;
    }

    private void RemoveCells(SudokuGrid grid, int count)
    {
        var positions = new List<(int, int)>();
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                positions.Add((i, j));

        positions = positions.OrderBy(x => _random.Next()).Take(count).ToList();

        foreach (var (row, col) in positions)
        {
            grid.IsFixed[row, col] = grid.Grid[row, col] != 0;
            if (!grid.IsFixed[row, col])
                grid.Grid[row, col] = 0;
        }
    }

    // Word puzzle helper methods
    private List<string> GetWordList(DifficultyLevel difficulty)
    {
        return difficulty switch
        {
            DifficultyLevel.Easy => new List<string> { "CAT", "DOG", "SUN", "MOON", "TREE", "BOOK", "FISH", "BIRD", "STAR", "RAIN" },
            DifficultyLevel.Medium => new List<string> { "COMPUTER", "ELEPHANT", "MOUNTAIN", "RAINBOW", "BUTTERFLY", "KEYBOARD", "TELEPHONE", "UMBRELLA" },
            DifficultyLevel.Hard => new List<string> { "PROGRAMMING", "ALGORITHM", "DEVELOPMENT", "ARCHITECTURE", "OPTIMIZATION", "IMPLEMENTATION" },
            DifficultyLevel.Expert => new List<string> { "CRYPTOCURRENCY", "BIOTECHNOLOGY", "NANOTECHNOLOGY", "TELECOMMUNICATIONS", "INTERDISCIPLINARY" },
            _ => new List<string> { "WORD", "PUZZLE", "GAME" }
        };
    }

    private bool PlaceWordInGrid(WordPuzzleGrid grid, string word)
    {
        // Simplified word placement - in a real implementation, this would be more sophisticated
        return true;
    }

    private string ExtractWordFromSelection(WordPuzzleGrid grid, int startRow, int startCol, int endRow, int endCol)
    {
        // Extract the selected word from the grid
        return "WORD"; // Simplified implementation
    }

    // Logic puzzle helper methods
    private List<int> GenerateNumberSequence(DifficultyLevel difficulty)
    {
        return difficulty switch
        {
            DifficultyLevel.Easy => new List<int> { 2, 4, 6, 8, 10 }, // Even numbers
            DifficultyLevel.Medium => new List<int> { 1, 4, 9, 16, 25 }, // Perfect squares
            DifficultyLevel.Hard => new List<int> { 1, 1, 2, 3, 5, 8 }, // Fibonacci
            DifficultyLevel.Expert => new List<int> { 2, 3, 5, 7, 11, 13 }, // Prime numbers
            _ => new List<int> { 1, 2, 3, 4, 5 }
        };
    }

    private string GetSequencePattern(List<int> sequence)
    {
        // Analyze the sequence to determine the pattern
        return "arithmetic"; // Simplified implementation
    }
}