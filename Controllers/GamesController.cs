using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EasyLearn.Data;
using EasyLearn.Models;

namespace EasyLearn.Controllers;

[Authorize]
public class GamesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GamesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var games = await _context.Games.Where(g => g.IsActive).ToListAsync();
        return View(games);
    }

    public async Task<IActionResult> Leaderboard(int gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound();

        var scores = await _context.GameScores
            .Include(s => s.User)
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.TimeTaken)
            .Take(10)
            .ToListAsync();

        ViewBag.GameName = game.Name;
        return View(scores);
    }

    [HttpPost]
    public async Task<IActionResult> SaveScore(int gameId, int score, int level, int seconds)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var gameScore = new GameScore
        {
            GameId = gameId,
            UserId = user.Id,
            Score = score,
            Level = level,
            TimeTaken = TimeSpan.FromSeconds(seconds)
        };

        _context.GameScores.Add(gameScore);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    public IActionResult MemoryCardMatching() => View();
    public IActionResult PatternMemory() => View();
    public IActionResult NumberRecall() => View();
    public IActionResult ImageMemory() => View();
    public IActionResult NumberGuessing() => View();
    public IActionResult ArithmeticChallenge() => View();
    public IActionResult FastCalculation() => View();
    public IActionResult SequenceCompletion() => View();
}