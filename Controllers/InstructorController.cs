using EasyLearn.Models;
using EasyLearn.Models.ViewModels;
using EasyLearn.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Controllers;

[Authorize(Roles = "Instructor")]
[Route("instructor")]
public class InstructorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IProfileService _profileService;
    private readonly IExamService _examService;

    public InstructorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IProfileService profileService, IExamService examService)
    {
        _context = context;
        _userManager = userManager;
        _profileService = profileService;
        _examService = examService;
    }

    [Route("")]
    [Route("dashboard")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(instructorId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get or create profile
            var profile = await _profileService.GetOrCreateProfileAsync(instructorId);
            ViewBag.Profile = profile;

            var courses = await _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Enrollments)
                .Include(c => c.Reviews)
                .Where(c => c.InstructorId == instructorId)
                .ToListAsync();

            var recentEnrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Where(e => e.Course.InstructorId == instructorId)
                .OrderByDescending(e => e.EnrolledAt)
                .Take(5)
                .ToListAsync();

            // Get quiz performance data
            var quizAttempts = await _context.QuizAttempts
                .Include(qa => qa.Quiz)
                .ThenInclude(q => q.Course)
                .Where(qa => qa.Quiz.Course.InstructorId == instructorId)
                .ToListAsync();

            // Get certificates issued
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                .Where(c => c.Course.InstructorId == instructorId)
                .CountAsync();

            var viewModel = new InstructorDashboardViewModel
            {
                TotalCourses = courses?.Count ?? 0,
                TotalStudents = courses?.SelectMany(c => c.Enrollments ?? new List<Enrollment>())
                    .Select(e => e.StudentId).Distinct().Count() ?? 0,
                TotalEarnings = courses?.Where(c => c.Price > 0)
                    .Sum(c => (c.Enrollments?.Count ?? 0) * c.Price) ?? 0,
                MyCourses = courses?.OrderByDescending(c => c.CreatedAt).Take(5).ToList() ?? new List<Course>(),
                RecentEnrollments = recentEnrollments ?? new List<Enrollment>(),
                QuizPerformance = new QuizPerformanceViewModel
                {
                    TotalAttempts = quizAttempts?.Count ?? 0,
                    AverageScore = quizAttempts?.Any() == true ? quizAttempts.Average(qa => qa.Percentage) : 0,
                    PassRate = quizAttempts?.Any() == true ? 
                        (double)quizAttempts.Count(qa => qa.IsPassed) / quizAttempts.Count * 100 : 0
                },
                CertificatesIssued = certificates
            };

            return View(viewModel);
        }
        catch (Exception)
        {
            // Log the exception
            TempData["Error"] = "An error occurred while loading the dashboard.";
            return View(new InstructorDashboardViewModel());
        }
    }

    [Route("my-courses")]
    public async Task<IActionResult> MyCourses()
    {
        var instructorId = _userManager.GetUserId(User)!;
        var courses = await _context.Courses
            .Include(c => c.Category)
            .Include(c => c.Enrollments)
            .Include(c => c.Reviews)
            .Where(c => c.InstructorId == instructorId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return View(courses);
    }

    [HttpGet]
    [Route("create-course")]
    public async Task<IActionResult> CreateCourse()
    {
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        return View();
    }

    [HttpPost]
    [Route("create-course")]
    public async Task<IActionResult> CreateCourse(CourseCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var course = new Course
            {
                Title = model.Title,
                Description = model.Description,
                Price = model.Price,
                ThumbnailUrl = model.ThumbnailUrl,
                CategoryId = model.CategoryId,
                InstructorId = _userManager.GetUserId(User)!,
                IsFeatured = model.IsFeatured
            };
            
            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(MyCourses));
        }
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        return View(model);
    }

    [Route("manage-content/{courseId:int}")]
    public async Task<IActionResult> ManageContent(int courseId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(instructorId))
            {
                return RedirectToAction("Login", "Account");
            }

            var course = await _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Lessons.OrderBy(l => l.OrderIndex))
                .Include(c => c.Quizzes)
                .ThenInclude(q => q.Questions)
                .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
            
            if (course == null) 
            {
                TempData["Error"] = "Course not found or access denied";
                return RedirectToAction("MyCourses");
            }
            
            return View(course);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while loading course content";
            return RedirectToAction("MyCourses");
        }
    }

    [HttpGet]
    [Route("managelessons/{courseId:int}")]
    public async Task<IActionResult> ManageLessons(int courseId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(instructorId))
            {
                return RedirectToAction("Login", "Account");
            }

            var course = await _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Lessons.OrderBy(l => l.OrderIndex))
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
            
            if (course == null) 
            {
                TempData["Error"] = "Course not found or access denied";
                return RedirectToAction("MyCourses");
            }
            
            return View(course);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while loading lessons";
            return RedirectToAction("MyCourses");
        }
    }

    [HttpPost]
    [Route("update-lesson")]
    public async Task<IActionResult> UpdateLesson([FromBody] LessonUpdateViewModel model)
    {
        try
        {
            // Validate video URL if provided
            if (!string.IsNullOrEmpty(model.VideoUrl) && !IsValidVideoUrl(model.VideoUrl))
            {
                return Json(new { success = false, message = "Invalid video URL format. Please use a valid YouTube URL." });
            }

            var instructorId = _userManager.GetUserId(User);
            var existingLesson = await _context.Lessons
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == model.Id && l.Course.InstructorId == instructorId);
            
            if (existingLesson == null)
            {
                return Json(new { success = false, message = "Lesson not found or access denied" });
            }

            existingLesson.Title = model.Title;
            existingLesson.Description = model.Description ?? string.Empty;
            existingLesson.VideoUrl = model.VideoUrl;
            existingLesson.Script = model.Script;
            if (!string.IsNullOrEmpty(model.MaterialUrl))
            {
                existingLesson.MaterialUrl = model.MaterialUrl;
            }
            existingLesson.OrderIndex = model.OrderIndex;
            existingLesson.Duration = TimeSpan.FromMinutes(model.Duration);
            existingLesson.IsActive = true; // Ensure updated lessons remain visible to students
            
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while updating the lesson" });
        }
    }

    [HttpGet]
    [Route("get-lesson/{id:int}")]
    public async Task<IActionResult> GetLesson(int id)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            var lesson = await _context.Lessons
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == id && l.Course.InstructorId == instructorId);
            
            if (lesson == null)
            {
                return Json(null);
            }

            return Json(new {
                id = lesson.Id,
                title = lesson.Title,
                description = lesson.Description,
                videoUrl = lesson.VideoUrl,
                materialUrl = lesson.MaterialUrl,
                script = lesson.Script,
                orderIndex = lesson.OrderIndex,
                duration = (int)lesson.Duration.TotalMinutes
            });
        }
        catch (Exception)
        {
            return Json(null);
        }
    }

    [HttpDelete]
    [Route("delete-lesson/{id:int}")]
    public async Task<IActionResult> DeleteLesson(int id)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            var lesson = await _context.Lessons
                .Include(l => l.Course)
                .FirstOrDefaultAsync(l => l.Id == id && l.Course.InstructorId == instructorId);
            
            if (lesson == null)
            {
                return Json(new { success = false, message = "Lesson not found or access denied" });
            }

            _context.Lessons.Remove(lesson);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while deleting the lesson" });
        }
    }

    [HttpPost]
    [Route("create-lesson")]
    public async Task<IActionResult> CreateLesson([FromForm] LessonCreateViewModel model)
    {
        try
        {
            if (string.IsNullOrEmpty(model.Title))
            {
                return Json(new { success = false, message = "Title is required" });
            }

            // Validate video URL if provided
            if (!string.IsNullOrEmpty(model.VideoUrl) && !IsValidVideoUrl(model.VideoUrl))
            {
                return Json(new { success = false, message = "Invalid video URL format. Please use a valid YouTube URL." });
            }

            var instructorId = _userManager.GetUserId(User);
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == instructorId);
            
            if (course == null)
            {
                return Json(new { success = false, message = "Course not found" });
            }

            var lesson = new Lesson
            {
                Title = model.Title,
                Description = model.Description ?? "",
                VideoUrl = model.VideoUrl,
                MaterialUrl = model.MaterialUrl,
                Script = model.Script,
                OrderIndex = model.OrderIndex > 0 ? model.OrderIndex : 1,
                Duration = TimeSpan.FromMinutes(model.Duration > 0 ? model.Duration : 10),
                CourseId = model.CourseId,
                IsActive = true, // Automatically visible to enrolled students
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Lessons.Add(lesson);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, lessonId = lesson.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }



    [HttpPost]
    [Route("upload-material")]
    public async Task<IActionResult> UploadMaterial(IFormFile file, int courseId)
    {
        if (file != null && file.Length > 0)
        {
            var allowedTypes = new[] { ".pdf", ".doc", ".docx" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            
            if (allowedTypes.Contains(extension))
            {
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine("wwwroot/uploads/materials", fileName);
                
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                
                return Json(new { success = true, url = $"/uploads/materials/{fileName}" });
            }
        }
        return Json(new { success = false, message = "Invalid file" });
    }

    [HttpGet]
    [Route("managequizzes/{courseId:int}")]
    public async Task<IActionResult> ManageQuizzes(int courseId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(instructorId))
            {
                return RedirectToAction("Login", "Account");
            }

            var course = await _context.Courses
                .Include(c => c.Category)
                .Include(c => c.Quizzes)
                .ThenInclude(q => q.Questions)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
            
            if (course == null) 
            {
                TempData["Error"] = "Course not found or access denied";
                return RedirectToAction("MyCourses");
            }
            
            return View(course);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while loading quizzes";
            return RedirectToAction("MyCourses");
        }
    }

    [HttpGet]
    [Route("create-quiz/{courseId:int}")]
    public async Task<IActionResult> CreateQuiz(int courseId)
    {
        var course = await _context.Courses.FindAsync(courseId);
        if (course?.InstructorId != _userManager.GetUserId(User)) return NotFound();
        
        ViewBag.CourseId = courseId;
        return View();
    }

    [HttpPost]
    [Route("create-quiz")]
    public async Task<IActionResult> CreateQuiz([FromForm] string Title, [FromForm] string Description, [FromForm] int TimeLimit, [FromForm] int PassingScore, [FromForm] int CourseId)
    {
        try
        {
            if (string.IsNullOrEmpty(Title))
            {
                return Json(new { success = false, message = "Title is required" });
            }

            var instructorId = _userManager.GetUserId(User);
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == CourseId && c.InstructorId == instructorId);
            
            if (course == null)
            {
                return Json(new { success = false, message = "Course not found" });
            }

            var quiz = new Quiz
            {
                Title = Title,
                Description = Description ?? "",
                TimeLimit = TimeLimit > 0 ? TimeLimit : 30,
                PassingScore = PassingScore > 0 ? PassingScore : 70,
                CourseId = CourseId
            };
            
            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, quizId = quiz.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [Route("manage-questions/{quizId:int}")]
    public async Task<IActionResult> ManageQuestions(int quizId)
    {
        var quiz = await _context.Quizzes
            .Include(q => q.Questions)
            .ThenInclude(q => q.Answers)
            .Include(q => q.Course)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.Course.InstructorId == _userManager.GetUserId(User));
        
        if (quiz == null) return NotFound();
        return View(quiz);
    }

    [HttpPost]
    [Route("create-question")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestion([FromForm] string Text, [FromForm] int Type, [FromForm] int Points, [FromForm] int QuizId, [FromForm] List<string> answerTexts, [FromForm] List<bool> isCorrectAnswers, [FromForm] string? trueFalseCorrect)
    {
        try
        {
            if (string.IsNullOrEmpty(Text) || QuizId <= 0)
            {
                TempData["Error"] = "Question text and quiz are required";
                return RedirectToAction(nameof(ManageQuestions), new { quizId = QuizId });
            }

            // Verify quiz belongs to instructor
            var instructorId = _userManager.GetUserId(User);
            var quiz = await _context.Quizzes
                .Include(q => q.Course)
                .FirstOrDefaultAsync(q => q.Id == QuizId && q.Course.InstructorId == instructorId);
            
            if (quiz == null)
            {
                TempData["Error"] = "Quiz not found or access denied";
                return RedirectToAction("Dashboard");
            }

            var question = new Question
            {
                Text = Text,
                Type = (QuestionType)Type,
                Points = Points > 0 ? Points : 1,
                QuizId = QuizId,
                OrderIndex = await _context.Questions.CountAsync(q => q.QuizId == QuizId) + 1
            };

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();

            // Add answers based on question type
            if (Type == (int)QuestionType.TrueFalse)
            {
                var trueAnswer = new Answer { QuestionId = question.Id, Text = "True", IsCorrect = trueFalseCorrect == "True", OrderIndex = 1 };
                var falseAnswer = new Answer { QuestionId = question.Id, Text = "False", IsCorrect = trueFalseCorrect == "False", OrderIndex = 2 };
                
                _context.Answers.AddRange(trueAnswer, falseAnswer);
            }
            else if (answerTexts?.Any() == true)
            {
                for (int i = 0; i < answerTexts.Count; i++)
                {
                    if (!string.IsNullOrEmpty(answerTexts[i]))
                    {
                        var answer = new Answer
                        {
                            QuestionId = question.Id,
                            Text = answerTexts[i],
                            IsCorrect = isCorrectAnswers != null && i < isCorrectAnswers.Count && isCorrectAnswers[i],
                            OrderIndex = i + 1
                        };
                        _context.Answers.Add(answer);
                    }
                }
            }
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Question added successfully!";
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while creating the question";
        }
        
        return RedirectToAction(nameof(ManageQuestions), new { quizId = QuizId });
    }

    [HttpGet]
    [Route("get-question/{id:int}")]
    public async Task<IActionResult> GetQuestion(int id)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            var question = await _context.Questions
                .Include(q => q.Quiz)
                .ThenInclude(q => q.Course)
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == id && q.Quiz.Course.InstructorId == instructorId);
            
            if (question == null)
            {
                return Json(null);
            }

            return Json(new {
                id = question.Id,
                text = question.Text,
                type = (int)question.Type,
                points = question.Points,
                answers = question.Answers.OrderBy(a => a.OrderIndex).Select(a => new {
                    id = a.Id,
                    text = a.Text,
                    isCorrect = a.IsCorrect,
                    orderIndex = a.OrderIndex
                }).ToList()
            });
        }
        catch (Exception)
        {
            return Json(null);
        }
    }

    [HttpPost]
    [Route("update-question")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuestion([FromForm] int Id, [FromForm] string Text, [FromForm] int Type, [FromForm] int Points, [FromForm] List<string> answerTexts, [FromForm] List<bool> isCorrectAnswers, [FromForm] string? trueFalseCorrect)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            var question = await _context.Questions
                .Include(q => q.Quiz)
                .ThenInclude(q => q.Course)
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == Id && q.Quiz.Course.InstructorId == instructorId);
            
            if (question == null)
            {
                TempData["Error"] = "Question not found or access denied";
                return RedirectToAction("Dashboard");
            }

            question.Text = Text;
            question.Type = (QuestionType)Type;
            question.Points = Points > 0 ? Points : 1;

            // Remove existing answers
            _context.Answers.RemoveRange(question.Answers);
            await _context.SaveChangesAsync();

            // Add new answers based on question type
            if (Type == (int)QuestionType.TrueFalse)
            {
                var trueAnswer = new Answer { QuestionId = question.Id, Text = "True", IsCorrect = trueFalseCorrect == "True", OrderIndex = 1 };
                var falseAnswer = new Answer { QuestionId = question.Id, Text = "False", IsCorrect = trueFalseCorrect == "False", OrderIndex = 2 };
                
                _context.Answers.AddRange(trueAnswer, falseAnswer);
            }
            else if (answerTexts?.Any() == true)
            {
                for (int i = 0; i < answerTexts.Count; i++)
                {
                    if (!string.IsNullOrEmpty(answerTexts[i]))
                    {
                        var answer = new Answer
                        {
                            QuestionId = question.Id,
                            Text = answerTexts[i],
                            IsCorrect = isCorrectAnswers != null && i < isCorrectAnswers.Count && isCorrectAnswers[i],
                            OrderIndex = i + 1
                        };
                        _context.Answers.Add(answer);
                    }
                }
            }
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Question updated successfully!";
            
            return RedirectToAction(nameof(ManageQuestions), new { quizId = question.QuizId });
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while updating the question";
            return RedirectToAction("Dashboard");
        }
    }

    [HttpDelete]
    [Route("delete-question/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            var question = await _context.Questions
                .Include(q => q.Quiz)
                .ThenInclude(q => q.Course)
                .FirstOrDefaultAsync(q => q.Id == id && q.Quiz.Course.InstructorId == instructorId);
            
            if (question == null)
            {
                return Json(new { success = false, message = "Question not found or access denied" });
            }

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while deleting the question" });
        }
    }

    [Route("issue-certificate/{courseId:int}/{studentId}")]
    public async Task<IActionResult> IssueCertificate(int courseId, string studentId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            
            // Verify course belongs to instructor
            var course = await _context.Courses
                .Include(c => c.Lessons)
                .Include(c => c.Quizzes)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
            
            if (course == null)
            {
                return NotFound();
            }

            // Check if student has completed all requirements
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
            
            if (enrollment == null || !enrollment.IsCompleted)
            {
                TempData["Error"] = "Student has not completed the course requirements";
                return RedirectToAction("StudentPerformance", new { courseId });
            }

            // Check if certificate already exists
            var existingCertificate = await _context.Certificates
                .FirstOrDefaultAsync(c => c.CourseId == courseId && c.StudentId == studentId);
            
            if (existingCertificate != null)
            {
                TempData["Info"] = "Certificate already issued for this student";
                return RedirectToAction("StudentPerformance", new { courseId });
            }

            // Create certificate
            var certificate = new Certificate
            {
                StudentId = studentId,
                CourseId = courseId,
                CertificateNumber = GenerateCertificateNumber(),
                IssuedAt = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddYears(1),
                FilePath = "" // Will be generated by certificate service
            };

            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Certificate issued successfully!";
            return RedirectToAction("StudentPerformance", new { courseId });
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while issuing the certificate";
            return RedirectToAction("StudentPerformance", new { courseId });
        }
    }

    [Route("export-performance/{courseId:int}")]
    public async Task<IActionResult> ExportPerformance(int courseId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            
            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Where(e => e.CourseId == courseId && e.Course.InstructorId == instructorId)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Student Name,Email,Enrolled Date,Progress %,Status,Completed Date");

            foreach (var enrollment in enrollments)
            {
                csv.AppendLine($"{enrollment.Student?.FirstName} {enrollment.Student?.LastName},{enrollment.Student?.Email},{enrollment.EnrolledAt:yyyy-MM-dd},{enrollment.ProgressPercentage:F1},{(enrollment.IsCompleted ? "Completed" : "In Progress")},{enrollment.CompletedAt?.ToString("yyyy-MM-dd") ?? ""}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"student-performance-{courseId}-{DateTime.Now:yyyyMMdd}.csv");
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while exporting data";
            return RedirectToAction("StudentPerformance", new { courseId });
        }
    }

    private string GenerateCertificateNumber()
    {
        return $"CERT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private bool IsValidVideoUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        
        try
        {
            // Check for YouTube URLs
            if (url.Contains("youtube.com/watch") || url.Contains("youtu.be/"))
            {
                var videoId = ExtractYouTubeVideoId(url);
                return !string.IsNullOrEmpty(videoId) && videoId.Length >= 10;
            }
            
            // Check for other video formats (basic validation)
            var uri = new Uri(url);
            var extension = Path.GetExtension(uri.AbsolutePath).ToLower();
            var validExtensions = new[] { ".mp4", ".webm", ".ogg", ".avi", ".mov" };
            
            return validExtensions.Contains(extension) || url.Contains("youtube") || url.Contains("vimeo");
        }
        catch
        {
            return false;
        }
    }

    private string? ExtractYouTubeVideoId(string url)
    {
        try
        {
            if (url.Contains("youtu.be/"))
            {
                return url.Split('/').Last().Split('?')[0];
            }
            else if (url.Contains("youtube.com/watch"))
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["v"];
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    [HttpGet]
    [Route("editcourse/{id:int}")]
    public async Task<IActionResult> EditCourse(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == _userManager.GetUserId(User));
        
        if (course == null) return NotFound();
        
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        
        var model = new CourseCreateViewModel
        {
            Title = course.Title,
            Description = course.Description,
            Price = course.Price,
            ImageUrl = course.ImageUrl,
            CategoryId = course.CategoryId
        };
        
        ViewBag.CourseId = id;
        return View(model);
    }

    [HttpPost]
    [Route("editcourse/{id:int}")]
    public async Task<IActionResult> EditCourse(int id, CourseCreateViewModel model)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id && c.InstructorId == _userManager.GetUserId(User));
        if (course == null) return NotFound();
        
        if (ModelState.IsValid)
        {
            course.Title = model.Title;
            course.Description = model.Description;
            course.Price = model.Price;
            course.ImageUrl = model.ImageUrl;
            course.CategoryId = model.CategoryId;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Course updated successfully.";
            return RedirectToAction(nameof(MyCourses));
        }
        
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.CourseId = id;
        return View(model);
    }

    [Route("student-performance/{courseId:int}")]
    public async Task<IActionResult> StudentPerformance(int courseId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(instructorId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Verify course belongs to instructor
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
            
            if (course == null)
            {
                return NotFound();
            }

            var enrollments = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Where(e => e.CourseId == courseId)
                .ToListAsync();
            
            var quizResults = await _context.QuizAttempts
                .Include(qa => qa.Student)
                .Include(qa => qa.Quiz)
                .Where(qa => qa.Quiz.CourseId == courseId)
                .GroupBy(qa => qa.StudentId)
                .Select(g => new {
                    StudentId = g.Key,
                    AverageScore = g.Average(qa => qa.TotalPoints > 0 ? (double)qa.Score / qa.TotalPoints * 100 : 0),
                    AttemptCount = g.Count(),
                    BestScore = g.Max(qa => qa.TotalPoints > 0 ? (double)qa.Score / qa.TotalPoints * 100 : 0)
                })
                .ToListAsync();
            
            // Get lesson progress
            var lessonProgress = await _context.LessonProgresses
                .Include(lp => lp.Lesson)
                .Where(lp => lp.Lesson.CourseId == courseId)
                .GroupBy(lp => lp.StudentId)
                .Select(g => new {
                    StudentId = g.Key,
                    CompletedLessons = g.Count(lp => lp.IsCompleted),
                    TotalLessons = _context.Lessons.Count(l => l.CourseId == courseId)
                })
                .ToListAsync();
            
            ViewBag.QuizResults = quizResults.Cast<object>().ToList();
            ViewBag.LessonProgress = lessonProgress.Cast<object>().ToList();
            ViewBag.Course = course;
            
            return View(enrollments ?? new List<Enrollment>());
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while loading student performance data.";
            return RedirectToAction("Dashboard");
        }
    }

    [AllowAnonymous]
    [Route("test-route/{id:int}")]
    public IActionResult TestRoute(int id)
    {
        return Content($"Route working! ID: {id}");
    }

    // Exam Management
    [HttpGet]
    [Route("create-exam")]
    public async Task<IActionResult> CreateExam(int? courseId = null)
    {
        if (courseId.HasValue)
        {
            var course = await _context.Courses.FindAsync(courseId.Value);
            if (course?.InstructorId != _userManager.GetUserId(User)) return NotFound();
            
            ViewBag.CourseId = courseId.Value;
            ViewBag.Course = course;
            return View();
        }
        
        // If no courseId provided, show course selection
        var instructorId = _userManager.GetUserId(User)!;
        var courses = await _context.Courses
            .Include(c => c.Category)
            .Include(c => c.Enrollments)
            .Include(c => c.Lessons)
            .Include(c => c.Quizzes)
            .Include(c => c.Reviews)
            .Where(c => c.InstructorId == instructorId && c.IsApproved)
            .ToListAsync();
        
        ViewBag.Courses = courses;
        return View("SelectCourseForExam");
    }

    [HttpGet]
    [Route("create-exam/{courseId:int}")]
    public async Task<IActionResult> CreateExamForCourse(int courseId)
    {
        var course = await _context.Courses.FindAsync(courseId);
        if (course?.InstructorId != _userManager.GetUserId(User)) return NotFound();
        
        ViewBag.CourseId = courseId;
        ViewBag.Course = course;
        return View("CreateExam");
    }

    [HttpPost]
    [Route("create-exam")]
    public async Task<IActionResult> CreateExam(ExamCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var exam = new Exam
            {
                Title = model.Title,
                Description = model.Description,
                ScheduledStartTime = model.ExamDate,
                ScheduledEndTime = model.ExamDate.AddMinutes(120),
                PatternType = model.PatternType,
                InstructorInstructions = model.InstructorInstructions ?? string.Empty,
                CourseId = model.CourseId,
                InstructorId = _userManager.GetUserId(User)!
            };
            
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Exam created successfully. Awaiting admin approval.";
            return RedirectToAction("ManageExams", new { courseId = model.CourseId });
        }
        
        var course = await _context.Courses.FindAsync(model.CourseId);
        ViewBag.Course = course;
        return View(model);
    }

    [Route("manage-exams/{courseId:int}")]
    public async Task<IActionResult> ManageExams(int courseId)
    {
        var instructorId = _userManager.GetUserId(User);
        var course = await _context.Courses
            .Include(c => c.Category)
            .Include(c => c.Enrollments)
            .Include(c => c.Lessons)
            .Include(c => c.Quizzes)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.InstructorId == instructorId);
        
        if (course == null) return NotFound();
        
        var exams = await _context.Exams
            .Include(e => e.ExamAttempts)
            .Where(e => e.CourseId == courseId)
            .ToListAsync();
        
        ViewBag.Course = course;
        return View(exams);
    }

    [Route("manage-exam-questions/{examId:int}")]
    public async Task<IActionResult> ManageExamQuestions(int examId)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Options)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == examId && e.InstructorId == _userManager.GetUserId(User));
        
        if (exam == null) return NotFound();
        return View(exam);
    }

    [HttpPost]
    [Route("create-exam-question")]
    public async Task<IActionResult> CreateExamQuestion(ExamQuestionCreateViewModel model)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            var exam = await _context.Exams
                .FirstOrDefaultAsync(e => e.Id == model.ExamId && e.InstructorId == instructorId);
            
            if (exam == null)
            {
                return Json(new { success = false, message = "Exam not found" });
            }

            var question = new ExamQuestion
            {
                Text = model.Text,
                Type = model.Type,
                Part = model.Part,
                Points = model.Points,
                ExamId = model.ExamId,
                OrderIndex = await _context.ExamQuestions.CountAsync(eq => eq.ExamId == model.ExamId) + 1
            };

            _context.ExamQuestions.Add(question);
            await _context.SaveChangesAsync();

            // Add options for MCQ questions
            if (model.Type == ExamQuestionType.MultipleChoice && model.Options?.Any() == true)
            {
                for (int i = 0; i < model.Options.Count; i++)
                {
                    var option = new ExamQuestionOption
                    {
                        ExamQuestionId = question.Id,
                        Text = model.Options[i],
                        IsCorrect = model.CorrectOptions?.Contains(i) == true,
                        OrderIndex = i + 1
                    };
                    _context.ExamQuestionOptions.Add(option);
                }
                await _context.SaveChangesAsync();
            }
            
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete]
    [Route("delete-exam-question/{questionId:int}")]
    public async Task<IActionResult> DeleteExamQuestion(int questionId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User);
            var question = await _context.ExamQuestions
                .Include(eq => eq.Exam)
                .FirstOrDefaultAsync(eq => eq.Id == questionId && eq.Exam.InstructorId == instructorId);
            
            if (question == null)
            {
                return Json(new { success = false, message = "Question not found" });
            }

            _context.ExamQuestions.Remove(question);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [Route("internal-assessments")]
    public async Task<IActionResult> InternalAssessments()
    {
        var instructorId = _userManager.GetUserId(User)!;
        var pendingAssessments = await _examService.GetPendingInternalAssessmentsAsync(instructorId);
        
        // Fix MCQ scores for any attempts showing 0
        foreach (var attempt in pendingAssessments.Where(a => a.PartACompleted && a.PartAScore == 0))
        {
            var partAAnswers = await _context.ExamAnswers
                .Include(ea => ea.ExamQuestion)
                .ThenInclude(eq => eq.Options)
                .Where(ea => ea.ExamAttemptId == attempt.Id && ea.ExamQuestion.Part == ExamPart.PartA)
                .ToListAsync();

            int score = 0;
            foreach (var answer in partAAnswers)
            {
                if (answer.SelectedOptionId.HasValue)
                {
                    var selectedOption = answer.ExamQuestion.Options.FirstOrDefault(o => o.Id == answer.SelectedOptionId);
                    if (selectedOption != null)
                    {
                        int points = selectedOption.IsCorrect ? 2 : -1;
                        score += points;
                        answer.IsCorrect = selectedOption.IsCorrect;
                        answer.Points = points;
                    }
                }
            }
            attempt.PartAScore = Math.Max(0, score);
        }
        
        await _context.SaveChangesAsync();
        return View(pendingAssessments);
    }

    [Route("grade-part-b/{examAttemptId:int}")]
    public async Task<IActionResult> GradePartB(int examAttemptId)
    {
        var instructorId = _userManager.GetUserId(User)!;
        
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Student)
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.ExamQuestions.Where(eq => eq.Part == ExamPart.PartB))
            .Include(ea => ea.ExamAnswers.Where(ans => ans.ExamAttemptId == examAttemptId))
            .ThenInclude(ea => ea.ExamQuestion)
            .FirstOrDefaultAsync(ea => ea.Id == examAttemptId && ea.Exam.InstructorId == instructorId);
        
        if (attempt == null || !attempt.PartBCompleted)
        {
            TempData["Error"] = "Exam attempt not found or Part B not completed.";
            return RedirectToAction("InternalAssessments");
        }
        
        return View(attempt);
    }

    [HttpPost]
    [Route("submit-part-b-grades")]
    public async Task<IActionResult> SubmitPartBGrades(int examAttemptId, Dictionary<int, int> questionGrades, int internalMarks)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User)!;
            
            var attempt = await _context.ExamAttempts
                .Include(ea => ea.Exam)
                .Include(ea => ea.ExamAnswers.Where(ans => ans.ExamAttemptId == examAttemptId))
                .FirstOrDefaultAsync(ea => ea.Id == examAttemptId && ea.Exam.InstructorId == instructorId);
            
            if (attempt == null)
            {
                TempData["Error"] = "Exam attempt not found.";
                return RedirectToAction("InternalAssessments");
            }
            
            // Update Part B scores - ensure we only update answers for THIS specific attempt
            int partBScore = 0;
            foreach (var grade in questionGrades)
            {
                var answer = await _context.ExamAnswers
                    .FirstOrDefaultAsync(ea => ea.ExamAttemptId == examAttemptId && ea.ExamQuestionId == grade.Key);
                if (answer != null)
                {
                    answer.Points = grade.Value;
                    partBScore += grade.Value;
                }
            }
            
            // Update scores and marks
            attempt.PartBScore = partBScore;
            attempt.InternalScore = Math.Min(internalMarks, attempt.Exam.InternalMarks);
            attempt.TotalScore = attempt.PartAScore + attempt.PartBScore + attempt.InternalScore;
            attempt.Percentage = (double)attempt.TotalScore / attempt.Exam.TotalMarks * 100;
            attempt.IsPassed = attempt.Percentage >= attempt.Exam.PassingPercentage;
            attempt.ResultPublished = true;
            attempt.ResultPublishedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            // Generate certificate if passed
            if (attempt.IsPassed)
            {
                await _examService.GenerateExamCertificateAsync(examAttemptId);
            }
            
            TempData["Success"] = "Part B graded successfully and results published.";
            return RedirectToAction("InternalAssessments");
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred while grading Part B.";
            return RedirectToAction("InternalAssessments");
        }
    }

    [HttpPost]
    [Route("assign-internal-marks")]
    public async Task<IActionResult> AssignInternalMarks(int examAttemptId, int internalMarks)
    {
        var instructorId = _userManager.GetUserId(User)!;
        var success = await _examService.AssignInternalMarksAsync(examAttemptId, internalMarks, instructorId);
        
        if (success)
        {
            TempData["Success"] = "Internal marks assigned successfully. Results will be published after 3 hours.";
        }
        else
        {
            TempData["Error"] = "Failed to assign internal marks.";
        }
        
        return RedirectToAction("InternalAssessments");
    }

    [Route("exam-results/{examId:int}")]
    public async Task<IActionResult> ExamResults(int examId)
    {
        var instructorId = _userManager.GetUserId(User)!;
        var exam = await _context.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamAttempts)
            .ThenInclude(ea => ea.Student)
            .FirstOrDefaultAsync(e => e.Id == examId && e.InstructorId == instructorId);
        
        if (exam == null) return NotFound();
        
        return View(exam);
    }

    [Route("exam-dashboard")]
    public async Task<IActionResult> ExamDashboard()
    {
        var instructorId = _userManager.GetUserId(User)!;
        
        var exams = await _context.Exams
            .Include(e => e.Course)
            .ThenInclude(c => c.Category)
            .Include(e => e.ExamAttempts)
            .Where(e => e.InstructorId == instructorId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        
        ViewBag.Exams = exams;
        return View();
    }

    [Route("exam-attempt-details/{attemptId:int}")]
    public async Task<IActionResult> ExamAttemptDetails(int attemptId)
    {
        var instructorId = _userManager.GetUserId(User)!;
        
        var attempt = await _context.ExamAttempts
            .Include(ea => ea.Student)
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.Course)
            .Include(ea => ea.Exam)
            .ThenInclude(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Options)
            .Include(ea => ea.ExamAnswers)
            .ThenInclude(ea => ea.ExamQuestion)
            .ThenInclude(eq => eq.Options)
            .FirstOrDefaultAsync(ea => ea.Id == attemptId && ea.Exam.InstructorId == instructorId);
        
        if (attempt == null)
        {
            TempData["Error"] = "Exam attempt not found or access denied.";
            return RedirectToAction("InternalAssessments");
        }
        
        return View(attempt);
    }
    
    [HttpGet]
    [Route("assign-exam-dates/{examId:int}")]
    public async Task<IActionResult> AssignExamDates(int examId)
    {
        var instructorId = _userManager.GetUserId(User)!;
        
        var exam = await _context.Exams
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == examId && e.InstructorId == instructorId);
        
        if (exam == null)
        {
            TempData["Error"] = "Exam not found or access denied.";
            return RedirectToAction("ExamDashboard");
        }
        
        // Get enrolled students for this course
        var enrolledStudents = await _context.Enrollments
            .Include(e => e.Student)
            .Where(e => e.CourseId == exam.CourseId)
            .Select(e => e.Student)
            .ToListAsync();
        
        // Get already assigned schedules
        var assignedSchedules = await _context.ExamSchedules
            .Include(es => es.Student)
            .Where(es => es.ExamId == examId)
            .ToListAsync();
        
        // Get unassigned students
        var assignedStudentIds = assignedSchedules.Select(es => es.StudentId).ToHashSet();
        var unassignedStudents = enrolledStudents.Where(s => !assignedStudentIds.Contains(s.Id)).ToList();
        
        ViewBag.Exam = exam;
        ViewBag.UnassignedStudents = unassignedStudents;
        ViewBag.AssignedSchedules = assignedSchedules;
        
        return View();
    }
    
    [HttpPost]
    [Route("assign-exam-date")]
    public async Task<IActionResult> AssignExamDate(int examId, string studentId, DateTime examDate, string session)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User)!;
            
            // Validate exam date is not in the past
            if (examDate.Date < DateTime.Today)
            {
                return Json(new { success = false, message = "Exam date cannot be in the past." });
            }
            
            // Validate session
            var validSessions = new[] { "Morning", "Afternoon", "Evening" };
            if (!validSessions.Contains(session))
            {
                return Json(new { success = false, message = "Invalid exam session." });
            }
            
            var success = await _examService.AssignExamDateToStudentAsync(instructorId, studentId, examId, examDate, session);
            
            if (success)
            {
                return Json(new { success = true, message = "Exam date assigned successfully." });
            }
            else
            {
                return Json(new { success = false, message = "Failed to assign exam date." });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while assigning exam date." });
        }
    }
    
    [HttpPost]
    [Route("bulk-assign-exam-dates")]
    public async Task<IActionResult> BulkAssignExamDates(int examId, DateTime examDate, string session)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User)!;
            
            // Validate exam date is not in the past
            if (examDate.Date < DateTime.Today)
            {
                return Json(new { success = false, message = "Exam date cannot be in the past." });
            }
            
            // Get all unassigned students
            var unassignedStudents = await _examService.GetUnassignedStudentsForExamAsync(instructorId, examId);
            
            int successCount = 0;
            foreach (var student in unassignedStudents)
            {
                var success = await _examService.AssignExamDateToStudentAsync(instructorId, student.Id, examId, examDate, session);
                if (success) successCount++;
            }
            
            return Json(new { 
                success = true, 
                message = $"Assigned exam dates to {successCount} out of {unassignedStudents.Count} students." 
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred during bulk assignment." });
        }
    }
    
    [HttpPost]
    [Route("remove-exam-assignment")]
    public async Task<IActionResult> RemoveExamAssignment(int scheduleId)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User)!;
            
            var schedule = await _context.ExamSchedules
                .Include(es => es.Exam)
                .FirstOrDefaultAsync(es => es.Id == scheduleId && es.Exam.InstructorId == instructorId);
            
            if (schedule == null)
            {
                return Json(new { success = false, message = "Schedule not found or access denied." });
            }
            
            // Check if student has already started the exam
            var hasAttempt = await _context.ExamAttempts
                .AnyAsync(ea => ea.ExamId == schedule.ExamId && ea.StudentId == schedule.StudentId);
            
            if (hasAttempt)
            {
                return Json(new { success = false, message = "Cannot remove assignment - student has already started the exam." });
            }
            
            _context.ExamSchedules.Remove(schedule);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, message = "Exam assignment removed successfully." });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while removing assignment." });
        }
    }
    
    [Route("debug-exam-attempts")]
    public async Task<IActionResult> DebugExamAttempts()
    {
        var instructorId = _userManager.GetUserId(User)!;
        
        var attempts = await _context.ExamAttempts
            .Include(ea => ea.Student)
            .Include(ea => ea.Exam)
            .Include(ea => ea.ExamAnswers)
            .ThenInclude(ea => ea.ExamQuestion)
            .Where(ea => ea.Exam.InstructorId == instructorId)
            .ToListAsync();
        
        var debugInfo = attempts.Select(a => new {
            AttemptId = a.Id,
            StudentName = $"{a.Student.FirstName} {a.Student.LastName}",
            ExamTitle = a.Exam.Title,
            PartAScore = a.PartAScore,
            PartACompleted = a.PartACompleted,
            IsCompleted = a.IsCompleted,
            AnswerCount = a.ExamAnswers.Count,
            PartAAnswers = a.ExamAnswers.Where(ea => ea.ExamQuestion.Part == ExamPart.PartA).Select(ea => new {
                QuestionId = ea.ExamQuestionId,
                Points = ea.Points,
                IsCorrect = ea.IsCorrect,
                SelectedOptionId = ea.SelectedOptionId
            }).ToList()
        }).ToList();
        
        return Json(debugInfo);
    }
    
    [Route("missed-exam-requests")]
    public async Task<IActionResult> MissedExamRequests()
    {
        var instructorId = _userManager.GetUserId(User)!;
        var pendingRequests = await _examService.GetPendingMissedExamRequestsAsync(instructorId);
        
        return View(pendingRequests);
    }
    
    [HttpPost]
    [Route("approve-missed-exam-request")]
    public async Task<IActionResult> ApproveMissedExamRequest(int requestId, DateTime newExamStartTime, DateTime newExamEndTime)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User)!;
            
            if (newExamStartTime <= DateTime.Now)
            {
                return Json(new { success = false, message = "New exam time must be in the future." });
            }
            
            if (newExamEndTime <= newExamStartTime)
            {
                return Json(new { success = false, message = "Exam end time must be after start time." });
            }
            
            var success = await _examService.ApproveMissedExamRequestAsync(requestId, instructorId, newExamStartTime, newExamEndTime);
            
            if (success)
            {
                return Json(new { success = true, message = "Missed exam request approved and new exam time assigned." });
            }
            else
            {
                return Json(new { success = false, message = "Failed to approve request." });
            }
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while approving the request." });
        }
    }
    
    [HttpPost]
    [Route("reject-missed-exam-request")]
    public async Task<IActionResult> RejectMissedExamRequest(int requestId, string response)
    {
        try
        {
            var instructorId = _userManager.GetUserId(User)!;
            
            if (string.IsNullOrWhiteSpace(response))
            {
                return Json(new { success = false, message = "Please provide a reason for rejection." });
            }
            
            var success = await _examService.RejectMissedExamRequestAsync(requestId, instructorId, response);
            
            if (success)
            {
                return Json(new { success = true, message = "Missed exam request rejected." });
            }
            else
            {
                return Json(new { success = false, message = "Failed to reject request." });
            }
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An error occurred while rejecting the request." });
        }
    }
}