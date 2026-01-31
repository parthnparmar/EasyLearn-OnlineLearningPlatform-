using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EasyLearn.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<LessonProgress> LessonProgresses { get; set; }
    public DbSet<QuizAttempt> QuizAttempts { get; set; }
    public DbSet<StudentAnswer> StudentAnswers { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Certificate> Certificates { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<LoginEntry> LoginEntries { get; set; }
    public DbSet<PaymentReceipt> PaymentReceipts { get; set; }
    public DbSet<RegistrationEntry> RegistrationEntries { get; set; }
    public DbSet<CertificatePayment> CertificatePayments { get; set; }
    public DbSet<CertificatePaymentReceipt> CertificatePaymentReceipts { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    
    // Exam System
    public DbSet<Exam> Exams { get; set; }
    public DbSet<ExamSchedule> ExamSchedules { get; set; }
    public DbSet<ExamAttempt> ExamAttempts { get; set; }
    public DbSet<ExamQuestion> ExamQuestions { get; set; }
    public DbSet<ExamQuestionOption> ExamQuestionOptions { get; set; }
    public DbSet<ExamAnswer> ExamAnswers { get; set; }
    public DbSet<ExamCertificate> ExamCertificates { get; set; }
    public DbSet<ReExamPayment> ReExamPayments { get; set; }
    public DbSet<ExamVerification> ExamVerifications { get; set; }
    public DbSet<MissedExamRequest> MissedExamRequests { get; set; }
    
    // Achievement System
    public DbSet<Achievement> Achievements { get; set; }
    public DbSet<StudentActivityFeed> StudentActivityFeeds { get; set; }
    
    // Puzzle Games System
    public DbSet<PuzzleGame> PuzzleGames { get; set; }
    public DbSet<PuzzleAttempt> PuzzleAttempts { get; set; }
    public DbSet<PuzzleMove> PuzzleMoves { get; set; }
    public DbSet<PuzzleLeaderboard> PuzzleLeaderboards { get; set; }
    
    // Games System
    public DbSet<Game> Games { get; set; }
    public DbSet<GameScore> GameScores { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings => 
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure relationships
        builder.Entity<Course>()
            .HasOne(c => c.Instructor)
            .WithMany(u => u.CreatedCourses)
            .HasForeignKey(c => c.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Enrollment>()
            .HasOne(e => e.Student)
            .WithMany(u => u.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<QuizAttempt>()
            .HasOne(qa => qa.Student)
            .WithMany(u => u.QuizAttempts)
            .HasForeignKey(qa => qa.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentAnswer>()
            .HasOne(sa => sa.QuizAttempt)
            .WithMany(qa => qa.StudentAnswers)
            .HasForeignKey(sa => sa.QuizAttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StudentAnswer>()
            .HasOne(sa => sa.Question)
            .WithMany()
            .HasForeignKey(sa => sa.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure decimal precision
        builder.Entity<Course>()
            .Property(c => c.Price)
            .HasPrecision(18, 2);

        builder.Entity<PaymentReceipt>()
            .Property(pr => pr.Amount)
            .HasPrecision(18, 2);

        builder.Entity<CertificatePayment>()
            .Property(cp => cp.CertificateFee)
            .HasPrecision(18, 2);
            
        builder.Entity<CertificatePayment>()
            .Property(cp => cp.ExamFee)
            .HasPrecision(18, 2);
            
        builder.Entity<CertificatePayment>()
            .Property(cp => cp.TotalAmount)
            .HasPrecision(18, 2);
            
        builder.Entity<CertificatePaymentReceipt>()
            .Property(cpr => cpr.CertificateFee)
            .HasPrecision(18, 2);
            
        builder.Entity<CertificatePaymentReceipt>()
            .Property(cpr => cpr.ExamFee)
            .HasPrecision(18, 2);
            
        builder.Entity<CertificatePaymentReceipt>()
            .Property(cpr => cpr.TotalAmount)
            .HasPrecision(18, 2);

        builder.Entity<PaymentTransaction>()
            .Property(pt => pt.Amount)
            .HasPrecision(18, 2);

        // Configure Review relationships
        builder.Entity<Review>()
            .HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Review>()
            .HasOne(r => r.Course)
            .WithMany(c => c.Reviews)
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Certificate relationships
        builder.Entity<Certificate>()
            .HasOne(c => c.Student)
            .WithMany()
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Certificate>()
            .HasOne(c => c.Course)
            .WithMany()
            .HasForeignKey(c => c.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Announcement relationships
        builder.Entity<Announcement>()
            .HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure UserProfile relationships
        builder.Entity<UserProfile>()
            .HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure LoginEntry relationships
        builder.Entity<LoginEntry>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure RegistrationEntry relationships
        builder.Entity<RegistrationEntry>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure PaymentReceipt relationships
        builder.Entity<PaymentReceipt>()
            .HasOne(pr => pr.Student)
            .WithMany()
            .HasForeignKey(pr => pr.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentReceipt>()
            .HasOne(pr => pr.Course)
            .WithMany()
            .HasForeignKey(pr => pr.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Enrollment-PaymentReceipt relationship
        builder.Entity<Enrollment>()
            .HasOne(e => e.PaymentReceipt)
            .WithOne()
            .HasForeignKey<Enrollment>(e => e.PaymentReceiptId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure CertificatePayment relationships
        builder.Entity<CertificatePayment>()
            .HasOne(cp => cp.Student)
            .WithMany()
            .HasForeignKey(cp => cp.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CertificatePayment>()
            .HasOne(cp => cp.Course)
            .WithMany()
            .HasForeignKey(cp => cp.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure CertificatePaymentReceipt relationships
        builder.Entity<CertificatePaymentReceipt>()
            .HasOne(cpr => cpr.Student)
            .WithMany()
            .HasForeignKey(cpr => cpr.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<CertificatePaymentReceipt>()
            .HasOne(cpr => cpr.Course)
            .WithMany()
            .HasForeignKey(cpr => cpr.CourseId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<CertificatePaymentReceipt>()
            .HasOne(cpr => cpr.CertificatePayment)
            .WithOne()
            .HasForeignKey<CertificatePaymentReceipt>(cpr => cpr.CertificatePaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure PaymentTransaction relationships
        builder.Entity<PaymentTransaction>()
            .HasOne(pt => pt.Student)
            .WithMany()
            .HasForeignKey(pt => pt.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentTransaction>()
            .HasOne(pt => pt.Course)
            .WithMany()
            .HasForeignKey(pt => pt.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Exam relationships
        builder.Entity<Exam>()
            .HasOne(e => e.Course)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Exam>()
            .HasOne(e => e.Instructor)
            .WithMany()
            .HasForeignKey(e => e.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamSchedule>()
            .HasOne(es => es.Exam)
            .WithMany(e => e.ExamSchedules)
            .HasForeignKey(es => es.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamSchedule>()
            .HasOne(es => es.Student)
            .WithMany()
            .HasForeignKey(es => es.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamAttempt>()
            .HasOne(ea => ea.Exam)
            .WithMany(e => e.ExamAttempts)
            .HasForeignKey(ea => ea.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamAttempt>()
            .HasOne(ea => ea.Student)
            .WithMany()
            .HasForeignKey(ea => ea.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamAttempt>()
            .HasOne(ea => ea.ExamSchedule)
            .WithMany()
            .HasForeignKey(ea => ea.ExamScheduleId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<ExamQuestion>()
            .HasOne(eq => eq.Exam)
            .WithMany(e => e.ExamQuestions)
            .HasForeignKey(eq => eq.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamQuestionOption>()
            .HasOne(eqo => eqo.ExamQuestion)
            .WithMany(eq => eq.Options)
            .HasForeignKey(eqo => eqo.ExamQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamAnswer>()
            .HasOne(ea => ea.ExamAttempt)
            .WithMany(eat => eat.ExamAnswers)
            .HasForeignKey(ea => ea.ExamAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamAnswer>()
            .HasOne(ea => ea.ExamQuestion)
            .WithMany()
            .HasForeignKey(ea => ea.ExamQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamAnswer>()
            .HasOne(ea => ea.SelectedOption)
            .WithMany()
            .HasForeignKey(ea => ea.SelectedOptionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ExamCertificate>()
            .HasOne(ec => ec.ExamAttempt)
            .WithOne(ea => ea.ExamCertificate)
            .HasForeignKey<ExamCertificate>(ec => ec.ExamAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamCertificate>()
            .HasOne(ec => ec.Student)
            .WithMany()
            .HasForeignKey(ec => ec.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<ExamCertificate>()
            .HasOne(ec => ec.Course)
            .WithMany()
            .HasForeignKey(ec => ec.CourseId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure ReExamPayment relationships
        builder.Entity<ReExamPayment>()
            .HasOne(rep => rep.Student)
            .WithMany()
            .HasForeignKey(rep => rep.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReExamPayment>()
            .HasOne(rep => rep.ExamAttempt)
            .WithMany()
            .HasForeignKey(rep => rep.ExamAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReExamPayment>()
            .HasOne(rep => rep.Exam)
            .WithMany()
            .HasForeignKey(rep => rep.ExamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<ReExamPayment>()
            .HasOne(rep => rep.Course)
            .WithMany()
            .HasForeignKey(rep => rep.CourseId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<ReExamPayment>()
            .Property(rep => rep.ReExamFee)
            .HasPrecision(18, 2);

        // Configure ExamVerification relationships
        builder.Entity<ExamVerification>()
            .HasOne(ev => ev.Student)
            .WithMany()
            .HasForeignKey(ev => ev.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamVerification>()
            .HasOne(ev => ev.Exam)
            .WithMany()
            .HasForeignKey(ev => ev.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure MissedExamRequest relationships
        builder.Entity<MissedExamRequest>()
            .HasOne(mer => mer.Student)
            .WithMany()
            .HasForeignKey(mer => mer.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<MissedExamRequest>()
            .HasOne(mer => mer.Exam)
            .WithMany()
            .HasForeignKey(mer => mer.ExamId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure Achievement relationships
        builder.Entity<Achievement>()
            .HasOne(a => a.Student)
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Achievement>()
            .HasOne(a => a.Course)
            .WithMany()
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Achievement>()
            .HasOne(a => a.ExamAttempt)
            .WithMany()
            .HasForeignKey(a => a.ExamAttemptId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure StudentActivityFeed relationships
        builder.Entity<StudentActivityFeed>()
            .HasOne(saf => saf.Student)
            .WithMany()
            .HasForeignKey(saf => saf.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentActivityFeed>()
            .HasOne(saf => saf.Achievement)
            .WithMany()
            .HasForeignKey(saf => saf.AchievementId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure PuzzleGame relationships
        builder.Entity<PuzzleAttempt>()
            .HasOne(pa => pa.User)
            .WithMany()
            .HasForeignKey(pa => pa.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PuzzleAttempt>()
            .HasOne(pa => pa.PuzzleGame)
            .WithMany(pg => pg.Attempts)
            .HasForeignKey(pa => pa.PuzzleGameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PuzzleMove>()
            .HasOne(pm => pm.PuzzleAttempt)
            .WithMany(pa => pa.Moves)
            .HasForeignKey(pm => pm.PuzzleAttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PuzzleLeaderboard>()
            .HasOne(pl => pl.User)
            .WithMany()
            .HasForeignKey(pl => pl.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PuzzleLeaderboard>()
            .HasOne(pl => pl.PuzzleGame)
            .WithMany()
            .HasForeignKey(pl => pl.PuzzleGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Games relationships
        builder.Entity<GameScore>()
            .HasOne(gs => gs.Game)
            .WithMany(g => g.Scores)
            .HasForeignKey(gs => gs.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<GameScore>()
            .HasOne(gs => gs.User)
            .WithMany()
            .HasForeignKey(gs => gs.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Seed data
        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Programming", Description = "Software development courses" },
            new Category { Id = 2, Name = "Design", Description = "UI/UX and graphic design courses" },
            new Category { Id = 3, Name = "Business", Description = "Business and management courses" },
            new Category { Id = 4, Name = "Marketing", Description = "Digital marketing courses" }
        );
    }
}