using EasyLearn.Models;
using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models.ViewModels;

public class PuzzleGameViewModel
{
    public PuzzleGame Game { get; set; } = null!;
    public PuzzleAttempt? CurrentAttempt { get; set; }
    public bool HasActiveAttempt { get; set; }
    public int BestScore { get; set; }
    public int BestTime { get; set; }
    public int TotalAttempts { get; set; }
}

public class PuzzlePlayViewModel
{
    public PuzzleGame Game { get; set; } = null!;
    public PuzzleAttempt Attempt { get; set; } = null!;
    public string GameState { get; set; } = string.Empty;
    public int TimeRemaining { get; set; }
    public bool CanUseHints { get; set; } = true;
    public int MaxHints { get; set; } = 3;
}

public class PuzzleResultViewModel
{
    public PuzzleAttempt Attempt { get; set; } = null!;
    public PuzzleGame Game { get; set; } = null!;
    public bool IsNewBestScore { get; set; }
    public bool IsNewBestTime { get; set; }
    public int Rank { get; set; }
    public string PerformanceMessage { get; set; } = string.Empty;
}

public class PuzzleLeaderboardViewModel
{
    public PuzzleGame Game { get; set; } = null!;
    public List<PuzzleLeaderboardEntry> TopScores { get; set; } = new();
    public List<PuzzleLeaderboardEntry> TopTimes { get; set; } = new();
    public PuzzleLeaderboardEntry? UserStats { get; set; }
}

public class PuzzleLeaderboardEntry
{
    public string UserName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int TimeSeconds { get; set; }
    public int Attempts { get; set; }
    public DateTime LastPlayed { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class PuzzleDashboardViewModel
{
    public List<PuzzleGame> AvailableGames { get; set; } = new();
    public List<PuzzleAttempt> RecentAttempts { get; set; } = new();
    public PuzzleStats UserStats { get; set; } = new();
    public List<PuzzleLeaderboardEntry> TopPlayers { get; set; } = new();
}

public class PuzzleStats
{
    public int TotalGamesPlayed { get; set; }
    public int TotalGamesCompleted { get; set; }
    public int TotalScore { get; set; }
    public int AverageScore { get; set; }
    public int TotalTimePlayed { get; set; }
    public int BestStreak { get; set; }
    public string FavoriteGameType { get; set; } = string.Empty;
}

// Sudoku specific models
public class SudokuGrid
{
    public int[,] Grid { get; set; } = new int[9, 9];
    public bool[,] IsFixed { get; set; } = new bool[9, 9];
    public bool[,] HasError { get; set; } = new bool[9, 9];
}

public class SudokuMove
{
    public int Row { get; set; }
    public int Col { get; set; }
    public int Value { get; set; }
    public int PreviousValue { get; set; }
}

// Word Puzzle specific models
public class WordPuzzleGrid
{
    public char[,] Grid { get; set; } = new char[15, 15];
    public List<WordPuzzleWord> Words { get; set; } = new();
    public List<WordPuzzleWord> FoundWords { get; set; } = new();
}

public class WordPuzzleWord
{
    public string Word { get; set; } = string.Empty;
    public int StartRow { get; set; }
    public int StartCol { get; set; }
    public int EndRow { get; set; }
    public int EndCol { get; set; }
    public bool IsFound { get; set; }
    public string Direction { get; set; } = string.Empty; // horizontal, vertical, diagonal
}

// Logic Puzzle specific models
public class LogicPuzzleState
{
    public string PuzzleType { get; set; } = string.Empty;
    public Dictionary<string, object> State { get; set; } = new();
    public List<string> Rules { get; set; } = new();
    public List<string> Clues { get; set; } = new();
}