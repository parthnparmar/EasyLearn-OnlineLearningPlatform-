using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public enum PuzzleType
{
    Sudoku,
    WordPuzzle,
    LogicPuzzle,
    NumberSequence,
    PatternMatching
}

public enum DifficultyLevel
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
    Expert = 4
}

public enum GameStatus
{
    InProgress,
    Completed,
    Abandoned,
    AutoSolved
}

public class PuzzleGame
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public PuzzleType Type { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    
    [Required]
    public string InitialState { get; set; } = string.Empty; // JSON representation
    
    [Required]
    public string Solution { get; set; } = string.Empty; // JSON representation
    
    public int MaxScore { get; set; } = 100;
    public int TimeLimit { get; set; } = 0; // 0 = no limit, in seconds
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public List<PuzzleAttempt> Attempts { get; set; } = new();
}

public class PuzzleAttempt
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int PuzzleGameId { get; set; }
    
    public string CurrentState { get; set; } = string.Empty; // JSON representation
    
    public GameStatus Status { get; set; } = GameStatus.InProgress;
    
    public int Score { get; set; } = 0;
    public int HintsUsed { get; set; } = 0;
    public int MovesCount { get; set; } = 0;
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    public int TimeTakenSeconds { get; set; } = 0;
    
    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public PuzzleGame PuzzleGame { get; set; } = null!;
    public List<PuzzleMove> Moves { get; set; } = new();
}

public class PuzzleMove
{
    public int Id { get; set; }
    
    public int PuzzleAttemptId { get; set; }
    
    [Required]
    public string MoveData { get; set; } = string.Empty; // JSON representation of the move
    
    public DateTime MadeAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public PuzzleAttempt PuzzleAttempt { get; set; } = null!;
}

public class PuzzleLeaderboard
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int PuzzleGameId { get; set; }
    
    public int BestScore { get; set; }
    public int BestTimeSeconds { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public PuzzleGame PuzzleGame { get; set; } = null!;
}