using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models;

public class Exam
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public int DurationMinutes { get; set; } = 120;
    public bool IsApproved { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Auto exam control
    public bool IsAutoScheduled { get; set; } = true;
    public bool RequiresPreVerification { get; set; } = true;
    
    // Auto exam time control
    public bool IsExamTimeActive()
    {
        var now = DateTime.Now;
        return now >= ScheduledStartTime && now <= ScheduledEndTime;
    }
    
    public bool IsExamMissed()
    {
        return DateTime.Now > ScheduledEndTime;
    }
    
    public TimeSpan GetTimeUntilStart()
    {
        var now = DateTime.Now;
        return now < ScheduledStartTime ? ScheduledStartTime - now : TimeSpan.Zero;
    }
    
    public TimeSpan GetTimeRemaining()
    {
        var now = DateTime.Now;
        return now < ScheduledEndTime ? ScheduledEndTime - now : TimeSpan.Zero;
    }
    
    // Exam Pattern Configuration (Instructor Decision)
    public ExamPatternType PatternType { get; set; } = ExamPatternType.Mixed;
    public string InstructorInstructions { get; set; } = string.Empty; // Instructions to students
    
    // Exam Structure
    public int PartAMarks { get; set; } = 50; // MCQs
    public int PartBMarks { get; set; } = 30; // Written
    public int InternalMarks { get; set; } = 20; // Internal Assessment
    public int TotalMarks { get; set; } = 100;
    public int PassingPercentage { get; set; } = 75;
    
    // Time limits in minutes
    public int PartATimeLimit { get; set; } = 60;
    public int PartBTimeLimit { get; set; } = 60;
    
    // Foreign keys
    public int CourseId { get; set; }
    public string InstructorId { get; set; } = string.Empty;
    
    // Navigation properties
    public Course Course { get; set; } = null!;
    public ApplicationUser Instructor { get; set; } = null!;
    public ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
    public ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}

public class ExamSchedule
{
    public int Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public string Session { get; set; } = string.Empty;
    public bool IsAssigned { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Foreign keys
    public int ExamId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    
    // Navigation properties
    public Exam Exam { get; set; } = null!;
    public ApplicationUser Student { get; set; } = null!;
}

public class MissedExamRequest
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int ExamId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public bool IsApproved { get; set; } = false;
    public bool IsRejected { get; set; } = false;
    public string? InstructorResponse { get; set; }
    public DateTime? ResponseAt { get; set; }
    public string? InstructorId { get; set; }
    
    // Navigation properties
    public ApplicationUser Student { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public ApplicationUser? Instructor { get; set; }
}

public class ExamAttempt
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; } = false;
    
    // Scores
    public int PartAScore { get; set; } = 0;
    public int PartBScore { get; set; } = 0;
    public int InternalScore { get; set; } = 0;
    public int TotalScore { get; set; } = 0;
    public double Percentage { get; set; } = 0;
    public bool IsPassed { get; set; } = false;
    
    // Status tracking
    public bool PartACompleted { get; set; } = false;
    public bool PartBCompleted { get; set; } = false;
    public bool InternalAssigned { get; set; } = false;
    public bool ResultPublished { get; set; } = false;
    public DateTime? ResultPublishedAt { get; set; }
    
    // Foreign keys
    public int ExamId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int? ExamScheduleId { get; set; }
    
    // Navigation properties
    public Exam Exam { get; set; } = null!;
    public ApplicationUser Student { get; set; } = null!;
    public ExamSchedule? ExamSchedule { get; set; }
    public ICollection<ExamAnswer> ExamAnswers { get; set; } = new List<ExamAnswer>();
    public ExamCertificate? ExamCertificate { get; set; }
}

public class ExamQuestion
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(1000)]
    public string Text { get; set; } = string.Empty;
    
    public ExamQuestionType Type { get; set; }
    public ExamPart Part { get; set; } // Part A or Part B
    public int Points { get; set; } = 2; // Default 2 points for MCQs (Part A)
    public int OrderIndex { get; set; }
    
    // Foreign key
    public int ExamId { get; set; }
    
    // Navigation properties
    public Exam Exam { get; set; } = null!;
    public ICollection<ExamQuestionOption> Options { get; set; } = new List<ExamQuestionOption>();
}

public class ExamQuestionOption
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(500)]
    public string Text { get; set; } = string.Empty;
    
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
    
    // Foreign key
    public int ExamQuestionId { get; set; }
    
    // Navigation property
    public ExamQuestion ExamQuestion { get; set; } = null!;
}

public class ExamAnswer
{
    public int Id { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int Points { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    
    // Foreign keys
    public int ExamAttemptId { get; set; }
    public int ExamQuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
    
    // Navigation properties
    public ExamAttempt ExamAttempt { get; set; } = null!;
    public ExamQuestion ExamQuestion { get; set; } = null!;
    public ExamQuestionOption? SelectedOption { get; set; }
}

public class ExamCertificate
{
    public int Id { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddYears(1);
    public string FilePath { get; set; } = string.Empty;
    public double Percentage { get; set; }
    
    // Foreign keys
    public int ExamAttemptId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int CourseId { get; set; }
    
    // Navigation properties
    public ExamAttempt ExamAttempt { get; set; } = null!;
    public ApplicationUser Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}

public enum ExamQuestionType
{
    MultipleChoice = 1,
    Written = 2
}

public enum ExamPart
{
    PartA = 1, // MCQs - 50 marks
    PartB = 2  // Written - 30 marks
}

public enum ExamPatternType
{
    TheoryOnly = 1,      // Only written/theory questions
    MCQOnly = 2,         // Only multiple choice questions
    Mixed = 3            // Combination of Theory and MCQs
}