using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public class Achievement
{
    public int Id { get; set; }
    
    [Required]
    public string StudentId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string BadgeTitle { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string BadgeIcon { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    
    public int? CourseId { get; set; }
    public int? ExamAttemptId { get; set; }
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Course? Course { get; set; }
    public ExamAttempt? ExamAttempt { get; set; }
}

public class StudentActivityFeed
{
    public int Id { get; set; }
    
    [Required]
    public string StudentId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string ActivityText { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string ActivityIcon { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int? AchievementId { get; set; }
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Achievement? Achievement { get; set; }
}