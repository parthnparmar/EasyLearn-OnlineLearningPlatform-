using EasyLearn.Models;
using EasyLearn.Models.ViewModels;
using EasyLearn.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyLearn.Controllers;

[Authorize]
[Route("puzzles")]
public class PuzzleController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPuzzleService _puzzleService;

    public PuzzleController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPuzzleService puzzleService)
    {
        _context = context;
        _userManager = userManager;
        _puzzleService = puzzleService;
    }

    [Route("")]
    [Route("dashboard")]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        
        var availableGames = await _puzzleService.GetAvailableGamesAsync();
        var recentAttempts = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .Where(pa => pa.UserId == userId)
            .OrderByDescending(pa => pa.StartedAt)
            .Take(5)
            .ToListAsync();

        var userStats = await _puzzleService.GetUserStatsAsync(userId);
        
        var topPlayers = await _context.PuzzleLeaderboards
            .Include(pl => pl.User)
            .GroupBy(pl => pl.UserId)
            .Select(g => new PuzzleLeaderboardEntry
            {
                UserName = $"{g.First().User.FirstName} {g.First().User.LastName}".Trim(),
                Score = g.Sum(pl => pl.BestScore),
                Attempts = g.Sum(pl => pl.TotalAttempts),
                LastPlayed = g.Max(pl => pl.LastPlayedAt)
            })
            .OrderByDescending(p => p.Score)
            .Take(5)
            .ToListAsync();

        var viewModel = new PuzzleDashboardViewModel
        {
            AvailableGames = availableGames,
            RecentAttempts = recentAttempts,
            UserStats = userStats,
            TopPlayers = topPlayers
        };

        return View(viewModel);
    }

    [Route("game/{gameId:int}")]
    public async Task<IActionResult> GameDetails(int gameId)
    {
        var userId = _userManager.GetUserId(User)!;
        var game = await _puzzleService.GetGameAsync(gameId);
        
        if (game == null)
        {
            TempData["Error"] = "Game not found";
            return RedirectToAction("Index");
        }

        var currentAttempt = await _puzzleService.GetActiveAttemptAsync(userId, gameId);
        var leaderboard = await _context.PuzzleLeaderboards
            .FirstOrDefaultAsync(pl => pl.UserId == userId && pl.PuzzleGameId == gameId);

        var viewModel = new PuzzleGameViewModel
        {
            Game = game,
            CurrentAttempt = currentAttempt,
            HasActiveAttempt = currentAttempt != null,
            BestScore = leaderboard?.BestScore ?? 0,
            BestTime = leaderboard?.BestTimeSeconds ?? 0,
            TotalAttempts = leaderboard?.TotalAttempts ?? 0
        };

        return View(viewModel);
    }

    [HttpPost]
    [Route("start/{gameId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartGame(int gameId)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _puzzleService.StartGameAsync(userId, gameId);
        
        if (attempt == null)
        {
            TempData["Error"] = "Unable to start game";
            return RedirectToAction("GameDetails", new { gameId });
        }

        return RedirectToAction("Play", new { attemptId = attempt.Id });
    }

    [Route("play/{attemptId:int}")]
    public async Task<IActionResult> Play(int attemptId)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.Id == attemptId && pa.UserId == userId);

        if (attempt == null || attempt.Status != GameStatus.InProgress)
        {
            TempData["Error"] = "Game session not found or already completed";
            return RedirectToAction("Index");
        }

        var timeRemaining = 0;
        if (attempt.PuzzleGame.TimeLimit > 0)
        {
            var elapsed = (int)(DateTime.UtcNow - attempt.StartedAt).TotalSeconds;
            timeRemaining = Math.Max(0, attempt.PuzzleGame.TimeLimit - elapsed);
            
            if (timeRemaining == 0)
            {
                // Time's up - complete the game
                await _puzzleService.CompleteGameAsync(attemptId);
                return RedirectToAction("Result", new { attemptId });
            }
        }

        var viewModel = new PuzzlePlayViewModel
        {
            Game = attempt.PuzzleGame,
            Attempt = attempt,
            GameState = attempt.CurrentState,
            TimeRemaining = timeRemaining,
            CanUseHints = attempt.HintsUsed < 3,
            MaxHints = 3
        };

        return View($"{attempt.PuzzleGame.Type}Play", viewModel);
    }

    [HttpPost]
    [Route("move/{attemptId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeMove(int attemptId, [FromBody] string moveData)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _context.PuzzleAttempts
            .FirstOrDefaultAsync(pa => pa.Id == attemptId && pa.UserId == userId);

        if (attempt == null || attempt.Status != GameStatus.InProgress)
        {
            return Json(new { success = false, message = "Invalid game session" });
        }

        var success = await _puzzleService.MakeMoveAsync(attemptId, moveData);
        
        if (!success)
        {
            return Json(new { success = false, message = "Invalid move" });
        }

        // Check if game is completed
        var updatedAttempt = await _context.PuzzleAttempts.FindAsync(attemptId);
        if (updatedAttempt?.Status == GameStatus.Completed)
        {
            return Json(new { 
                success = true, 
                completed = true, 
                redirectUrl = Url.Action("Result", new { attemptId }) 
            });
        }

        return Json(new { success = true, completed = false });
    }

    [HttpPost]
    [Route("hint/{attemptId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetHint(int attemptId)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _context.PuzzleAttempts
            .FirstOrDefaultAsync(pa => pa.Id == attemptId && pa.UserId == userId);

        if (attempt == null || attempt.Status != GameStatus.InProgress)
        {
            return Json(new { success = false, message = "Invalid game session" });
        }

        if (attempt.HintsUsed >= 3)
        {
            return Json(new { success = false, message = "No more hints available" });
        }

        var hint = await _puzzleService.GetHintAsync(attemptId);
        return Json(new { success = true, hint, hintsRemaining = 3 - attempt.HintsUsed - 1 });
    }

    [HttpPost]
    [Route("autosolve/{attemptId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSolve(int attemptId)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _context.PuzzleAttempts
            .FirstOrDefaultAsync(pa => pa.Id == attemptId && pa.UserId == userId);

        if (attempt == null || attempt.Status != GameStatus.InProgress)
        {
            return Json(new { success = false, message = "Invalid game session" });
        }

        var solvedAttempt = await _puzzleService.AutoSolveAsync(attemptId);
        if (solvedAttempt == null)
        {
            return Json(new { success = false, message = "Unable to auto-solve" });
        }

        return Json(new { 
            success = true, 
            redirectUrl = Url.Action("Result", new { attemptId }) 
        });
    }

    [Route("result/{attemptId:int}")]
    public async Task<IActionResult> Result(int attemptId)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.Id == attemptId && pa.UserId == userId);

        if (attempt == null)
        {
            TempData["Error"] = "Game result not found";
            return RedirectToAction("Index");
        }

        var leaderboard = await _context.PuzzleLeaderboards
            .Where(pl => pl.PuzzleGameId == attempt.PuzzleGameId)
            .OrderByDescending(pl => pl.BestScore)
            .ToListAsync();

        var userRank = leaderboard.FindIndex(pl => pl.UserId == userId) + 1;
        var isNewBestScore = false;
        var isNewBestTime = false;

        var userLeaderboard = leaderboard.FirstOrDefault(pl => pl.UserId == userId);
        if (userLeaderboard != null)
        {
            isNewBestScore = attempt.Score == userLeaderboard.BestScore && userLeaderboard.TotalAttempts == 1;
            isNewBestTime = attempt.TimeTakenSeconds == userLeaderboard.BestTimeSeconds && userLeaderboard.CompletedAttempts == 1;
        }

        var performanceMessage = attempt.Status switch
        {
            GameStatus.Completed when attempt.Score >= attempt.PuzzleGame.MaxScore * 0.9 => "Excellent! Outstanding performance!",
            GameStatus.Completed when attempt.Score >= attempt.PuzzleGame.MaxScore * 0.7 => "Great job! Well done!",
            GameStatus.Completed when attempt.Score >= attempt.PuzzleGame.MaxScore * 0.5 => "Good work! Keep improving!",
            GameStatus.Completed => "Nice try! Practice makes perfect!",
            GameStatus.AutoSolved => "Puzzle auto-solved. Try again for a score!",
            _ => "Game abandoned"
        };

        var viewModel = new PuzzleResultViewModel
        {
            Attempt = attempt,
            Game = attempt.PuzzleGame,
            IsNewBestScore = isNewBestScore,
            IsNewBestTime = isNewBestTime,
            Rank = userRank,
            PerformanceMessage = performanceMessage
        };

        return View(viewModel);
    }

    [Route("leaderboard/{gameId:int}")]
    public async Task<IActionResult> Leaderboard(int gameId, string type = "score")
    {
        var game = await _puzzleService.GetGameAsync(gameId);
        if (game == null)
        {
            TempData["Error"] = "Game not found";
            return RedirectToAction("Index");
        }

        var userId = _userManager.GetUserId(User)!;
        var topScores = await _puzzleService.GetLeaderboardAsync(gameId, "score");
        var topTimes = await _puzzleService.GetLeaderboardAsync(gameId, "time");
        
        var userStats = await _context.PuzzleLeaderboards
            .FirstOrDefaultAsync(pl => pl.UserId == userId && pl.PuzzleGameId == gameId);

        PuzzleLeaderboardEntry? userEntry = null;
        if (userStats != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            userEntry = new PuzzleLeaderboardEntry
            {
                UserName = $"{user?.FirstName} {user?.LastName}".Trim(),
                Score = userStats.BestScore,
                TimeSeconds = userStats.BestTimeSeconds,
                Attempts = userStats.TotalAttempts,
                LastPlayed = userStats.LastPlayedAt,
                IsCurrentUser = true
            };
        }

        var viewModel = new PuzzleLeaderboardViewModel
        {
            Game = game,
            TopScores = topScores,
            TopTimes = topTimes,
            UserStats = userEntry
        };

        return View(viewModel);
    }

    [Route("sudoku")]
    public async Task<IActionResult> SudokuGames()
    {
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.Sudoku && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View(games);
    }

    [Route("wordpuzzle")]
    public async Task<IActionResult> WordPuzzleGames()
    {
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.WordPuzzle && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View(games);
    }

    [Route("logic")]
    public async Task<IActionResult> LogicGames()
    {
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.LogicPuzzle && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View(games);
    }

    [HttpPost]
    [Route("abandon/{attemptId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AbandonGame(int attemptId)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _context.PuzzleAttempts
            .FirstOrDefaultAsync(pa => pa.Id == attemptId && pa.UserId == userId);

        if (attempt != null && attempt.Status == GameStatus.InProgress)
        {
            attempt.Status = GameStatus.Abandoned;
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.TimeTakenSeconds = (int)(DateTime.UtcNow - attempt.StartedAt).TotalSeconds;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }

    [Route("stats")]
    public async Task<IActionResult> UserStats()
    {
        var userId = _userManager.GetUserId(User)!;
        var stats = await _puzzleService.GetUserStatsAsync(userId);
        
        var detailedStats = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .Where(pa => pa.UserId == userId)
            .GroupBy(pa => pa.PuzzleGame.Type)
            .Select(g => new
            {
                Type = g.Key,
                TotalPlayed = g.Count(),
                TotalCompleted = g.Count(pa => pa.Status == GameStatus.Completed),
                AverageScore = g.Where(pa => pa.Status == GameStatus.Completed).Average(pa => (double?)pa.Score) ?? 0,
                BestScore = g.Max(pa => (int?)pa.Score) ?? 0,
                TotalTime = g.Sum(pa => pa.TimeTakenSeconds)
            })
            .ToListAsync();

        ViewBag.DetailedStats = detailedStats;
        return View(stats);
    }
}