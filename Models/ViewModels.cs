using System.ComponentModel.DataAnnotations;

namespace EasyLearn.Models.ViewModels;

public class EnrollmentViewModel
{
    [Required]
    [StringLength(100)]
    public string StudentName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string StudentEmail { get; set; } = string.Empty;
    
    [Phone]
    public string StudentPhone { get; set; } = string.Empty;
    
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.CreditCard;
}

public class PaymentViewModel
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string StudentPhone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethodType SelectedPaymentMethod { get; set; }
    public List<PaymentMethodInfo> AvailablePaymentMethods { get; set; } = new();
    public string QRTransactionId { get; set; } = string.Empty;
}

public class PaymentConfirmationViewModel
{
    public PaymentReceipt Receipt { get; set; } = null!;
    public Enrollment Enrollment { get; set; } = null!;
    public Course Course { get; set; } = null!;
}

public class CourseCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
    
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? PreviewVideoUrl { get; set; }
    
    [Required]
    public int CategoryId { get; set; }
    
    public bool IsFeatured { get; set; }
}

public class LessonCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public string? VideoUrl { get; set; }
    public string? MaterialUrl { get; set; }
    
    [StringLength(5000)]
    public string? Script { get; set; }
    
    public int OrderIndex { get; set; }
    public int Duration { get; set; } // Duration in minutes
    public int CourseId { get; set; }
}

public class LessonUpdateViewModel
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public string? VideoUrl { get; set; }
    public string? MaterialUrl { get; set; }
    
    [StringLength(5000)]
    public string? Script { get; set; }
    
    public int OrderIndex { get; set; }
    public int Duration { get; set; } // Duration in minutes
    public int CourseId { get; set; }
}

public class QuizCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public int TimeLimit { get; set; }
    public int PassingScore { get; set; }
    public int CourseId { get; set; }
    public List<QuestionCreateViewModel> Questions { get; set; } = new();
}

public class QuestionCreateViewModel
{
    [Required]
    [StringLength(500)]
    public string Text { get; set; } = string.Empty;
    
    public QuestionType Type { get; set; }
    public int Points { get; set; } = 1;
    public List<AnswerCreateViewModel> Answers { get; set; } = new();
}

public class AnswerCreateViewModel
{
    [Required]
    [StringLength(300)]
    public string Text { get; set; } = string.Empty;
    
    public bool IsCorrect { get; set; }
}

public class CourseDetailsViewModel
{
    public Course Course { get; set; } = null!;
    public bool IsEnrolled { get; set; }
    public bool CanEnroll { get; set; }
    public List<Lesson> Lessons { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public bool CanReview { get; set; }
    public int CompletedLessons { get; set; }
    public double ProgressPercentage { get; set; }
    public Lesson? CurrentLesson { get; set; }
    public List<Course> RelatedCourses { get; set; } = new();
    public Dictionary<int, bool> LessonCompletionStatus { get; set; } = new();
}

public class StudentDashboardViewModel
{
    public List<Enrollment> EnrolledCourses { get; set; } = new();
    public List<Course> RecommendedCourses { get; set; } = new();
    public int TotalCourses { get; set; }
    public int CompletedCourses { get; set; }
    public List<Certificate> Certificates { get; set; } = new();
    public List<Achievement> Achievements { get; set; } = new();
    public List<StudentActivityFeed> ActivityFeed { get; set; } = new();
}

public class InstructorDashboardViewModel
{
    public List<Course> MyCourses { get; set; } = new();
    public int TotalStudents { get; set; }
    public int TotalCourses { get; set; }
    public decimal TotalEarnings { get; set; }
    public List<Enrollment> RecentEnrollments { get; set; } = new();
    public QuizPerformanceViewModel QuizPerformance { get; set; } = new();
    public int CertificatesIssued { get; set; }
}

public class QuizPerformanceViewModel
{
    public int TotalAttempts { get; set; }
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalCourses { get; set; }
    public int PendingApprovals { get; set; }
    public int TotalEnrollments { get; set; }
    public List<Course> PendingCourses { get; set; } = new();
    public List<ApplicationUser> RecentUsers { get; set; } = new();
}

public class ReviewCreateViewModel
{
    [Range(1, 5)]
    public int Rating { get; set; }
    
    [StringLength(1000)]
    public string Comment { get; set; } = string.Empty;
    
    public int CourseId { get; set; }
}

public class UserManagementViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class VideoPlayerViewModel
{
    public Lesson CurrentLesson { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public List<Lesson> Playlist { get; set; } = new();
    public bool IsEnrolled { get; set; }
    public int CompletedLessons { get; set; }
    public double ProgressPercentage { get; set; }
    public Lesson? NextLesson { get; set; }
    public Lesson? PreviousLesson { get; set; }
    public bool IsCompleted { get; set; }
}

public class CourseBrowseViewModel
{
    public List<Course> Courses { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int? SelectedCategoryId { get; set; }
    public string? PriceFilter { get; set; }
    public string? SortBy { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCourses { get; set; }
}

public class CertificatePaymentViewModel
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public decimal CertificateFee { get; set; } = 500; // Default certificate fee
    public decimal ExamFee { get; set; } = 300; // Default exam fee
    public decimal TotalAmount { get; set; } = 800; // Make it settable
    public PaymentMethodType SelectedPaymentMethod { get; set; }
    public List<PaymentMethodInfo> AvailablePaymentMethods { get; set; } = new();
}

public class CertificatePaymentConfirmationViewModel
{
    public CertificatePaymentReceipt Receipt { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public bool ExamAccess { get; set; }
    public bool CertificateAccess { get; set; }
}

public class CourseContentViewModel
{
    public Course Course { get; set; } = null!;
    public List<Lesson> Lessons { get; set; } = new();
    public int CompletedLessons { get; set; }
    public double ProgressPercentage { get; set; }
    public Dictionary<int, bool> LessonCompletionStatus { get; set; } = new();
}

// Exam ViewModels
public class ExamCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public DateTime ExamDate { get; set; }
    
    [Required]
    public string ExamSession { get; set; } = string.Empty;
    
    public ExamPatternType PatternType { get; set; } = ExamPatternType.Mixed;
    
    public string InstructorInstructions { get; set; } = string.Empty;
    
    public int CourseId { get; set; }
}

public class ExamQuestionCreateViewModel
{
    [Required]
    public string Text { get; set; } = string.Empty;
    
    public ExamQuestionType Type { get; set; }
    public ExamPart Part { get; set; }
    public int Points { get; set; } = 1;
    public int ExamId { get; set; }
    
    public List<string> Options { get; set; } = new List<string>();
    public List<int> CorrectOptions { get; set; } = new List<int>();
}

public class ExamDashboardViewModel
{
    public List<ExamSchedule> ScheduledExams { get; set; } = new();
    public List<ExamAttempt> CompletedExams { get; set; } = new();
    public List<ExamAttempt> PendingResults { get; set; } = new();
    public List<ExamCertificate> ExamCertificates { get; set; } = new();
}

public class ExamAttemptViewModel
{
    public Exam Exam { get; set; } = null!;
    public ExamAttempt? CurrentAttempt { get; set; }
    public bool CanTakeExam { get; set; }
    public bool IsExamTimeActive { get; set; }
    public string TimeStatus { get; set; } = string.Empty;
    public bool HasPaidReExam { get; set; }
}

public class ReExamPaymentViewModel
{
    public int ExamAttemptId { get; set; }
    public Exam Exam { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public decimal ReExamFee { get; set; } = 200;
    public PaymentMethodType SelectedPaymentMethod { get; set; }
    public List<PaymentMethodInfo> AvailablePaymentMethods { get; set; } = new();
}

public class InternalAssessmentViewModel
{
    public ExamAttempt ExamAttempt { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public ApplicationUser Student { get; set; } = null!;
    public int InternalMarks { get; set; }
    public int MaxInternalMarks { get; set; } = 20;
}