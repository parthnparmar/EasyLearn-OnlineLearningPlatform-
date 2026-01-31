using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public class Game
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public GameType Type { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<GameScore> Scores { get; set; } = new List<GameScore>();
}

public enum GameType
{
    MemoryCardMatching = 1,
    PatternMemory = 2,
    NumberRecall = 3,
    ImageMemory = 4,
    NumberGuessing = 5,
    ArithmeticChallenge = 6,
    FastCalculation = 7,
    SequenceCompletion = 8
}

public class GameScore
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Level { get; set; }
    public TimeSpan TimeTaken { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Game Game { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}