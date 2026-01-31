using EasyLearn.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Services;

public interface IExamService
{
    Task<bool> ScheduleExamForStudentAsync(string studentId, int courseId);
    Task<ExamAttempt?> StartExamAsync(string studentId, int examId);
    Task<bool> SubmitPartAAsync(int examAttemptId, List<ExamAnswerSubmission> answers);
    Task<bool> SubmitPartBAsync(int examAttemptId, List<ExamAnswerSubmission> answers);
    Task<bool> AssignInternalMarksAsync(int examAttemptId, int internalMarks, string instructorId);
    Task<bool> PublishResultsAsync(int examAttemptId);
    Task<ExamCertificate?> GenerateExamCertificateAsync(int examAttemptId);
    Task<List<ExamAttempt>> GetPendingInternalAssessmentsAsync(string instructorId);
    Task<bool> IsExamTimeActiveAsync(int examId);
    Task<string> GetExamTimeStatusAsync(int examId, string? studentId = null);
    Task<bool> AssignExamDateToStudentAsync(string instructorId, string studentId, int examId, DateTime examDate, string session);
    Task<List<ApplicationUser>> GetUnassignedStudentsForExamAsync(string instructorId, int examId);
    Task<bool> HasPaidForReExamAsync(string studentId, int examAttemptId);
    Task<ExamAttempt?> CreateReExamAttemptAsync(string studentId, int originalExamAttemptId);
    Task<bool> IsExamAvailableForStudentAsync(string studentId, int examId);
    Task<List<ExamSchedule>> GetActiveExamSchedulesForStudentAsync(string studentId);
    Task<bool> ProcessReExamPaymentAsync(string studentId, int examAttemptId, PaymentMethodType paymentMethod);
    Task<bool> SubmitMissedExamRequestAsync(string studentId, int examId, string reason);
    Task<List<MissedExamRequest>> GetPendingMissedExamRequestsAsync(string instructorId);
    Task<bool> ApproveMissedExamRequestAsync(int requestId, string instructorId, DateTime newExamStartTime, DateTime newExamEndTime);
    Task<bool> RejectMissedExamRequestAsync(int requestId, string instructorId, string response);
}

public class ExamService : IExamService
{
    private readonly ApplicationDbContext _context;
    private readonly ICertificateService _certificateService;
    private readonly IAchievementService _achievementService;

    public ExamService(ApplicationDbContext context, ICertificateService certificateService, IAchievementService achievementService)
    {
        _context = context;
        _certificateService = certificateService;
        _achievementService = achievementService;
    }

    public async Task<bool> ScheduleExamForStudentAsync(string studentId, int courseId)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId && e.IsCompleted);

        if (enrollment == null) return false;

        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.IsApproved && e.IsActive);

        if (exam == null) return false;

        var existingSchedule = await _context.ExamSchedules
            .FirstOrDefaultAsync(es => es.StudentId == studentId && es.ExamId == exam.Id);

        if (existingSchedule != null) return false;

        var schedule = new ExamSchedule
        {
            ExamId = exam.Id,
            StudentId = studentId,
            ScheduledDate = exam.ScheduledStartTime,
            Session = "Auto",
            IsAssigned = true
        };

        _context.ExamSchedules.Add(schedule);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ExamAttempt?> StartExamAsync(string studentId, int examId)
    {
        var exam = await _context.Exams.FindAsync(examId);
        if (exam == null) return null;
        
        // Check if student has an assigned exam schedule FIRST
        var schedule = await _context.ExamSchedules
            .FirstOrDefaultAsync(es => es.StudentId == studentId && es.ExamId == examId && es.IsAssigned);
        
        if (schedule == null)
        {
            return null; // New users must have instructor-assigned exam dates
        }
        
        // Check if exam is available for this student (includes time validation)
        var isAvailable = await IsExamAvailableForStudentAsync(studentId, examId);
        if (!isAvailable)
        {
            return null; // Exam is not available at this time
        }
        
        // Check if there's already an existing attempt
        var existingAttempt = await _context.ExamAttempts
            .FirstOrDefaultAsync(ea => ea.StudentId == studentId && ea.ExamId == examId && !ea.IsCompleted);

        if (existingAttempt != null) return existingAttempt;

        var attempt = new ExamAttempt
        {
            ExamId = examId,
            StudentId = studentId,
            ExamScheduleId = schedule.Id,
            StartedAt = DateTime.UtcNow
        };

        _context.ExamAttempts.Add(attempt);
        await _context.SaveChangesAsync();
        
        // Create randomized question order for this student
        await CreateRandomizedQuestionOrderAsync(attempt.Id, examId, studentId);
        
        return attempt;
    }
    
    private async Task CreateRandomizedQuestionOrderAsync(int attemptId, int examId, string studentId)
    {
        // Get all questions for this exam
        var questions = await _context.ExamQuestions
            .Where(eq => eq.ExamId == examId)
            .ToListAsync();
        
        // Don't create placeholder answers - let them be created during submission
        // This prevents duplicate key issues
    }

    public async Task<bool> SubmitPartAAsync(int examAttemptId, List<ExamAnswerSubmission> answers)
    {
        try
        {
            var attempt = await _context.ExamAttempts
                .Include(ea => ea.Exam)
                .FirstOrDefaultAsync(ea => ea.Id == examAttemptId);

            if (attempt == null || attempt.PartACompleted) return false;

            var partAQuestions = await _context.ExamQuestions
                .Include(eq => eq.Options)
                .Where(eq => eq.ExamId == attempt.ExamId && eq.Part == ExamPart.PartA)
                .ToListAsync();

            int partAScore = 0;
            
            // Clear existing answers for this attempt and part
            var existingAnswers = await _context.ExamAnswers
                .Where(ea => ea.ExamAttemptId == examAttemptId && 
                           partAQuestions.Select(q => q.Id).Contains(ea.ExamQuestionId))
                .ToListAsync();
            _context.ExamAnswers.RemoveRange(existingAnswers);
            
            // Process all Part A questions for MCQ marking scheme
            foreach (var question in partAQuestions)
            {
                var answer = answers.FirstOrDefault(a => a.QuestionId == question.Id);
                int points = 0;
                bool isCorrect = false;
                int? selectedOptionId = null;
                
                if (answer?.SelectedOptionId != null)
                {
                    // Question was attempted
                    selectedOptionId = answer.SelectedOptionId;
                    var selectedOption = question.Options.FirstOrDefault(o => o.Id == answer.SelectedOptionId);
                    isCorrect = selectedOption?.IsCorrect ?? false;
                    
                    // MCQ Marking Scheme: +2 for correct, 0 for wrong/unattempted
                    points = isCorrect ? 2 : 0;
                }
                // If answer is null or SelectedOptionId is null, points remain 0 (unattempted)
                
                partAScore += points;

                // Create new ExamAnswer
                var examAnswer = new ExamAnswer
                {
                    ExamAttemptId = examAttemptId,
                    ExamQuestionId = question.Id,
                    SelectedOptionId = selectedOptionId,
                    AnswerText = selectedOptionId?.ToString() ?? "",
                    IsCorrect = isCorrect,
                    Points = points,
                    AnsweredAt = DateTime.UtcNow
                };
                _context.ExamAnswers.Add(examAnswer);
            }

            // Ensure minimum score is 0 (no negative total scores)
            partAScore = Math.Max(0, partAScore);
            
            attempt.PartAScore = partAScore;
            attempt.PartACompleted = true;
            
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Log the exception if you have logging configured
            return false;
        }
    }

    public async Task<bool> SubmitPartBAsync(int examAttemptId, List<ExamAnswerSubmission> answers)
    {
        var attempt = await _context.ExamAttempts
            .FirstOrDefaultAsync(ea => ea.Id == examAttemptId);

        if (attempt == null || attempt.PartBCompleted) return false;

        foreach (var answer in answers)
        {
            // Update existing ExamAnswer or create new one
            var existingAnswer = await _context.ExamAnswers
                .FirstOrDefaultAsync(ea => ea.ExamAttemptId == examAttemptId && ea.ExamQuestionId == answer.QuestionId);
            
            if (existingAnswer != null)
            {
                existingAnswer.AnswerText = answer.AnswerText ?? string.Empty;
                existingAnswer.Points = 0; // Will be graded by instructor
                existingAnswer.AnsweredAt = DateTime.UtcNow;
            }
            else
            {
                var examAnswer = new ExamAnswer
                {
                    ExamAttemptId = examAttemptId,
                    ExamQuestionId = answer.QuestionId,
                    AnswerText = answer.AnswerText ?? string.Empty,
                    Points = 0, // Will be graded by instructor
                    AnsweredAt = DateTime.UtcNow
                };
                _context.ExamAnswers.Add(examAnswer);
            }
        }

        attempt.PartBCompleted = true;
        attempt.CompletedAt = DateTime.UtcNow;
        attempt.IsCompleted = true;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignInternalMarksAsync(int examAttemptId, int internalMarks, string instructorId)
    {
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .FirstOrDefaultAsync(ea => ea.Id == examAttemptId);

        if (attempt == null || attempt.InternalAssigned) return false;

        // Verify instructor owns the course
        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == attempt.ExamId && e.InstructorId == instructorId);

        if (exam == null) return false;

        attempt.InternalScore = Math.Min(internalMarks, attempt.Exam.InternalMarks);
        attempt.InternalAssigned = true;
        
        // Calculate total score and percentage
        attempt.TotalScore = attempt.PartAScore + attempt.PartBScore + attempt.InternalScore;
        attempt.Percentage = (double)attempt.TotalScore / attempt.Exam.TotalMarks * 100;
        attempt.IsPassed = attempt.Percentage >= attempt.Exam.PassingPercentage;

        await _context.SaveChangesAsync();

        // Schedule result publication after 3 hours
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromHours(3));
            await PublishResultsAsync(examAttemptId);
        });

        return true;
    }

    public async Task<bool> PublishResultsAsync(int examAttemptId)
    {
        var attempt = await _context.ExamAttempts
            .FirstOrDefaultAsync(ea => ea.Id == examAttemptId);

        if (attempt == null || attempt.ResultPublished) return false;

        attempt.ResultPublished = true;
        attempt.ResultPublishedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Check for exam score achievements
        await _achievementService.CheckAndAwardAchievementsAsync(attempt.StudentId, null, examAttemptId);

        // Generate certificate if passed
        if (attempt.IsPassed)
        {
            await GenerateExamCertificateAsync(examAttemptId);
        }

        return true;
    }

    public async Task<ExamCertificate?> GenerateExamCertificateAsync(int examAttemptId)
    {
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Student)
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .ThenInclude(c => c.Instructor)
            .FirstOrDefaultAsync(ea => ea.Id == examAttemptId);

        if (attempt == null || !attempt.IsPassed || !attempt.ResultPublished) return null;

        var existingCertificate = await _context.ExamCertificates
            .FirstOrDefaultAsync(ec => ec.ExamAttemptId == examAttemptId);

        if (existingCertificate != null) return existingCertificate;

        var certificateNumber = $"EXAM-{DateTime.UtcNow:yyyyMMdd}-{attempt.Student.Id[..8].ToUpper()}-{attempt.Exam.CourseId:D4}";
        // Debug: Log the student name components
        Console.WriteLine($"Debug - FirstName: '{attempt.Student.FirstName}'");
        Console.WriteLine($"Debug - LastName: '{attempt.Student.LastName}'");
        Console.WriteLine($"Debug - UserName: '{attempt.Student.UserName}'");
        
        // Handle case where FirstName might already contain the full name
        var firstName = attempt.Student.FirstName?.Trim() ?? "";
        var lastName = attempt.Student.LastName?.Trim() ?? "";
        
        string studentName;
        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
        {
            // Check if FirstName already contains the full name (contains space)
            if (firstName.Contains(' ') && string.IsNullOrWhiteSpace(lastName))
            {
                studentName = firstName; // FirstName already contains full name
            }
            else if (lastName.Contains(' ') && string.IsNullOrWhiteSpace(firstName))
            {
                studentName = lastName; // LastName contains full name
            }
            else
            {
                studentName = $"{firstName} {lastName}"; // Normal case
            }
        }
        else if (!string.IsNullOrWhiteSpace(firstName))
        {
            studentName = firstName;
        }
        else if (!string.IsNullOrWhiteSpace(lastName))
        {
            studentName = lastName;
        }
        else
        {
            studentName = attempt.Student.UserName ?? "Student";
        }
        
        Console.WriteLine($"Debug - Final studentName: '{studentName}'");
        var instructorName = !string.IsNullOrWhiteSpace(attempt.Exam.Course.Instructor?.FirstName) && !string.IsNullOrWhiteSpace(attempt.Exam.Course.Instructor?.LastName)
            ? $"{attempt.Exam.Course.Instructor.FirstName} {attempt.Exam.Course.Instructor.LastName}".Trim()
            : attempt.Exam.Course.Instructor?.UserName ?? "Instructor";

        var certificate = new ExamCertificate
        {
            ExamAttemptId = examAttemptId,
            StudentId = attempt.StudentId,
            CourseId = attempt.Exam.CourseId,
            CertificateNumber = certificateNumber,
            Percentage = attempt.Percentage,
            IssuedAt = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddYears(1)
        };

        // Generate PDF certificate
        var filePath = await _certificateService.GenerateExamCertificateAsync(
            studentName,
            attempt.Exam.Course.Title,
            attempt.Percentage,
            certificateNumber,
            instructorName
        );

        certificate.FilePath = filePath;

        _context.ExamCertificates.Add(certificate);
        await _context.SaveChangesAsync();

        return certificate;
    }

    public async Task<List<ExamAttempt>> GetPendingInternalAssessmentsAsync(string instructorId)
    {
        return await _context.ExamAttempts
            .Include(ea => ea.Student)
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .Include(ea => ea.ExamAnswers.Where(ans => ans.ExamAttemptId == ea.Id))
            .ThenInclude(ea => ea.ExamQuestion)
            .Where(ea => ea.Exam.InstructorId == instructorId && 
                        ea.IsCompleted && 
                        ea.PartBCompleted &&
                        !ea.ResultPublished)
            .OrderBy(ea => ea.CompletedAt)
            .ToListAsync();
    }

    public async Task<bool> IsExamTimeActiveAsync(int examId)
    {
        var exam = await _context.Exams.FindAsync(examId);
        return exam?.IsExamTimeActive() ?? false;
    }

    public async Task<string> GetExamTimeStatusAsync(int examId, string? studentId = null)
    {
        var exam = await _context.Exams.FindAsync(examId);
        if (exam == null) return "Exam not found";

        // If studentId is provided, check their specific schedule
        if (!string.IsNullOrEmpty(studentId))
        {
            var schedule = await _context.ExamSchedules
                .FirstOrDefaultAsync(es => es.StudentId == studentId && es.ExamId == examId && es.IsAssigned);
            
            if (schedule != null)
            {
                var now = DateTime.Now;
                var examStart = GetExamStartTime(schedule.ScheduledDate, schedule.Session);
                var examEnd = examStart.AddMinutes(exam.DurationMinutes);
                
                if (now < examStart)
                {
                    var timeUntilStart = examStart - now;
                    return $"Exam starts in {timeUntilStart.Days}d {timeUntilStart.Hours}h {timeUntilStart.Minutes}m";
                }
                else if (now >= examStart && now <= examEnd)
                {
                    var timeRemaining = examEnd - now;
                    return $"Exam in progress - {timeRemaining.Hours}h {timeRemaining.Minutes}m remaining";
                }
                else
                {
                    return "Exam time has ended.";
                }
            }
            else
            {
                return "Exam not assigned to you. Contact your instructor.";
            }
        }
        
        // Fallback to general exam time status
        var currentTime = DateTime.Now;
        
        if (currentTime < exam.ScheduledStartTime)
        {
            var timeUntilStart = exam.GetTimeUntilStart();
            return $"Exam starts in {timeUntilStart.Days}d {timeUntilStart.Hours}h {timeUntilStart.Minutes}m";
        }
        else if (exam.IsExamTimeActive())
        {
            var timeRemaining = exam.GetTimeRemaining();
            return $"Exam in progress - {timeRemaining.Hours}h {timeRemaining.Minutes}m remaining";
        }
        else
        {
            return "Exam time has ended.";
        }
    }

    public async Task<bool> AssignExamDateToStudentAsync(string instructorId, string studentId, int examId, DateTime examDate, string session)
    {
        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == examId && e.InstructorId == instructorId);
        
        if (exam == null) return false;
        
        // Check if student is enrolled in the course
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == exam.CourseId);
        
        if (enrollment == null) return false;
        
        // Check if schedule already exists
        var existingSchedule = await _context.ExamSchedules
            .FirstOrDefaultAsync(es => es.StudentId == studentId && es.ExamId == examId);
        
        if (existingSchedule != null)
        {
            // Update existing schedule
            existingSchedule.ScheduledDate = examDate;
            existingSchedule.Session = session;
            existingSchedule.IsAssigned = true;
        }
        else
        {
            // Create new schedule
            var schedule = new ExamSchedule
            {
                ExamId = examId,
                StudentId = studentId,
                ScheduledDate = examDate,
                Session = session,
                IsAssigned = true
            };
            _context.ExamSchedules.Add(schedule);
        }
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<List<ApplicationUser>> GetUnassignedStudentsForExamAsync(string instructorId, int examId)
    {
        var exam = await _context.Exams
            .FirstOrDefaultAsync(e => e.Id == examId && e.InstructorId == instructorId);
        
        if (exam == null) return new List<ApplicationUser>();
        
        // Get enrolled students who don't have exam schedules
        var enrolledStudentIds = await _context.Enrollments
            .Where(e => e.CourseId == exam.CourseId)
            .Select(e => e.StudentId)
            .ToListAsync();
        
        var assignedStudentIds = await _context.ExamSchedules
            .Where(es => es.ExamId == examId && es.IsAssigned)
            .Select(es => es.StudentId)
            .ToListAsync();
        
        var unassignedStudentIds = enrolledStudentIds.Except(assignedStudentIds).ToList();
        
        return await _context.Users
            .Where(u => unassignedStudentIds.Contains(u.Id))
            .ToListAsync();
    }

    public async Task<bool> ProcessReExamPaymentAsync(string studentId, int examAttemptId, PaymentMethodType paymentMethod)
    {
        var examAttempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(ea => ea.Id == examAttemptId && ea.StudentId == studentId);

        if (examAttempt == null || examAttempt.IsPassed) return false;

        var existingPayment = await _context.ReExamPayments
            .FirstOrDefaultAsync(rep => rep.ExamAttemptId == examAttemptId && rep.StudentId == studentId);

        if (existingPayment != null) return false;

        var student = await _context.Users.FindAsync(studentId);
        if (student == null) return false;

        var reExamPayment = new ReExamPayment
        {
            StudentId = studentId,
            ExamAttemptId = examAttemptId,
            ExamId = examAttempt.ExamId,
            CourseId = examAttempt.Exam.CourseId,
            StudentName = $"{student.FirstName} {student.LastName}".Trim(),
            StudentEmail = student.Email ?? "",
            PaymentMethod = paymentMethod,
            TransactionId = $"REEXAM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            PaymentStatus = "Completed"
        };

        _context.ReExamPayments.Add(reExamPayment);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasPaidForReExamAsync(string studentId, int examAttemptId)
    {
        return await _context.ReExamPayments
            .AnyAsync(rep => rep.StudentId == studentId && 
                           rep.ExamAttemptId == examAttemptId && 
                           rep.PaymentStatus == "Completed");
    }

    public async Task<ExamAttempt?> CreateReExamAttemptAsync(string studentId, int originalExamAttemptId)
    {
        var originalAttempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .Include(ea => ea.ExamSchedule)
            .FirstOrDefaultAsync(ea => ea.Id == originalExamAttemptId && ea.StudentId == studentId);

        if (originalAttempt == null || originalAttempt.IsPassed) return null;

        var hasPaid = await HasPaidForReExamAsync(studentId, originalExamAttemptId);
        if (!hasPaid) return null;
        
        // For re-exams, allow access regardless of time restrictions since payment has been made
        // This ensures students can access their paid re-exam
        
        var newAttempt = new ExamAttempt
        {
            ExamId = originalAttempt.ExamId,
            StudentId = studentId,
            StartedAt = DateTime.UtcNow,
            ExamScheduleId = originalAttempt.ExamScheduleId
        };

        _context.ExamAttempts.Add(newAttempt);
        await _context.SaveChangesAsync();
        return newAttempt;
    }
    
    private DateTime GetExamStartTime(DateTime examDate, string session)
    {
        return session.ToLower() switch
        {
            "morning" => examDate.Date.AddHours(9), // 9:00 AM
            "afternoon" => examDate.Date.AddHours(14), // 2:00 PM
            "evening" => examDate.Date.AddHours(18), // 6:00 PM
            _ => examDate
        };
    }
    
    public async Task<bool> IsExamAvailableForStudentAsync(string studentId, int examId)
    {
        // Check if this is a re-exam scenario first
        var completedAttempt = await _context.ExamAttempts
            .FirstOrDefaultAsync(ea => ea.ExamId == examId && ea.StudentId == studentId && ea.IsCompleted && !ea.IsPassed);
        
        if (completedAttempt != null)
        {
            var hasPaidReExam = await HasPaidForReExamAsync(studentId, completedAttempt.Id);
            if (hasPaidReExam)
            {
                return true; // Re-exam is always available if paid
            }
        }
        
        // Check if student has an assigned exam schedule
        var schedule = await _context.ExamSchedules
            .FirstOrDefaultAsync(es => es.StudentId == studentId && es.ExamId == examId && es.IsAssigned);
        
        if (schedule == null)
        {
            return false; // No assigned schedule
        }
        
        var exam = await _context.Exams.FindAsync(examId);
        if (exam == null) return false;
        
        // For scheduled exams, check if current time is within the exam window
        var now = DateTime.Now;
        var examStart = GetExamStartTime(schedule.ScheduledDate, schedule.Session);
        var examEnd = examStart.AddMinutes(exam.DurationMinutes);
        
        return now >= examStart && now <= examEnd;
    }
    
    public async Task<List<ExamSchedule>> GetActiveExamSchedulesForStudentAsync(string studentId)
    {
        var schedules = await _context.ExamSchedules
            .Include(es => es.Exam)
            .ThenInclude(e => e.Course)
            .ThenInclude(c => c.Category)
            .Include(es => es.Exam)
            .ThenInclude(e => e.Instructor)
            .Include(es => es.Exam)
            .ThenInclude(e => e.ExamAttempts.Where(ea => ea.StudentId == studentId))
            .Where(es => es.StudentId == studentId && es.IsAssigned && 
                        es.Exam.IsApproved && es.Exam.IsActive)
            .ToListAsync();
        
        // Return all schedules - don't filter by time so students can see expired exams
        return schedules;
    }
    
    public async Task<bool> SubmitMissedExamRequestAsync(string studentId, int examId, string reason)
    {
        var exam = await _context.Exams.FindAsync(examId);
        if (exam == null || !exam.IsExamMissed()) return false;
        
        var existingRequest = await _context.MissedExamRequests
            .FirstOrDefaultAsync(mer => mer.StudentId == studentId && mer.ExamId == examId);
        
        if (existingRequest != null) return false;
        
        var request = new MissedExamRequest
        {
            StudentId = studentId,
            ExamId = examId,
            Reason = reason,
            RequestedAt = DateTime.UtcNow
        };
        
        _context.MissedExamRequests.Add(request);
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<List<MissedExamRequest>> GetPendingMissedExamRequestsAsync(string instructorId)
    {
        return await _context.MissedExamRequests
            .Include(mer => mer.Student)
            .Include(mer => mer.Exam)
            .ThenInclude(e => e.Course)
            .Where(mer => mer.Exam.InstructorId == instructorId && !mer.IsApproved && !mer.IsRejected)
            .OrderBy(mer => mer.RequestedAt)
            .ToListAsync();
    }
    
    public async Task<bool> ApproveMissedExamRequestAsync(int requestId, string instructorId, DateTime newExamStartTime, DateTime newExamEndTime)
    {
        var request = await _context.MissedExamRequests
            .Include(mer => mer.Exam)
            .FirstOrDefaultAsync(mer => mer.Id == requestId && mer.Exam.InstructorId == instructorId);
        
        if (request == null || request.IsApproved || request.IsRejected) return false;
        
        // Update the exam schedule for this student
        request.Exam.ScheduledStartTime = newExamStartTime;
        request.Exam.ScheduledEndTime = newExamEndTime;
        
        request.IsApproved = true;
        request.InstructorId = instructorId;
        request.InstructorResponse = "Approved - New exam time assigned";
        request.ResponseAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> RejectMissedExamRequestAsync(int requestId, string instructorId, string response)
    {
        var request = await _context.MissedExamRequests
            .Include(mer => mer.Exam)
            .FirstOrDefaultAsync(mer => mer.Id == requestId && mer.Exam.InstructorId == instructorId);
        
        if (request == null || request.IsApproved || request.IsRejected) return false;
        
        request.IsRejected = true;
        request.InstructorId = instructorId;
        request.InstructorResponse = response;
        request.ResponseAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }
}

public class ExamAnswerSubmission
{
    public int QuestionId { get; set; }
    public int? SelectedOptionId { get; set; }
    public string? AnswerText { get; set; }
}