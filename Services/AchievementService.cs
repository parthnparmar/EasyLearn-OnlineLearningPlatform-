using EasyLearn.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Services;

public interface IAchievementService
{
    Task CheckAndAwardAchievementsAsync(string studentId, int? courseId = null, int? examAttemptId = null);
    Task<List<Achievement>> GetStudentAchievementsAsync(string studentId);
    Task<List<StudentActivityFeed>> GetRecentActivityFeedAsync(int count = 10);
}

public class AchievementService : IAchievementService
{
    private readonly ApplicationDbContext _context;

    public AchievementService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CheckAndAwardAchievementsAsync(string studentId, int? courseId = null, int? examAttemptId = null)
    {
        var newAchievements = new List<Achievement>();

        // Course completion achievement
        if (courseId.HasValue)
        {
            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId && e.CompletedAt != null);

            if (enrollment != null)
            {
                var hasAchievement = await _context.Achievements
                    .AnyAsync(a => a.StudentId == studentId && a.CourseId == courseId && a.BadgeTitle == "Course Completed");

                if (!hasAchievement)
                {
                    newAchievements.Add(new Achievement
                    {
                        StudentId = studentId,
                        BadgeTitle = "Course Completed",
                        BadgeIcon = "fas fa-graduation-cap",
                        Description = $"Completed {enrollment.Course.Title}",
                        CourseId = courseId
                    });
                }
            }
        }

        // Exam score achievements
        if (examAttemptId.HasValue)
        {
            var examAttempt = await _context.ExamAttempts
                .Include(ea => ea.Exam)
                .ThenInclude(e => e.Course)
                .FirstOrDefaultAsync(ea => ea.Id == examAttemptId && ea.StudentId == studentId && ea.IsCompleted);

            if (examAttempt != null)
            {
                var score = examAttempt.TotalScore;
                
                if (score >= 90)
                {
                    var hasTopScorer = await _context.Achievements
                        .AnyAsync(a => a.StudentId == studentId && a.ExamAttemptId == examAttemptId && a.BadgeTitle == "Top Scorer");

                    if (!hasTopScorer)
                    {
                        newAchievements.Add(new Achievement
                        {
                            StudentId = studentId,
                            BadgeTitle = "Top Scorer",
                            BadgeIcon = "fas fa-trophy",
                            Description = $"Scored {score}% in {examAttempt.Exam.Course.Title}",
                            ExamAttemptId = examAttemptId,
                            CourseId = examAttempt.Exam.CourseId
                        });
                    }
                }
                else if (score >= 75)
                {
                    var hasExcellent = await _context.Achievements
                        .AnyAsync(a => a.StudentId == studentId && a.ExamAttemptId == examAttemptId && a.BadgeTitle == "Excellent Performance");

                    if (!hasExcellent)
                    {
                        newAchievements.Add(new Achievement
                        {
                            StudentId = studentId,
                            BadgeTitle = "Excellent Performance",
                            BadgeIcon = "fas fa-medal",
                            Description = $"Scored {score}% in {examAttempt.Exam.Course.Title}",
                            ExamAttemptId = examAttemptId,
                            CourseId = examAttempt.Exam.CourseId
                        });
                    }
                }
            }
        }

        // Multiple course completion achievements
        var completedCourses = await _context.Enrollments
            .Where(e => e.StudentId == studentId && e.CompletedAt != null)
            .CountAsync();

        if (completedCourses >= 5)
        {
            var hasLearner = await _context.Achievements
                .AnyAsync(a => a.StudentId == studentId && a.BadgeTitle == "Dedicated Learner");

            if (!hasLearner)
            {
                newAchievements.Add(new Achievement
                {
                    StudentId = studentId,
                    BadgeTitle = "Dedicated Learner",
                    BadgeIcon = "fas fa-book-open",
                    Description = "Completed 5 or more courses"
                });
            }
        }

        // Save achievements and create activity feed
        if (newAchievements.Any())
        {
            _context.Achievements.AddRange(newAchievements);
            await _context.SaveChangesAsync();

            var student = await _context.Users.FindAsync(studentId);
            var studentName = $"{student?.FirstName} {student?.LastName}".Trim();

            foreach (var achievement in newAchievements)
            {
                var activityFeed = new StudentActivityFeed
                {
                    StudentId = studentId,
                    ActivityText = $"{studentName} earned \"{achievement.BadgeTitle}\" badge",
                    ActivityIcon = "🎉",
                    AchievementId = achievement.Id
                };

                _context.StudentActivityFeeds.Add(activityFeed);
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Achievement>> GetStudentAchievementsAsync(string studentId)
    {
        return await _context.Achievements
            .Include(a => a.Course)
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.EarnedAt)
            .ToListAsync();
    }

    public async Task<List<StudentActivityFeed>> GetRecentActivityFeedAsync(int count = 10)
    {
        return await _context.StudentActivityFeeds
            .Include(saf => saf.Student)
            .Include(saf => saf.Achievement)
            .OrderByDescending(saf => saf.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}