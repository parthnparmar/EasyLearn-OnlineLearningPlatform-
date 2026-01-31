using EasyLearn.Models;
using EasyLearn.Models.ViewModels;
using EasyLearn.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EasyLearn.Controllers;

[Authorize(Roles = "Student")]
[Route("student")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IProgressService _progressService;
    private readonly ICertificateService _certificateService;
    private readonly IProfileService _profileService;
    private readonly IPaymentService _paymentService;
    private readonly IReceiptService _receiptService;
    private readonly ICertificatePaymentService _certificatePaymentService;
    private readonly IExamService _examService;
    private readonly ICaptchaService _captchaService;
    private readonly IAchievementService _achievementService;

    public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
        IProgressService progressService, ICertificateService certificateService, IProfileService profileService,
        IPaymentService paymentService, IReceiptService receiptService, ICertificatePaymentService certificatePaymentService,
        IExamService examService, ICaptchaService captchaService, IAchievementService achievementService)
    {
        _context = context;
        _userManager = userManager;
        _progressService = progressService;
        _certificateService = certificateService;
        _profileService = profileService;
        _paymentService = paymentService;
        _receiptService = receiptService;
        _certificatePaymentService = certificatePaymentService;
        _examService = examService;
        _captchaService = captchaService;
        _achievementService = achievementService;
    }

    [Route("dashboard")]
    public async Task<IActionResult> Index()
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var profile = await _profileService.GetOrCreateProfileAsync(studentId);
        ViewBag.Profile = profile;
        
        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .ToListAsync();

        var certificates = await _context.Certificates
            .Include(c => c.Course)
            .Where(c => c.StudentId == studentId)
            .ToListAsync();

        var recommendedCourses = await _context.Courses
            .Include(c => c.Category)
            .Include(c => c.Instructor)
            .Include(c => c.Reviews)
            .Where(c => c.IsApproved && c.IsActive)
            .OrderByDescending(c => c.Reviews.Average(r => (double?)r.Rating) ?? 0)
            .Take(6)
            .ToListAsync();

        var achievements = await _achievementService.GetStudentAchievementsAsync(studentId);
        var activityFeed = await _achievementService.GetRecentActivityFeedAsync(10);

        var viewModel = new StudentDashboardViewModel
        {
            EnrolledCourses = enrollments,
            TotalCourses = enrollments.Count,
            CompletedCourses = enrollments.Count(e => e.CompletedAt != null),
            Certificates = certificates,
            RecommendedCourses = recommendedCourses,
            Achievements = achievements,
            ActivityFeed = activityFeed
        };

        return View(viewModel);
    }

    [Route("test-routing")]
    public IActionResult TestRouting()
    {
        ViewBag.Message = "Routing is working correctly!";
        ViewBag.UserId = _userManager.GetUserId(User);
        ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
        ViewBag.Roles = string.Join(", ", User.Claims.Where(c => c.Type == "role").Select(c => c.Value));
        return View();
    }

    [HttpGet]
    [Route("browse-courses")]
    public async Task<IActionResult> BrowseCourses(int? categoryId, string? search, string? priceFilter, string? sortBy, int page = 1)
    {
        try
        {
            const int pageSize = 12;
            
            var query = _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Reviews)
                .Where(c => c.IsApproved && c.IsActive);

            if (categoryId.HasValue)
                query = query.Where(c => c.CategoryId == categoryId);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));

            query = sortBy switch
            {
                "price_low" => query.OrderBy(c => c.Price),
                "price_high" => query.OrderByDescending(c => c.Price),
                "rating" => query.OrderByDescending(c => c.Reviews.Average(r => (double?)r.Rating) ?? 0),
                "newest" => query.OrderByDescending(c => c.CreatedAt),
                "popular" => query.OrderByDescending(c => c.TotalEnrollments),
                _ => query.OrderByDescending(c => c.CreatedAt)
            };

            var totalCourses = await query.CountAsync();
            var courses = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();

            var viewModel = new CourseBrowseViewModel
            {
                Courses = courses,
                Categories = categories,
                SearchTerm = search,
                SelectedCategoryId = categoryId,
                PriceFilter = priceFilter,
                SortBy = sortBy,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCourses / (double)pageSize),
                TotalCourses = totalCourses
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred while loading courses. Please try again.";
            return View(new CourseBrowseViewModel
            {
                Courses = new List<Course>(),
                Categories = new List<Category>(),
                CurrentPage = 1,
                TotalPages = 0,
                TotalCourses = 0
            });
        }
    }

    [Route("my-courses")]
    public async Task<IActionResult> MyCourses()
    {
        var studentId = _userManager.GetUserId(User);
        var enrollments = await _context.Enrollments
            .Include(e => e.Course)
            .ThenInclude(c => c.Category)
            .Include(e => e.Course)
            .ThenInclude(c => c.Instructor)
            .Include(e => e.Course)
            .ThenInclude(c => c.Lessons)
            .Where(e => e.StudentId == studentId)
            .ToListAsync();

        // Get lesson progress for all enrolled courses
        var courseIds = enrollments.Select(e => e.CourseId).ToList();
        var lessonProgress = await _context.LessonProgresses
            .Where(lp => lp.StudentId == studentId && 
                         _context.Lessons.Any(l => l.Id == lp.LessonId && courseIds.Contains(l.CourseId)))
            .ToListAsync();

        // Update progress for each enrollment
        foreach (var enrollment in enrollments)
        {
            var courseLessons = enrollment.Course.Lessons.Count;
            var completedLessons = lessonProgress.Count(lp => 
                lp.IsCompleted && 
                enrollment.Course.Lessons.Any(l => l.Id == lp.LessonId));
            
            enrollment.Progress = courseLessons > 0 ? (completedLessons * 100 / courseLessons) : 0;
        }

        return View(enrollments);
    }

    [HttpGet]
    [Route("enrolldirect")]
    public async Task<IActionResult> EnrollDirect(int courseid)
    {
        var course = await _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.Id == courseid && c.IsApproved && c.IsActive);

        if (course == null)
        {
            TempData["Error"] = "Course not found";
            return RedirectToAction("BrowseCourses");
        }

        var studentId = _userManager.GetUserId(User);
        var existingEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == courseid && e.StudentId == studentId);

        if (existingEnrollment != null)
        {
            TempData["Info"] = "You are already enrolled in this course";
            return RedirectToAction("CourseDetails", new { id = courseid });
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var viewModel = new PaymentViewModel
        {
            CourseId = courseid,
            Course = course,
            StudentName = $"{currentUser?.FirstName} {currentUser?.LastName}".Trim(),
            StudentEmail = currentUser?.Email ?? "",
            StudentPhone = currentUser?.PhoneNumber ?? "",
            Amount = 0, // Courses are free - only exams and certificates require payment
            AvailablePaymentMethods = await _paymentService.GetAvailablePaymentMethodsAsync()
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("enrolldirect")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollDirect(PaymentViewModel model)
    {
        var studentId = _userManager.GetUserId(User)!;
        var course = await _context.Courses.FindAsync(model.CourseId);

        if (course == null)
        {
            TempData["Error"] = "Course not found";
            return RedirectToAction("BrowseCourses");
        }

        var existingEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == model.CourseId && e.StudentId == studentId);

        if (existingEnrollment != null)
        {
            TempData["Info"] = "You are already enrolled in this course";
            return RedirectToAction("MyCourses");
        }

        // Courses are free to enroll - only exams and certificates require payment (₹800)
        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseId = model.CourseId,
            EnrolledAt = DateTime.UtcNow,
            Progress = 0,
            StudentName = model.StudentName,
            StudentEmail = model.StudentEmail,
            StudentPhone = model.StudentPhone
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Successfully enrolled in the course for free! Exams and certificates require payment of ₹800 (₹500 certificate + ₹300 exam fee).";
        return RedirectToAction("MyCourses");
    }

    [Route("course-details/{id:int}")]
    [Route("CourseDetails/{id:int}")]
    public async Task<IActionResult> CourseDetails(int id)
    {
        try
        {
            var course = await _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Lessons.OrderBy(l => l.OrderIndex))
                .Include(c => c.Reviews)
                .ThenInclude(r => r.Student)
                .Include(c => c.Quizzes)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsApproved && c.IsActive);

            if (course == null)
            {
                TempData["Error"] = "Course not found or not available.";
                return RedirectToAction("BrowseCourses");
            }

            var studentId = _userManager.GetUserId(User);
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == id && e.StudentId == studentId);

            var isEnrolled = enrollment != null;
            var completedLessons = 0;
            var lessonCompletionStatus = new Dictionary<int, bool>();
            
            if (isEnrolled && studentId != null)
            {
                var lessonProgress = await _context.LessonProgresses
                    .Where(lp => lp.StudentId == studentId && course.Lessons.Select(l => l.Id).Contains(lp.LessonId))
                    .ToListAsync();
                
                completedLessons = lessonProgress.Count(lp => lp.IsCompleted);
                lessonCompletionStatus = lessonProgress.ToDictionary(lp => lp.LessonId, lp => lp.IsCompleted);
            }

            var progressPercentage = course.Lessons.Any() ? (completedLessons * 100.0 / course.Lessons.Count) : 0;
            var currentLesson = course.Lessons.FirstOrDefault(l => !lessonCompletionStatus.GetValueOrDefault(l.Id, false));
            
            var relatedCourses = await _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Reviews)
                .Where(c => c.CategoryId == course.CategoryId && c.Id != id && c.IsApproved && c.IsActive)
                .Take(4)
                .ToListAsync();

            var viewModel = new CourseDetailsViewModel
            {
                Course = course,
                IsEnrolled = isEnrolled,
                CanEnroll = !isEnrolled,
                Lessons = course.Lessons.ToList(),
                Reviews = course.Reviews.ToList(),
                CanReview = isEnrolled && enrollment?.CompletedAt != null,
                CompletedLessons = completedLessons,
                ProgressPercentage = progressPercentage,
                CurrentLesson = currentLesson,
                RelatedCourses = relatedCourses,
                LessonCompletionStatus = lessonCompletionStatus
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred while loading the course details. Please try again.";
            return RedirectToAction("BrowseCourses");
        }
    }

    [Route("coursecontent")]
    public async Task<IActionResult> CourseContent(int courseId)
    {
        var studentId = _userManager.GetUserId(User);
        
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);

        if (enrollment == null)
        {
            TempData["Error"] = "You must be enrolled in this course to access the content";
            return RedirectToAction("CourseDetails", new { id = courseId });
        }

        var course = await _context.Courses
            .Include(c => c.Category)
            .Include(c => c.Instructor)
            .Include(c => c.Lessons.OrderBy(l => l.OrderIndex))
            .Include(c => c.Quizzes)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
        {
            TempData["Error"] = "Course not found";
            return RedirectToAction("BrowseCourses");
        }

        var lessonProgress = await _context.LessonProgresses
            .Where(lp => lp.StudentId == studentId && course.Lessons.Select(l => l.Id).Contains(lp.LessonId))
            .ToListAsync();

        var completedLessons = lessonProgress.Count(lp => lp.IsCompleted);
        var progressPercentage = course.Lessons.Any() ? (completedLessons * 100.0 / course.Lessons.Count) : 0;
        var lessonCompletionStatus = lessonProgress.ToDictionary(lp => lp.LessonId, lp => lp.IsCompleted);

        var viewModel = new CourseContentViewModel
        {
            Course = course,
            Lessons = course.Lessons.ToList(),
            CompletedLessons = completedLessons,
            ProgressPercentage = progressPercentage,
            LessonCompletionStatus = lessonCompletionStatus
        };

        return View(viewModel);
    }

    [Route("watchlesson")]
    public async Task<IActionResult> WatchLesson(int lessonId)
    {
        var studentId = _userManager.GetUserId(User);
        
        var lesson = await _context.Lessons
            .Include(l => l.Course)
            .ThenInclude(c => c.Instructor)
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson == null)
        {
            TempData["Error"] = "Lesson not found";
            return RedirectToAction("MyCourses");
        }

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == lesson.CourseId && e.StudentId == studentId);

        if (enrollment == null)
        {
            TempData["Error"] = "You must be enrolled in this course to watch lessons";
            return RedirectToAction("CourseDetails", new { id = lesson.CourseId });
        }

        var allLessons = await _context.Lessons
            .Where(l => l.CourseId == lesson.CourseId)
            .OrderBy(l => l.OrderIndex)
            .ToListAsync();

        var lessonProgress = await _context.LessonProgresses
            .Where(lp => lp.StudentId == studentId && allLessons.Select(l => l.Id).Contains(lp.LessonId))
            .ToListAsync();

        var completedLessons = lessonProgress.Count(lp => lp.IsCompleted);
        var progressPercentage = allLessons.Any() ? (completedLessons * 100.0 / allLessons.Count) : 0;
        var isCompleted = lessonProgress.Any(lp => lp.LessonId == lessonId && lp.IsCompleted);

        var currentIndex = allLessons.FindIndex(l => l.Id == lessonId);
        var nextLesson = currentIndex < allLessons.Count - 1 ? allLessons[currentIndex + 1] : null;
        var previousLesson = currentIndex > 0 ? allLessons[currentIndex - 1] : null;

        var viewModel = new VideoPlayerViewModel
        {
            CurrentLesson = lesson,
            Course = lesson.Course,
            Playlist = allLessons,
            IsEnrolled = true,
            CompletedLessons = completedLessons,
            ProgressPercentage = progressPercentage,
            NextLesson = nextLesson,
            PreviousLesson = previousLesson,
            IsCompleted = isCompleted
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("completelesson")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteLesson(int lessonId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var lesson = await _context.Lessons.FindAsync(lessonId);
        if (lesson == null)
        {
            return Json(new { success = false, message = "Lesson not found" });
        }

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == lesson.CourseId && e.StudentId == studentId);

        if (enrollment == null)
        {
            return Json(new { success = false, message = "Not enrolled in this course" });
        }

        var progress = await _context.LessonProgresses
            .FirstOrDefaultAsync(lp => lp.LessonId == lessonId && lp.StudentId == studentId);

        if (progress == null)
        {
            progress = new LessonProgress
            {
                LessonId = lessonId,
                StudentId = studentId,
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow
            };
            _context.LessonProgresses.Add(progress);
        }
        else if (!progress.IsCompleted)
        {
            progress.IsCompleted = true;
            progress.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var allLessons = await _context.Lessons
            .Where(l => l.CourseId == lesson.CourseId)
            .CountAsync();

        var completedCount = await _context.LessonProgresses
            .Where(lp => lp.StudentId == studentId && 
                         _context.Lessons.Any(l => l.Id == lp.LessonId && l.CourseId == lesson.CourseId) && 
                         lp.IsCompleted)
            .CountAsync();

        var progressPercentage = allLessons > 0 ? (completedCount * 100.0 / allLessons) : 0;

        enrollment.Progress = (int)progressPercentage;
        if (progressPercentage == 100 && enrollment.CompletedAt == null)
        {
            enrollment.CompletedAt = DateTime.UtcNow;
            
            // Check for course completion achievements
            await _achievementService.CheckAndAwardAchievementsAsync(studentId, lesson.CourseId);
        }

        await _context.SaveChangesAsync();

        return Json(new { success = true, progress = progressPercentage, completed = completedCount, total = allLessons });
    }

    [Route("takequiz")]
    public async Task<IActionResult> TakeQuiz(int quizId)
    {
        var studentId = _userManager.GetUserId(User);
        
        var quiz = await _context.Quizzes
            .Include(q => q.Course)
            .Include(q => q.Questions)
            .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.IsActive);

        if (quiz == null)
        {
            TempData["Error"] = "Quiz not found";
            return RedirectToAction("MyCourses");
        }

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.CourseId == quiz.CourseId && e.StudentId == studentId);

        if (enrollment == null)
        {
            TempData["Error"] = "You must be enrolled in this course to take the quiz";
            return RedirectToAction("CourseDetails", new { id = quiz.CourseId });
        }

        return View(quiz);
    }

    [HttpPost]
    [Route("submitquiz")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitQuiz(int quizId, IFormCollection form)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var quiz = await _context.Quizzes
            .Include(q => q.Questions)
            .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz == null)
        {
            TempData["Error"] = "Quiz not found";
            return RedirectToAction("MyCourses");
        }

        var quizAttempt = new QuizAttempt
        {
            QuizId = quizId,
            StudentId = studentId,
            AttemptedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Score = 0,
            TotalPoints = quiz.Questions.Sum(q => q.Points)
        };

        _context.QuizAttempts.Add(quizAttempt);
        await _context.SaveChangesAsync();

        int correctAnswers = 0;

        foreach (var question in quiz.Questions)
        {
            var selectedAnswerIds = form[$"question_{question.Id}"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var answerIdStr in selectedAnswerIds)
            {
                if (int.TryParse(answerIdStr, out int answerId))
                {
                    var studentAnswer = new StudentAnswer
                    {
                        QuizAttemptId = quizAttempt.Id,
                        QuestionId = question.Id,
                        SelectedAnswerId = answerId,
                        AnswerText = ""
                    };
                    _context.StudentAnswers.Add(studentAnswer);
                }
            }

            var correctAnswerIds = question.Answers.Where(a => a.IsCorrect).Select(a => a.Id).ToList();
            var selectedIds = selectedAnswerIds.Select(int.Parse).ToList();
            
            if (correctAnswerIds.OrderBy(x => x).SequenceEqual(selectedIds.OrderBy(x => x)))
            {
                correctAnswers++;
            }
        }

        quizAttempt.Percentage = quiz.Questions.Count > 0 ? (correctAnswers * 100.0 / quiz.Questions.Count) : 0;
        quizAttempt.IsPassed = quizAttempt.Score >= quiz.PassingScore;
        
        await _context.SaveChangesAsync();

        return RedirectToAction("QuizResult", new { attemptId = quizAttempt.Id });
    }

    [Route("quiz-result/{attemptId:int}")]
    public async Task<IActionResult> QuizResult(int attemptId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var attempt = await _context.QuizAttempts
            .Include(qa => qa.Quiz)
            .ThenInclude(q => q.Course)
            .FirstOrDefaultAsync(qa => qa.Id == attemptId && qa.StudentId == studentId);
        
        if (attempt == null)
        {
            TempData["Error"] = "Quiz attempt not found";
            return RedirectToAction("MyCourses");
        }
        
        return View(attempt);
    }

    [HttpGet]
    [Route("api/course-details/{courseId:int}")]
    public async Task<IActionResult> GetCourseDetailsApi(int courseId)
    {
        try
        {
            var studentId = _userManager.GetUserId(User)!;
            
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Lessons.OrderBy(l => l.OrderIndex))
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);

            if (enrollment == null)
            {
                return Json(new { success = false, message = "Course not found or not enrolled" });
            }

            var lessonProgress = await _context.LessonProgresses
                .Where(lp => lp.StudentId == studentId && 
                           enrollment.Course.Lessons.Select(l => l.Id).Contains(lp.LessonId))
                .ToListAsync();

            var completedLessons = lessonProgress.Count(lp => lp.IsCompleted);
            var totalLessons = enrollment.Course.Lessons.Count;
            var progressPercentage = totalLessons > 0 ? (completedLessons * 100.0 / totalLessons) : 0;
            
            var totalDurationTicks = enrollment.Course.Lessons.Sum(l => l.Duration.Ticks);
            var totalDuration = TimeSpan.FromTicks(totalDurationTicks);
            
            var quizCount = await _context.Quizzes.CountAsync(q => q.CourseId == courseId && q.IsActive);
            
            var recentLessons = enrollment.Course.Lessons
                .Take(5)
                .Select(l => new {
                    id = l.Id,
                    title = l.Title,
                    duration = l.Duration.ToString(@"mm\:ss"),
                    isCompleted = lessonProgress.Any(lp => lp.LessonId == l.Id && lp.IsCompleted),
                    videoUrl = l.VideoUrl
                })
                .ToList();

            var lastAccessed = lessonProgress.Any() ? 
                lessonProgress.Max(lp => lp.CompletedAt)?.ToString("d") ?? "Never" : "Never";

            var timeSpent = completedLessons > 0 ? 
                TimeSpan.FromTicks(enrollment.Course.Lessons
                    .Where(l => lessonProgress.Any(lp => lp.LessonId == l.Id && lp.IsCompleted))
                    .Sum(l => l.Duration.Ticks))
                    .ToString(@"h\.h") + " hours" : "0 hours";

            return Json(new {
                success = true,
                courseInfo = new {
                    duration = totalDuration.ToString(@"h\.h") + " hours",
                    lessons = totalLessons,
                    quizzes = quizCount,
                    certificate = "Yes"
                },
                progress = new {
                    percentage = Math.Round(progressPercentage, 1),
                    timeSpent = timeSpent,
                    lastAccessed = lastAccessed
                },
                recentLessons = recentLessons
            });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "Error loading course details" });
        }
    }

    [Route("exam-debug")]
    public async Task<IActionResult> ExamDebug()
    {
        var studentId = _userManager.GetUserId(User)!;
        var activeSchedules = await _examService.GetActiveExamSchedulesForStudentAsync(studentId);
        
        ViewBag.ExamService = _examService;
        ViewBag.StudentId = studentId;
        
        return View(activeSchedules);
    }

    [Route("my-exams")]
    public async Task<IActionResult> MyExams()
    {
        var studentId = _userManager.GetUserId(User)!;
        var activeSchedules = await _examService.GetActiveExamSchedulesForStudentAsync(studentId);
        
        ViewBag.ExamAttempts = await _context.ExamAttempts
            .Where(ea => ea.StudentId == studentId)
            .ToListAsync();
        ViewBag.ReExamPayments = await _context.ReExamPayments
            .Where(rep => rep.StudentId == studentId)
            .ToListAsync();
        ViewBag.ExamService = _examService;
        ViewBag.StudentId = studentId;
        ViewBag.CompletedEnrollments = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .ToListAsync();
        
        return View(activeSchedules);
    }

    [HttpGet]
    [Route("pre-exam-verification/{examId:int}")]
    public async Task<IActionResult> PreExamVerification(int examId)
    {
        var studentId = _userManager.GetUserId(User)!;
        var currentUser = await _userManager.GetUserAsync(User);
        
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == examId && e.IsApproved && e.IsActive);
        
        if (exam == null)
        {
            TempData["Error"] = "Exam not found or not available.";
            return RedirectToAction("MyExams");
        }
        
        var existingVerification = await _context.ExamVerifications
            .FirstOrDefaultAsync(ev => ev.StudentId == studentId && ev.ExamId == examId && ev.IsVerified);
        
        if (existingVerification != null)
        {
            return RedirectToAction("TakeExam", new { examId });
        }
        
        var (captchaQuestion, captchaAnswer) = _captchaService.GenerateMathCaptcha();
        
        var viewModel = new PreExamVerificationViewModel
        {
            ExamId = examId,
            Exam = exam,
            FullName = $"{currentUser?.FirstName} {currentUser?.LastName}".Trim(),
            Email = currentUser?.Email ?? "",
            PhoneNumber = currentUser?.PhoneNumber ?? "",
            CaptchaQuestion = captchaQuestion,
            CaptchaCorrectAnswer = captchaAnswer
        };
        
        TempData["CaptchaAnswer"] = captchaAnswer;
        
        return View(viewModel);
    }

    [HttpPost]
    [Route("pre-exam-verification")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreExamVerification(PreExamVerificationViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var (captchaQuestion, captchaAnswer) = _captchaService.GenerateMathCaptcha();
                model.CaptchaQuestion = captchaQuestion;
                model.CaptchaCorrectAnswer = captchaAnswer;
                TempData["CaptchaAnswer"] = captchaAnswer;
                
                model.Exam = await _context.Exams
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.Id == model.ExamId);
                
                return View(model);
            }
            
            var studentId = _userManager.GetUserId(User)!;
            var storedCaptchaAnswer = TempData["CaptchaAnswer"] as int? ?? 0;
            
            if (!_captchaService.VerifyCaptcha(model.CaptchaAnswer, storedCaptchaAnswer))
            {
                ModelState.AddModelError("CaptchaAnswer", "Incorrect CAPTCHA answer. Please try again.");
                
                var (captchaQuestion, captchaAnswer) = _captchaService.GenerateMathCaptcha();
                model.CaptchaQuestion = captchaQuestion;
                model.CaptchaCorrectAnswer = captchaAnswer;
                TempData["CaptchaAnswer"] = captchaAnswer;
                
                model.Exam = await _context.Exams
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.Id == model.ExamId);
                
                return View(model);
            }
            
            var existingVerification = await _context.ExamVerifications
                .FirstOrDefaultAsync(ev => ev.StudentId == studentId && ev.ExamId == model.ExamId);
            
            if (existingVerification != null)
            {
                if (existingVerification.IsVerified)
                {
                    return RedirectToAction("TakeExam", new { examId = model.ExamId });
                }
                else
                {
                    existingVerification.FullName = model.FullName;
                    existingVerification.EnrollmentNumber = model.EnrollmentNumber;
                    existingVerification.Email = model.Email;
                    existingVerification.PhoneNumber = model.PhoneNumber;
                    existingVerification.CaptchaVerified = true;
                    existingVerification.IsVerified = true;
                    existingVerification.VerifiedAt = DateTime.UtcNow;
                }
            }
            else
            {
                var verification = new ExamVerification
                {
                    StudentId = studentId,
                    ExamId = model.ExamId,
                    FullName = model.FullName,
                    EnrollmentNumber = model.EnrollmentNumber,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    CaptchaVerified = true,
                    IsVerified = true,
                    VerifiedAt = DateTime.UtcNow
                };
                
                _context.ExamVerifications.Add(verification);
            }
            
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Verification completed successfully! You can now proceed to the exam.";
            
            // Check if this is a re-exam scenario
            var completedAttempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(ea => ea.ExamId == model.ExamId && ea.StudentId == studentId && ea.IsCompleted && !ea.IsPassed);
            
            if (completedAttempt != null && await _examService.HasPaidForReExamAsync(studentId, completedAttempt.Id))
            {
                return RedirectToAction("ExamInterface", new { examId = model.ExamId });
            }
            
            return RedirectToAction("TakeExam", new { examId = model.ExamId });
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred during verification. Please try again.";
            
            var (captchaQuestion, captchaAnswer) = _captchaService.GenerateMathCaptcha();
            model.CaptchaQuestion = captchaQuestion;
            model.CaptchaCorrectAnswer = captchaAnswer;
            TempData["CaptchaAnswer"] = captchaAnswer;
            
            model.Exam = await _context.Exams
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == model.ExamId);
            
            return View(model);
        }
    }

    [Route("take-exam/{examId:int}")]
    public async Task<IActionResult> TakeExam(int examId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == examId && e.IsApproved && e.IsActive);
        
        if (exam == null)
        {
            TempData["Error"] = "Exam not found or not available.";
            return RedirectToAction("MyExams");
        }
        
        // Check if student has paid for exam access
        var hasExamAccess = await _certificatePaymentService.HasExamAccessAsync(studentId, exam.CourseId);
        if (!hasExamAccess)
        {
            TempData["Error"] = "Exam payment required. Please complete certificate payment (₹800) to access the exam.";
            return RedirectToAction("CertificatePayment", new { courseId = exam.CourseId });
        }
        
        // Check if this is a re-exam attempt (has paid for re-exam)
        var completedAttempt = await _context.ExamAttempts
            .FirstOrDefaultAsync(ea => ea.ExamId == examId && ea.StudentId == studentId && ea.IsCompleted && !ea.IsPassed);
        
        bool isReExam = false;
        if (completedAttempt != null)
        {
            isReExam = await _examService.HasPaidForReExamAsync(studentId, completedAttempt.Id);
        }
        
        // Skip verification check for re-exams
        if (!isReExam)
        {
            var existingVerification = await _context.ExamVerifications
                .FirstOrDefaultAsync(ev => ev.StudentId == studentId && ev.ExamId == examId && ev.IsVerified);
            
            if (existingVerification == null)
            {
                return RedirectToAction("PreExamVerification", new { examId });
            }
        }
        
        var examWithQuestions = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions.Where(eq => eq.Part == ExamPart.PartA))
            .ThenInclude(eq => eq.Options)
            .FirstOrDefaultAsync(e => e.Id == examId && e.IsApproved && e.IsActive);
        
        if (examWithQuestions == null)
        {
            TempData["Error"] = "Exam not found or not available.";
            return RedirectToAction("MyExams");
        }
        
        // Skip time availability check for re-exams
        if (!isReExam)
        {
            var isAvailable = await _examService.IsExamAvailableForStudentAsync(studentId, examId);
            if (!isAvailable)
            {
                var timeStatus = await _examService.GetExamTimeStatusAsync(examId, studentId);
                
                // Check if student has an assigned schedule but exam time hasn't started yet
                var schedule = await _context.ExamSchedules
                    .FirstOrDefaultAsync(es => es.StudentId == studentId && es.ExamId == examId && es.IsAssigned);
                
                if (schedule != null)
                {
                    TempData["Info"] = $"Exam is scheduled but not yet available. {timeStatus}";
                }
                else
                {
                    TempData["Error"] = "Exam not assigned to you. Contact your instructor for exam scheduling.";
                }
                return RedirectToAction("MyExams");
            }
        }
        
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == examWithQuestions.CourseId);
        
        if (enrollment == null)
        {
            TempData["Error"] = "You must be enrolled in this course to access the exam.";
            return RedirectToAction("MyExams");
        }
        
        // Check for existing incomplete attempt
        var existingAttempt = await _context.ExamAttempts
            .FirstOrDefaultAsync(ea => ea.ExamId == examId && ea.StudentId == studentId && !ea.IsCompleted);
        
        if (existingAttempt == null)
        {
            // Check if this is a re-exam scenario
            var failedAttempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(ea => ea.ExamId == examId && ea.StudentId == studentId && ea.IsCompleted && !ea.IsPassed);
            
            if (failedAttempt != null)
            {
                // Check if student has paid for re-exam
                var hasPaidReExam = await _examService.HasPaidForReExamAsync(studentId, failedAttempt.Id);
                if (hasPaidReExam)
                {
                    // For re-exams, directly redirect to exam interface
                    return RedirectToAction("ExamInterface", new { examId });
                }
                else
                {
                    TempData["Error"] = "You need to pay the re-exam fee to retake this exam.";
                    return RedirectToAction("ReExamPayment", new { examAttemptId = failedAttempt.Id });
                }
            }
            else
            {
                // First attempt
                var attempt = await _examService.StartExamAsync(studentId, examId);
                if (attempt == null)
                {
                    TempData["Error"] = "Unable to start exam. Exam may not be available at this time.";
                    return RedirectToAction("MyExams");
                }
            }
        }
        
        ViewBag.TimeStatus = await _examService.GetExamTimeStatusAsync(examId, studentId);
        ViewBag.IsExamTimeActive = await _examService.IsExamAvailableForStudentAsync(studentId, examId);
        ViewBag.CourseName = examWithQuestions.Course.Title;
        return View(examWithQuestions);
    }

    [HttpPost]
    [Route("start-exam-attempt")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartExamAttempt(int examId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        // Verify that the student has completed pre-exam verification
        var verification = await _context.ExamVerifications
            .FirstOrDefaultAsync(ev => ev.StudentId == studentId && ev.ExamId == examId && ev.IsVerified);
        
        if (verification == null)
        {
            TempData["Error"] = "Please complete pre-exam verification first.";
            return RedirectToAction("PreExamVerification", new { examId });
        }
        
        // Check if exam is available
        var isAvailable = await _examService.IsExamAvailableForStudentAsync(studentId, examId);
        if (!isAvailable)
        {
            TempData["Error"] = "Exam is not available at this time.";
            return RedirectToAction("MyExams");
        }
        
        // Start the exam attempt
        var attempt = await _examService.StartExamAsync(studentId, examId);
        if (attempt == null)
        {
            TempData["Error"] = "Unable to start exam. Please try again.";
            return RedirectToAction("TakeExam", new { examId });
        }
        
        // Redirect to the actual exam interface
        return RedirectToAction("ExamInterface", new { examId });
    }

    [Route("exam-interface/{examId:int}")]
    public async Task<IActionResult> ExamInterface(int examId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        // Check for active attempt first
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(ea => ea.ExamId == examId && ea.StudentId == studentId && !ea.IsCompleted);
        
        // If no active attempt, check if this is a re-exam scenario
        if (attempt == null)
        {
            var completedAttempt = await _context.ExamAttempts
                .FirstOrDefaultAsync(ea => ea.ExamId == examId && ea.StudentId == studentId && ea.IsCompleted && !ea.IsPassed);
            
            if (completedAttempt != null)
            {
                var hasPaidReExam = await _examService.HasPaidForReExamAsync(studentId, completedAttempt.Id);
                if (hasPaidReExam)
                {
                    // Create new re-exam attempt
                    attempt = await _examService.CreateReExamAttemptAsync(studentId, completedAttempt.Id);
                    if (attempt == null)
                    {
                        TempData["Error"] = "Unable to start re-exam. Please try again.";
                        return RedirectToAction("MyExams");
                    }
                }
                else
                {
                    TempData["Error"] = "Payment required for re-exam.";
                    return RedirectToAction("ReExamPayment", new { examAttemptId = completedAttempt.Id });
                }
            }
            else
            {
                TempData["Error"] = "No active exam attempt found.";
                return RedirectToAction("MyExams");
            }
        }
        
        var exam = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions.Where(eq => eq.Part == ExamPart.PartA))
            .ThenInclude(eq => eq.Options)
            .FirstOrDefaultAsync(e => e.Id == examId);
        
        if (exam == null)
        {
            TempData["Error"] = "Exam not found.";
            return RedirectToAction("MyExams");
        }
        
        // Create student-specific randomized order
        var seed = studentId.GetHashCode() + examId;
        var random = new Random(seed);
        var shuffledQuestions = exam.ExamQuestions.OrderBy(x => random.Next()).ToList();
        exam.ExamQuestions = shuffledQuestions;
        
        ViewBag.AttemptId = attempt.Id;
        ViewBag.StartTime = attempt.StartedAt;
        ViewBag.TimeRemaining = (attempt.StartedAt.AddMinutes(exam.DurationMinutes) - DateTime.UtcNow).TotalMinutes;
        
        return View(exam);
    }

    [HttpPost]
    [Route("submit-exam-part-a")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitExamPartA(int examId, int attemptId, IFormCollection form)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .FirstOrDefaultAsync(ea => ea.Id == attemptId && ea.StudentId == studentId && !ea.IsCompleted);
        
        if (attempt == null)
        {
            TempData["Error"] = "Invalid exam attempt.";
            return RedirectToAction("MyExams");
        }
        
        // Prepare answers for automatic MCQ scoring
        var answers = new List<EasyLearn.Services.ExamAnswerSubmission>();
        
        foreach (var key in form.Keys)
        {
            if (key.StartsWith("question_"))
            {
                var questionId = int.Parse(key.Replace("question_", ""));
                var selectedValue = form[key].ToString();
                
                if (!string.IsNullOrEmpty(selectedValue) && int.TryParse(selectedValue, out int optionId))
                {
                    answers.Add(new EasyLearn.Services.ExamAnswerSubmission
                    {
                        QuestionId = questionId,
                        SelectedOptionId = optionId
                    });
                }
            }
        }
        
        // Use ExamService for automatic MCQ scoring
        var success = await _examService.SubmitPartAAsync(attemptId, answers);
        
        if (!success)
        {
            TempData["Error"] = "Failed to submit Part A. Please try again.";
            return RedirectToAction("ExamInterface", new { examId });
        }
        
        // Reload attempt to get updated score
        attempt = await _context.ExamAttempts.FindAsync(attemptId);
        
        TempData["Success"] = "Part A submitted successfully! Now proceed to Part B.";
        return RedirectToAction("TakePartB", new { examId, attemptId });
    }

    [Route("take-part-b/{examId:int}/{attemptId:int}")]
    public async Task<IActionResult> TakePartB(int examId, int attemptId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(ea => ea.Id == attemptId && ea.StudentId == studentId && !ea.IsCompleted);
        
        if (attempt == null || !attempt.PartACompleted)
        {
            TempData["Error"] = "Please complete Part A first.";
            return RedirectToAction("MyExams");
        }
        
        var exam = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions.Where(eq => eq.Part == ExamPart.PartB))
            .FirstOrDefaultAsync(e => e.Id == examId);
        
        if (exam == null)
        {
            TempData["Error"] = "Exam not found.";
            return RedirectToAction("MyExams");
        }
        
        ViewBag.AttemptId = attemptId;
        ViewBag.TimeRemaining = (attempt.StartedAt.AddMinutes(exam.DurationMinutes) - DateTime.UtcNow).TotalMinutes;
        
        return View(exam);
    }

    [HttpPost]
    [Route("submit-exam-part-b")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitExamPartB(int examId, int attemptId, IFormCollection form)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var attempt = await _context.ExamAttempts
            .FirstOrDefaultAsync(ea => ea.Id == attemptId && ea.StudentId == studentId && !ea.IsCompleted);
        
        if (attempt == null || !attempt.PartACompleted)
        {
            TempData["Error"] = "Invalid exam attempt or Part A not completed.";
            return RedirectToAction("MyExams");
        }
        
        // Prepare Part B answers
        var answers = new List<EasyLearn.Services.ExamAnswerSubmission>();
        
        foreach (var key in form.Keys)
        {
            if (key.StartsWith("question_"))
            {
                var questionId = int.Parse(key.Replace("question_", ""));
                var answerText = form[key].ToString();
                
                answers.Add(new EasyLearn.Services.ExamAnswerSubmission
                {
                    QuestionId = questionId,
                    AnswerText = answerText
                });
            }
        }
        
        // Use ExamService for Part B submission
        var success = await _examService.SubmitPartBAsync(attemptId, answers);
        
        if (!success)
        {
            TempData["Error"] = "Failed to submit Part B. Please try again.";
            return RedirectToAction("TakePartB", new { examId, attemptId });
        }
        
        TempData["Success"] = "Exam completed successfully! Your answers have been submitted.";
        return RedirectToAction("ExamResult", new { attemptId });
    }

    [Route("exam-result/{attemptId:int}")]
    public async Task<IActionResult> ExamResult(int attemptId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .Include(ea => ea.ExamCertificate)
            .FirstOrDefaultAsync(ea => ea.Id == attemptId && ea.StudentId == studentId);
        
        if (attempt == null)
        {
            TempData["Error"] = "Exam attempt not found.";
            return RedirectToAction("MyExams");
        }
        
        return View(attempt);
    }

    [Route("exam-results")]
    public async Task<IActionResult> ExamResults()
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var attempts = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .Where(ea => ea.StudentId == studentId && ea.IsCompleted)
            .OrderByDescending(ea => ea.CompletedAt)
            .ToListAsync();
        
        return View(attempts);
    }

    [Route("approved-exams")]
    public async Task<IActionResult> ApprovedExams()
    {
        var studentId = _userManager.GetUserId(User)!;
        
        // Get enrolled courses for the student
        var enrolledCourseIds = await _context.Enrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.CourseId)
            .ToListAsync();
        
        // Get approved exams for enrolled courses
        var approvedExams = await _context.Exams
            .Include(e => e.Course)
            .ThenInclude(c => c.Category)
            .Include(e => e.Course)
            .ThenInclude(c => c.Instructor)
            .Where(e => e.IsApproved && e.IsActive && enrolledCourseIds.Contains(e.CourseId))
            .OrderBy(e => e.ScheduledStartTime)
            .ToListAsync();
        
        return View(approvedExams);
    }
    
    // Debug endpoint removed for security - MCQ scores should not be visible to students

    [HttpGet]
    [Route("re-exam-payment/{examAttemptId:int}")]
    public async Task<IActionResult> ReExamPayment(int examAttemptId)
    {
        var studentId = _userManager.GetUserId(User)!;
        var currentUser = await _userManager.GetUserAsync(User);
        
        var examAttempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(ea => ea.Id == examAttemptId && ea.StudentId == studentId);
        
        if (examAttempt == null)
        {
            TempData["Error"] = "Exam attempt not found.";
            return RedirectToAction("MyExams");
        }
        
        var viewModel = new ReExamPaymentViewModel
        {
            ExamAttemptId = examAttemptId,
            Exam = examAttempt.Exam,
            Course = examAttempt.Exam.Course,
            StudentName = $"{currentUser?.FirstName} {currentUser?.LastName}".Trim(),
            StudentEmail = currentUser?.Email ?? "",
            ReExamFee = 200,
            AvailablePaymentMethods = await _paymentService.GetAvailablePaymentMethodsAsync()
        };
        
        return View(viewModel);
    }

    [HttpPost]
    [Route("process-re-exam-payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessReExamPayment(ReExamPaymentViewModel model)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var examAttempt = await _context.ExamAttempts
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(ea => ea.Id == model.ExamAttemptId && ea.StudentId == studentId);
        
        if (examAttempt == null)
        {
            TempData["Error"] = "Exam attempt not found.";
            return RedirectToAction("MyExams");
        }
        
        // Check if payment already exists
        var existingPayment = await _context.ReExamPayments
            .FirstOrDefaultAsync(rep => rep.ExamAttemptId == model.ExamAttemptId && rep.StudentId == studentId);
        
        if (existingPayment != null)
        {
            TempData["Success"] = "Payment already completed! Redirecting to verification...";
            return RedirectToAction("PreExamVerification", new { examId = examAttempt.ExamId });
        }
        
        var reExamPayment = new ReExamPayment
        {
            StudentId = studentId,
            ExamAttemptId = model.ExamAttemptId,
            ExamId = examAttempt.ExamId,
            CourseId = examAttempt.Exam.CourseId,
            StudentName = model.StudentName,
            StudentEmail = model.StudentEmail,
            ReExamFee = model.ReExamFee,
            PaymentMethod = model.SelectedPaymentMethod,
            TransactionId = Guid.NewGuid().ToString("N")[..12].ToUpper(),
            PaymentStatus = "Completed",
            PaymentDate = DateTime.UtcNow
        };
        
        _context.ReExamPayments.Add(reExamPayment);
        await _context.SaveChangesAsync();
        
        TempData["Success"] = "Re-exam payment completed successfully! Please complete verification to start exam.";
        return RedirectToAction("PreExamVerification", new { examId = examAttempt.ExamId });
    }

    [HttpGet]
    [Route("certificate-payment/{courseId:int}")]
    public async Task<IActionResult> CertificatePayment(int courseId)
    {
        var studentId = _userManager.GetUserId(User)!;
        var currentUser = await _userManager.GetUserAsync(User);
        
        var enrollment = await _context.Enrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
        
        if (enrollment == null)
        {
            TempData["Error"] = "You must be enrolled in this course first.";
            return RedirectToAction("CourseDetails", new { id = courseId });
        }
        
        if (enrollment.CompletedAt == null)
        {
            TempData["Error"] = "You must complete the course (100% progress) before paying for certificate and exam access.";
            return RedirectToAction("MyCourses");
        }
        
        // Check if already paid
        var hasPaid = await _certificatePaymentService.HasPaidForCertificateAsync(studentId, courseId);
        if (hasPaid)
        {
            TempData["Info"] = "Certificate payment already completed.";
            return RedirectToAction("DownloadCertificate", new { courseId });
        }
        
        var viewModel = new CertificatePaymentViewModel
        {
            CourseId = courseId,
            Course = enrollment.Course,
            StudentName = $"{currentUser?.FirstName} {currentUser?.LastName}".Trim(),
            StudentEmail = currentUser?.Email ?? "",
            CertificateFee = 500m,
            ExamFee = 300m,
            TotalAmount = 800m,
            AvailablePaymentMethods = await _paymentService.GetAvailablePaymentMethodsAsync()
        };
        
        return View(viewModel);
    }
    
    [HttpPost]
    [Route("process-certificate-payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessCertificatePayment(CertificatePaymentViewModel model)
    {
        try
        {
            var studentId = _userManager.GetUserId(User)!;
            
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.CourseId == model.CourseId && e.StudentId == studentId);
            
            if (enrollment == null)
            {
                TempData["Error"] = "You must be enrolled in this course first.";
                return RedirectToAction("CourseDetails", new { id = model.CourseId });
            }
            
            if (enrollment.CompletedAt == null)
            {
                TempData["Error"] = "You must complete the course (100% progress) before paying for certificate and exam access.";
                return RedirectToAction("MyCourses");
            }
            
            // Process certificate payment
            var receipt = await _certificatePaymentService.ProcessCertificatePaymentAsync(
                studentId, model.CourseId, model.SelectedPaymentMethod, model.StudentName, model.StudentEmail);
            
            TempData["Success"] = "Certificate payment completed successfully! You now have access to exams and can download your certificate.";
            return RedirectToAction("CertificatePaymentConfirmation", new { receiptId = receipt.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("CertificatePayment", new { courseId = model.CourseId });
        }
        catch (Exception)
        {
            TempData["Error"] = "Payment processing failed. Please try again.";
            return RedirectToAction("CertificatePayment", new { courseId = model.CourseId });
        }
    }
    
    [Route("certificate-payment-confirmation/{receiptId:int}")]
    public async Task<IActionResult> CertificatePaymentConfirmation(int receiptId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var receipt = await _context.CertificatePaymentReceipts
            .Include(cpr => cpr.Course)
            .FirstOrDefaultAsync(cpr => cpr.Id == receiptId && cpr.StudentId == studentId);
        
        if (receipt == null)
        {
            TempData["Error"] = "Receipt not found.";
            return RedirectToAction("MyCourses");
        }
        
        var viewModel = new CertificatePaymentConfirmationViewModel
        {
            Receipt = receipt,
            Course = receipt.Course,
            ExamAccess = true,
            CertificateAccess = true
        };
        
        return View(viewModel);
    }
    
    [Route("download-certificate-receipt/{receiptId:int}")]
    [Route("downloadcertificatereceipt")]
    public async Task<IActionResult> DownloadCertificateReceipt(int receiptId)
    {
        try
        {
            var studentId = _userManager.GetUserId(User)!;
            
            var receipt = await _context.CertificatePaymentReceipts
                .Include(cpr => cpr.Course)
                .FirstOrDefaultAsync(cpr => cpr.Id == receiptId && cpr.StudentId == studentId);
            
            if (receipt == null)
            {
                TempData["Error"] = "Receipt not found.";
                return RedirectToAction("MyCourses");
            }
            
            // Generate PDF receipt
            var pdfBytes = await _certificatePaymentService.GenerateCertificateReceiptPdfAsync(receipt);
            var fileName = $"CertificatePaymentReceipt_{receipt.ReceiptNumber}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while generating the receipt. Please try again.";
            return RedirectToAction("MyCourses");
        }
    }
    [Route("DownloadCertificate/{courseId:int}")]
    public async Task<IActionResult> DownloadCertificate(int courseId)
    {
        try
        {
            var studentId = _userManager.GetUserId(User)!;
            
            // Check if student is enrolled and completed the course
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId && e.CompletedAt != null);
            
            if (enrollment == null)
            {
                TempData["Error"] = "You must complete the course to download the certificate.";
                return RedirectToAction("MyCourses");
            }
            
            // Check if student has paid for certificate
            var hasPaid = await _certificatePaymentService.HasPaidForCertificateAsync(studentId, courseId);
            if (!hasPaid)
            {
                TempData["Error"] = "Certificate payment required. Please complete payment to download your certificate.";
                return RedirectToAction("CertificatePayment", new { courseId });
            }
            
            // Check if certificate exists
            var certificate = await _context.Certificates
                .FirstOrDefaultAsync(c => c.CourseId == courseId && c.StudentId == studentId);
            
            if (certificate == null)
            {
                // Generate certificate
                certificate = await _certificateService.GenerateCertificateAsync(studentId, courseId);
                if (certificate == null)
                {
                    TempData["Error"] = "Unable to generate certificate. Please try again.";
                    return RedirectToAction("MyCourses");
                }
            }
            
            // Generate PDF certificate
            var pdfBytes = await _certificateService.GenerateCertificatePdfAsync(studentId, courseId);
            var fileName = $"Certificate_{enrollment.Course.Title.Replace(" ", "_")}_{certificate.CertificateNumber}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while generating the certificate. Please try again.";
            return RedirectToAction("MyCourses");
        }
    }

    [HttpGet]
    [Route("download-exam-certificate/{attemptId:int}")]
    public async Task<IActionResult> DownloadExamCertificate(int attemptId)
    {
        try
        {
            var studentId = _userManager.GetUserId(User)!;
            
            var attempt = await _context.ExamAttempts
                .Include(ea => ea.Exam)
                .ThenInclude(e => e.Course)
                .ThenInclude(c => c.Instructor)
                .Include(ea => ea.ExamCertificate)
                .FirstOrDefaultAsync(ea => ea.Id == attemptId && ea.StudentId == studentId && ea.IsPassed);
            
            if (attempt == null)
            {
                TempData["Error"] = "Exam certificate not found or exam not passed.";
                return RedirectToAction("ExamResults");
            }
            
            var student = await _userManager.GetUserAsync(User);
            var studentName = $"{student?.FirstName} {student?.LastName}".Trim();
            var courseTitle = attempt.Exam.Course.Title;
            var percentage = attempt.TotalScore;
            var certificateNumber = attempt.ExamCertificate?.CertificateNumber ?? $"EXAM-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}-{attemptId:D4}";
            var instructorName = $"{attempt.Exam.Course.Instructor.FirstName} {attempt.Exam.Course.Instructor.LastName}";
            
            // Generate exam certificate PDF
            var filePath = await _certificateService.GenerateExamCertificateAsync(
                studentName, courseTitle, percentage, certificateNumber, instructorName);
            
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "Certificate file could not be generated.";
                return RedirectToAction("ExamResults");
            }
            
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = $"ExamCertificate_{courseTitle.Replace(" ", "_")}_{certificateNumber}.pdf";
            
            return File(fileBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred while generating the exam certificate. Please try again.";
            return RedirectToAction("ExamResults");
        }
    }

    [HttpGet]
    [Route("export-progress")]
    public async Task<IActionResult> ExportProgress()
    {
        try
        {
            var studentId = _userManager.GetUserId(User)!;
            
            var enrollments = await _context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Category)
                .Include(e => e.Course)
                .ThenInclude(c => c.Instructor)
                .Where(e => e.StudentId == studentId)
                .ToListAsync();
            
            var csv = new StringBuilder();
            csv.AppendLine("Course Title,Instructor,Category,Progress %,Enrolled Date,Completed Date,Status");
            
            foreach (var enrollment in enrollments)
            {
                var courseTitle = enrollment.Course.Title?.Replace(",", ";") ?? "";
                var instructor = $"{enrollment.Course.Instructor?.FirstName} {enrollment.Course.Instructor?.LastName}".Replace(",", ";");
                var category = enrollment.Course.Category?.Name?.Replace(",", ";") ?? "";
                var status = enrollment.CompletedAt != null ? "Completed" : enrollment.Progress > 0 ? "In Progress" : "Not Started";
                var completedDate = enrollment.CompletedAt?.ToString("yyyy-MM-dd") ?? "";
                
                csv.AppendLine($"{courseTitle},{instructor},{category},{enrollment.Progress},{enrollment.EnrolledAt:yyyy-MM-dd},{completedDate},{status}");
            }
            
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"my-learning-progress-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            
            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Failed to export progress. Please try again.";
            return RedirectToAction("MyCourses");
        }
    }

    [HttpPost]
    [Route("add-review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(int CourseId, int Rating, string Comment = "")
    {
        try
        {
            var studentId = _userManager.GetUserId(User)!;
            
            // Check if student is enrolled and completed the course
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == CourseId && e.StudentId == studentId && e.CompletedAt != null);
            
            if (enrollment == null)
            {
                TempData["Error"] = "You must complete the course before leaving a review.";
                return RedirectToAction("CourseDetails", new { id = CourseId });
            }
            
            // Check if review already exists
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.CourseId == CourseId && r.StudentId == studentId);
            
            if (existingReview != null)
            {
                TempData["Error"] = "You have already reviewed this course.";
                return RedirectToAction("CourseDetails", new { id = CourseId });
            }
            
            // Create new review
            var review = new Review
            {
                CourseId = CourseId,
                StudentId = studentId,
                Rating = Rating,
                Comment = Comment?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow,
                IsApproved = true
            };
            
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Thank you for your review! It has been added successfully.";
            return RedirectToAction("CourseDetails", new { id = CourseId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred while submitting your review. Please try again.";
            return RedirectToAction("CourseDetails", new { id = CourseId });
        }
    }
    
    [HttpGet]
    [Route("request-missed-exam/{examId:int}")]
    public async Task<IActionResult> RequestMissedExam(int examId)
    {
        var studentId = _userManager.GetUserId(User)!;
        
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == examId && e.IsApproved && e.IsActive);
        
        if (exam == null)
        {
            TempData["Error"] = "Exam not found.";
            return RedirectToAction("MyExams");
        }
        
        if (!exam.IsExamMissed())
        {
            TempData["Error"] = "You can only request for missed exams.";
            return RedirectToAction("MyExams");
        }
        
        var existingRequest = await _context.MissedExamRequests
            .FirstOrDefaultAsync(mer => mer.StudentId == studentId && mer.ExamId == examId);
        
        if (existingRequest != null)
        {
            TempData["Info"] = "You have already submitted a request for this exam.";
            return RedirectToAction("MyExams");
        }
        
        var viewModel = new MissedExamRequestViewModel
        {
            ExamId = examId,
            Exam = exam
        };
        
        return View(viewModel);
    }
    
    [HttpPost]
    [Route("request-missed-exam")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestMissedExam(MissedExamRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Exam = await _context.Exams
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == model.ExamId);
            return View(model);
        }
        
        var studentId = _userManager.GetUserId(User)!;
        
        var success = await _examService.SubmitMissedExamRequestAsync(studentId, model.ExamId, model.Reason);
        
        if (success)
        {
            TempData["Success"] = "Your missed exam request has been submitted. The instructor will review and respond.";
        }
        else
        {
            TempData["Error"] = "Unable to submit request. Please try again.";
        }
        
        return RedirectToAction("MyExams");
    }
}