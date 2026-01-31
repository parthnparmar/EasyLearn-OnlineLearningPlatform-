using EasyLearn.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Controllers;

[Route("fix")]
public class FixController : Controller
{
    private readonly ApplicationDbContext _context;

    public FixController(ApplicationDbContext context)
    {
        _context = context;
    }

    [Route("mcq-scores")]
    public async Task<IActionResult> FixMcqScores()
    {
        try
        {
            // Execute raw SQL to fix MCQ scores
            await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE ea
                SET PartAScore = (
                    SELECT COALESCE(SUM(
                        CASE 
                            WHEN ans.SelectedOptionId IS NOT NULL AND opt.IsCorrect = 1 THEN 2
                            WHEN ans.SelectedOptionId IS NOT NULL AND opt.IsCorrect = 0 THEN -1
                            ELSE 0
                        END
                    ), 0)
                    FROM ExamAnswers ans
                    INNER JOIN ExamQuestions eq ON ans.ExamQuestionId = eq.Id
                    LEFT JOIN ExamQuestionOptions opt ON ans.SelectedOptionId = opt.Id
                    WHERE ans.ExamAttemptId = ea.Id AND eq.Part = 1
                )
                FROM ExamAttempts ea
                INNER JOIN Exams e ON ea.ExamId = e.Id
                INNER JOIN Courses c ON e.CourseId = c.Id
                WHERE ea.PartACompleted = 1 
                AND c.Title LIKE '%Digital Marketing%'
                AND ea.PartAScore = 0
            ");
            
            // Ensure no negative scores
            await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE ea
                SET PartAScore = 0
                FROM ExamAttempts ea
                INNER JOIN Exams e ON ea.ExamId = e.Id
                INNER JOIN Courses c ON e.CourseId = c.Id
                WHERE c.Title LIKE '%Digital Marketing%' AND ea.PartAScore < 0
            ");
            
            // Get updated results
            var results = await _context.ExamAttempts
                .Include(ea => ea.Exam)
                .ThenInclude(e => e.Course)
                .Include(ea => ea.Student)
                .Where(ea => ea.Exam.Course.Title.Contains("Digital Marketing") && ea.PartACompleted)
                .Select(ea => new {
                    StudentName = ea.Student.FirstName + " " + ea.Student.LastName,
                    CourseTitle = ea.Exam.Course.Title,
                    PartAScore = ea.PartAScore,
                    AttemptId = ea.Id
                })
                .ToListAsync();
            
            return Json(new { 
                success = true, 
                message = "Digital Marketing MCQ scores updated successfully!",
                results = results
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }
}