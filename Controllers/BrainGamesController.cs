using EasyLearn.Models;
using EasyLearn.Models.ViewModels;
using EasyLearn.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyLearn.Controllers;

[Route("brain-games")]
public class BrainGamesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPuzzleService _puzzleService;

    public BrainGamesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPuzzleService puzzleService)
    {
        _context = context;
        _userManager = userManager;
        _puzzleService = puzzleService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            
            // Ensure we have sample games
            await EnsureSampleGamesExist();
            
            var availableGames = await _puzzleService.GetAvailableGamesAsync();
            
            // For anonymous users, show empty stats
            if (userId == null)
            {
                var viewModelAnonymous = new PuzzleDashboardViewModel
                {
                    AvailableGames = availableGames,
                    RecentAttempts = new List<PuzzleAttempt>(),
                    UserStats = new PuzzleStats
                    {
                        TotalGamesPlayed = 0,
                        TotalGamesCompleted = 0,
                        AverageScore = 0,
                        TotalTimePlayed = 0
                    },
                    TopPlayers = new List<PuzzleLeaderboardEntry>()
                };
                return View(viewModelAnonymous);
            }
            
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
        catch (Exception ex)
        {
            // Return a simple view with error message for debugging
            ViewBag.Error = ex.Message;
            return View(new PuzzleDashboardViewModel
            {
                AvailableGames = new List<PuzzleGame>(),
                RecentAttempts = new List<PuzzleAttempt>(),
                UserStats = new PuzzleStats(),
                TopPlayers = new List<PuzzleLeaderboardEntry>()
            });
        }
    }

    [HttpGet("sudoku")]
    public async Task<IActionResult> Sudoku()
    {
        await EnsureSampleGamesExist();
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.Sudoku && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View(games);
    }

    [HttpGet("memory")]
    public async Task<IActionResult> Memory()
    {
        await EnsureSampleGamesExist();
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.PatternMatching && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View(games);
    }

    [HttpGet("math")]
    public async Task<IActionResult> MathGames()
    {
        await EnsureSampleGamesExist();
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.NumberSequence && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View("MathGames", games);
    }

    [HttpGet("word")]
    public async Task<IActionResult> Word()
    {
        await EnsureSampleGamesExist();
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.WordPuzzle && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View(games);
    }

    [HttpGet("logic")]
    public async Task<IActionResult> Logic()
    {
        await EnsureSampleGamesExist();
        var games = await _context.PuzzleGames
            .Where(pg => pg.Type == PuzzleType.LogicPuzzle && pg.IsActive)
            .OrderBy(pg => pg.Difficulty)
            .ToListAsync();

        return View(games);
    }

    [HttpGet("play/{gameId}")]
    public async Task<IActionResult> Play(int gameId)
    {
        var userId = _userManager.GetUserId(User);
        
        // If user is not logged in, redirect to login
        if (userId == null)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Play", new { gameId }) });
        }
        
        var game = await _puzzleService.GetGameAsync(gameId);
        
        if (game == null)
        {
            TempData["Error"] = "Game not found";
            return RedirectToAction("Index");
        }

        // Check for existing active attempt
        var currentAttempt = await _puzzleService.GetActiveAttemptAsync(userId, gameId);
        
        // If no active attempt, start a new one
        if (currentAttempt == null)
        {
            currentAttempt = await _puzzleService.StartGameAsync(userId, gameId);
            if (currentAttempt == null)
            {
                TempData["Error"] = "Unable to start game";
                return RedirectToAction("Index");
            }
        }

        var timeRemaining = 0;
        if (game.TimeLimit > 0)
        {
            var elapsed = (int)(DateTime.UtcNow - currentAttempt.StartedAt).TotalSeconds;
            timeRemaining = Math.Max(0, game.TimeLimit - elapsed);
            
            if (timeRemaining == 0)
            {
                await _puzzleService.CompleteGameAsync(currentAttempt.Id);
                return RedirectToAction("Result", new { attemptId = currentAttempt.Id });
            }
        }

        var viewModel = new PuzzlePlayViewModel
        {
            Game = game,
            Attempt = currentAttempt,
            GameState = currentAttempt.CurrentState,
            TimeRemaining = timeRemaining,
            CanUseHints = currentAttempt.HintsUsed < 3,
            MaxHints = 3
        };

        return View($"{game.Type}Play", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> MakeMove(int attemptId, [FromBody] JsonElement moveData)
    {
        var userId = _userManager.GetUserId(User)!;
        var attempt = await _context.PuzzleAttempts
            .Include(pa => pa.PuzzleGame)
            .FirstOrDefaultAsync(pa => pa.Id == attemptId && pa.UserId == userId);

        if (attempt == null || attempt.Status != GameStatus.InProgress)
        {
            return Json(new { success = false, message = "Invalid game session" });
        }

        var success = await _puzzleService.MakeMoveAsync(attemptId, moveData.GetRawText());
        
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
    [ValidateAntiForgeryToken]
    [Authorize]
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

    [Authorize]
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

        var performanceMessage = attempt.Status switch
        {
            GameStatus.Completed when attempt.Score >= attempt.PuzzleGame.MaxScore * 0.9 => "🎉 Excellent! Outstanding performance!",
            GameStatus.Completed when attempt.Score >= attempt.PuzzleGame.MaxScore * 0.7 => "👏 Great job! Well done!",
            GameStatus.Completed when attempt.Score >= attempt.PuzzleGame.MaxScore * 0.5 => "👍 Good work! Keep improving!",
            GameStatus.Completed => "💪 Nice try! Practice makes perfect!",
            GameStatus.AutoSolved => "🤖 Puzzle auto-solved. Try again for a score!",
            _ => "❌ Game abandoned"
        };

        var viewModel = new PuzzleResultViewModel
        {
            Attempt = attempt,
            Game = attempt.PuzzleGame,
            PerformanceMessage = performanceMessage
        };

        return View(viewModel);
    }

    private async Task EnsureSampleGamesExist()
    {
        // Check if we already have games
        if (await _context.PuzzleGames.AnyAsync())
        {
            return;
        }

        var sampleGames = new List<PuzzleGame>
        {
            // Memory Games
            new PuzzleGame
            {
                Title = "Memory Match - Colors (4 Cards)",
                Description = "Simple color matching for beginners",
                Type = PuzzleType.PatternMatching,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"cards\":[\"🔴\",\"🔵\",\"🔴\",\"🔵\"],\"flipped\":[false,false,false,false],\"matched\":[false,false,false,false]}",
                Solution = "{\"matched\":[true,true,true,true]}",
                MaxScore = 60,
                TimeLimit = 60,
                IsActive = true
            },
            new PuzzleGame
            {
                Title = "Memory Match - Animals (8 Cards)",
                Description = "Match pairs of animal cards",
                Type = PuzzleType.PatternMatching,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"cards\":[\"🐶\",\"🐱\",\"🐭\",\"🐹\",\"🐶\",\"🐱\",\"🐭\",\"🐹\"],\"flipped\":[false,false,false,false,false,false,false,false],\"matched\":[false,false,false,false,false,false,false,false]}",
                Solution = "{\"matched\":[true,true,true,true,true,true,true,true]}",
                MaxScore = 80,
                TimeLimit = 120,
                IsActive = true
            },
            
            // Math Games
            new PuzzleGame
            {
                Title = "Counting by 1s",
                Description = "Simple counting sequence",
                Type = PuzzleType.NumberSequence,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"sequence\":[1,2,3,4],\"answer\":5,\"pattern\":\"arithmetic\"}",
                Solution = "{\"sequence\":[1,2,3,4,5],\"answer\":5,\"pattern\":\"arithmetic\"}",
                MaxScore = 40,
                TimeLimit = 120,
                IsActive = true
            },
            new PuzzleGame
            {
                Title = "Even Numbers",
                Description = "Find the pattern in even numbers",
                Type = PuzzleType.NumberSequence,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"sequence\":[2,4,6,8],\"answer\":10,\"pattern\":\"arithmetic\"}",
                Solution = "{\"sequence\":[2,4,6,8,10],\"answer\":10,\"pattern\":\"arithmetic\"}",
                MaxScore = 50,
                TimeLimit = 150,
                IsActive = true
            },
            
            // Word Games
            new PuzzleGame
            {
                Title = "Word Scramble - 3 Letter Words",
                Description = "Simple 3-letter words",
                Type = PuzzleType.WordPuzzle,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"scrambled\":\"TAC\",\"answer\":\"CAT\",\"category\":\"animals\"}",
                Solution = "{\"scrambled\":\"TAC\",\"answer\":\"CAT\",\"solved\":true}",
                MaxScore = 40,
                TimeLimit = 120,
                IsActive = true
            },
            
            // Logic Games
            new PuzzleGame
            {
                Title = "Simple Logic - 2 Variables",
                Description = "Basic logic with 2 variables",
                Type = PuzzleType.LogicPuzzle,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"clues\":[\"A is greater than B\",\"B is not 1\"],\"variables\":[\"A\",\"B\"],\"values\":[1,2]}",
                Solution = "{\"A\":2,\"B\":1}",
                MaxScore = 60,
                TimeLimit = 240,
                IsActive = true
            },
            
            // Sudoku Games
            new PuzzleGame
            {
                Title = "Beginner Sudoku #1",
                Description = "Perfect for absolute beginners - very easy introduction",
                Type = PuzzleType.Sudoku,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"Grid\":[[5,3,0,0,7,0,0,0,0],[6,0,0,1,9,5,0,0,0],[0,9,8,0,0,0,0,6,0],[8,0,0,0,6,0,0,0,3],[4,0,0,8,0,3,0,0,1],[7,0,0,0,2,0,0,0,6],[0,6,0,0,0,0,2,8,0],[0,0,0,4,1,9,0,0,5],[0,0,0,0,8,0,0,7,9]],\"IsFixed\":[[true,true,false,false,true,false,false,false,false],[true,false,false,true,true,true,false,false,false],[false,true,true,false,false,false,false,true,false],[true,false,false,false,true,false,false,false,true],[true,false,false,true,false,true,false,false,true],[true,false,false,false,true,false,false,false,true],[false,true,false,false,false,false,true,true,false],[false,false,false,true,true,true,false,false,true],[false,false,false,false,true,false,false,true,true]]}",
                Solution = "{\"Grid\":[[5,3,4,6,7,8,9,1,2],[6,7,2,1,9,5,3,4,8],[1,9,8,3,4,2,5,6,7],[8,5,9,7,6,1,4,2,3],[4,2,6,8,5,3,7,9,1],[7,1,3,9,2,4,8,5,6],[9,6,1,5,3,7,2,8,4],[2,8,7,4,1,9,6,3,5],[3,4,5,2,8,6,1,7,9]]}",
                MaxScore = 80,
                TimeLimit = 0,
                IsActive = true
            }
        };

        _context.PuzzleGames.AddRange(sampleGames);
        await _context.SaveChangesAsync();
    }
}