using EasyLearn.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EasyLearn.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Create roles
        string[] roles = { "Admin", "Instructor", "Student" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Create admin user
        var adminEmail = "admin@easylearn.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Create sample instructor
        var instructorEmail = "instructor@easylearn.com";
        var instructorUser = await userManager.FindByEmailAsync(instructorEmail);
        
        if (instructorUser == null)
        {
            instructorUser = new ApplicationUser
            {
                UserName = instructorEmail,
                Email = instructorEmail,
                FirstName = "John",
                LastName = "Instructor",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(instructorUser, "Instructor123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(instructorUser, "Instructor");
            }
        }

        // Create sample student
        var studentEmail = "student@easylearn.com";
        var studentUser = await userManager.FindByEmailAsync(studentEmail);
        
        if (studentUser == null)
        {
            studentUser = new ApplicationUser
            {
                UserName = studentEmail,
                Email = studentEmail,
                FirstName = "Jane",
                LastName = "Student",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(studentUser, "Student123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(studentUser, "Student");
            }
        }

        await context.SaveChangesAsync();

        // Create sample courses and quizzes if they don't exist
        if (!await context.Courses.AnyAsync())
        {
            await CreateSampleCoursesAsync(context, instructorUser!);
        }

        // Create sample achievements for testing
        await CreateSampleAchievementsAsync(context, studentUser!);
        
        // Create sample puzzle games
        await CreateSamplePuzzleGamesAsync(context);
        
        // Create brain games
        await CreateBrainGamesAsync(context);
    }

    private static async Task CreateSampleCoursesAsync(ApplicationDbContext context, ApplicationUser instructor)
    {
        // Get or create categories
        var programmingCategory = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Programming");
        if (programmingCategory == null)
        {
            programmingCategory = new Category { Name = "Programming", Description = "Software development courses" };
            context.Categories.Add(programmingCategory);
            await context.SaveChangesAsync();
        }

        // Create sample course
        var course = new Course
        {
            Title = "Introduction to C# Programming",
            Description = "Learn the fundamentals of C# programming language",
            InstructorId = instructor.Id,
            CategoryId = programmingCategory.Id,
            Price = 0,
            IsApproved = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        // Create sample lessons
        var lessons = new List<Lesson>
        {
            new Lesson
            {
                CourseId = course.Id,
                Title = "Getting Started with C#",
                Description = "Introduction to C# and .NET",
                VideoUrl = "https://www.youtube.com/watch?v=GhQdlIFylQ8",
                OrderIndex = 1,
                IsActive = true
            },
            new Lesson
            {
                CourseId = course.Id,
                Title = "Variables and Data Types",
                Description = "Understanding C# variables and data types",
                VideoUrl = "https://www.youtube.com/watch?v=wxznTygnRfQ",
                OrderIndex = 2,
                IsActive = true
            }
        };
        
        context.Lessons.AddRange(lessons);
        await context.SaveChangesAsync();

        // Create sample quiz
        var quiz = new Quiz
        {
            CourseId = course.Id,
            Title = "C# Basics Quiz",
            Description = "Test your knowledge of C# fundamentals",
            TimeLimit = 15, // 15 minutes
            PassingScore = 70,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        // Create sample questions
        var questions = new List<Question>
        {
            new Question
            {
                QuizId = quiz.Id,
                Text = "What is C#?",
                Type = QuestionType.MultipleChoice,
                Points = 10,
                OrderIndex = 1,
                Answers = new List<Answer>
                {
                    new Answer { Text = "A programming language developed by Microsoft", IsCorrect = true, OrderIndex = 1 },
                    new Answer { Text = "A database management system", IsCorrect = false, OrderIndex = 2 },
                    new Answer { Text = "A web browser", IsCorrect = false, OrderIndex = 3 },
                    new Answer { Text = "An operating system", IsCorrect = false, OrderIndex = 4 }
                }
            },
            new Question
            {
                QuizId = quiz.Id,
                Text = "C# is a case-sensitive language.",
                Type = QuestionType.TrueFalse,
                Points = 10,
                OrderIndex = 2,
                Answers = new List<Answer>
                {
                    new Answer { Text = "True", IsCorrect = true, OrderIndex = 1 },
                    new Answer { Text = "False", IsCorrect = false, OrderIndex = 2 }
                }
            },
            new Question
            {
                QuizId = quiz.Id,
                Text = "Which keyword is used to declare a variable in C#?",
                Type = QuestionType.MultipleChoice,
                Points = 10,
                OrderIndex = 3,
                Answers = new List<Answer>
                {
                    new Answer { Text = "var", IsCorrect = true, OrderIndex = 1 },
                    new Answer { Text = "variable", IsCorrect = false, OrderIndex = 2 },
                    new Answer { Text = "declare", IsCorrect = false, OrderIndex = 3 },
                    new Answer { Text = "def", IsCorrect = false, OrderIndex = 4 }
                }
            },
            new Question
            {
                QuizId = quiz.Id,
                Text = "Explain the difference between 'int' and 'string' data types in C#.",
                Type = QuestionType.ShortAnswer,
                Points = 20,
                OrderIndex = 4,
                Answers = new List<Answer>
                {
                    new Answer { Text = "int is used for whole numbers, string is used for text", IsCorrect = true, OrderIndex = 1 }
                }
            },
            new Question
            {
                QuizId = quiz.Id,
                Text = "C# supports multiple inheritance.",
                Type = QuestionType.TrueFalse,
                Points = 10,
                OrderIndex = 5,
                Answers = new List<Answer>
                {
                    new Answer { Text = "True", IsCorrect = false, OrderIndex = 1 },
                    new Answer { Text = "False", IsCorrect = true, OrderIndex = 2 }
                }
            }
        };
        
        context.Questions.AddRange(questions);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSampleAchievementsAsync(ApplicationDbContext context, ApplicationUser student)
    {
        // Only create sample achievements if none exist for this student
        if (await context.Achievements.AnyAsync(a => a.StudentId == student.Id))
            return;

        var course = await context.Courses.FirstOrDefaultAsync();
        if (course == null) return;

        var achievements = new List<Achievement>
        {
            new Achievement
            {
                StudentId = student.Id,
                BadgeTitle = "First Steps",
                BadgeIcon = "fas fa-baby",
                Description = "Started your learning journey",
                EarnedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Achievement
            {
                StudentId = student.Id,
                BadgeTitle = "Course Completed",
                BadgeIcon = "fas fa-graduation-cap",
                Description = $"Completed {course.Title}",
                CourseId = course.Id,
                EarnedAt = DateTime.UtcNow.AddDays(-3)
            },
            new Achievement
            {
                StudentId = student.Id,
                BadgeTitle = "Top Scorer",
                BadgeIcon = "fas fa-trophy",
                Description = "Scored 95% in an exam",
                EarnedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        context.Achievements.AddRange(achievements);
        await context.SaveChangesAsync();

        // Create activity feed entries
        var studentName = $"{student.FirstName} {student.LastName}";
        var activityFeeds = new List<StudentActivityFeed>();

        foreach (var achievement in achievements)
        {
            activityFeeds.Add(new StudentActivityFeed
            {
                StudentId = student.Id,
                ActivityText = $"{studentName} earned \"{achievement.BadgeTitle}\" badge",
                ActivityIcon = "🎉",
                AchievementId = achievement.Id,
                CreatedAt = achievement.EarnedAt
            });
        }

        // Add some other students' activities for demo
        var otherStudents = new[] { "Alice Johnson", "Bob Smith", "Carol Davis" };
        var otherAchievements = new[] { "Course Completed", "Excellent Performance", "Dedicated Learner" };
        
        for (int i = 0; i < 3; i++)
        {
            activityFeeds.Add(new StudentActivityFeed
            {
                StudentId = student.Id, // Using same student ID for demo purposes
                ActivityText = $"{otherStudents[i]} earned \"{otherAchievements[i]}\" badge",
                ActivityIcon = "🎉",
                CreatedAt = DateTime.UtcNow.AddHours(-i * 2)
            });
        }

        context.StudentActivityFeeds.AddRange(activityFeeds);
        await context.SaveChangesAsync();
    }

    private static async Task CreateSamplePuzzleGamesAsync(ApplicationDbContext context)
    {
        // Only create sample puzzle games if none exist
        if (await context.PuzzleGames.AnyAsync())
            return;

        var puzzleGames = new List<PuzzleGame>
        {
            // Sudoku Games
            new PuzzleGame
            {
                Title = "Easy Sudoku #1",
                Description = "Perfect for beginners - a gentle introduction to Sudoku",
                Type = PuzzleType.Sudoku,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"Grid\":[[5,3,0,0,7,0,0,0,0],[6,0,0,1,9,5,0,0,0],[0,9,8,0,0,0,0,6,0],[8,0,0,0,6,0,0,0,3],[4,0,0,8,0,3,0,0,1],[7,0,0,0,2,0,0,0,6],[0,6,0,0,0,0,2,8,0],[0,0,0,4,1,9,0,0,5],[0,0,0,0,8,0,0,7,9]],\"IsFixed\":[[true,true,false,false,true,false,false,false,false],[true,false,false,true,true,true,false,false,false],[false,true,true,false,false,false,false,true,false],[true,false,false,false,true,false,false,false,true],[true,false,false,true,false,true,false,false,true],[true,false,false,false,true,false,false,false,true],[false,true,false,false,false,false,true,true,false],[false,false,false,true,true,true,false,false,true],[false,false,false,false,true,false,false,true,true]]}",
                Solution = "{\"Grid\":[[5,3,4,6,7,8,9,1,2],[6,7,2,1,9,5,3,4,8],[1,9,8,3,4,2,5,6,7],[8,5,9,7,6,1,4,2,3],[4,2,6,8,5,3,7,9,1],[7,1,3,9,2,4,8,5,6],[9,6,1,5,3,7,2,8,4],[2,8,7,4,1,9,6,3,5],[3,4,5,2,8,6,1,7,9]]}",
                MaxScore = 100,
                TimeLimit = 0
            },
            new PuzzleGame
            {
                Title = "Medium Sudoku #1",
                Description = "Step up the challenge with this medium difficulty puzzle",
                Type = PuzzleType.Sudoku,
                Difficulty = DifficultyLevel.Medium,
                InitialState = "{\"Grid\":[[0,0,0,6,0,0,4,0,0],[7,0,0,0,0,3,6,0,0],[0,0,0,0,9,1,0,8,0],[0,0,0,0,0,0,0,0,0],[0,5,0,1,8,0,0,0,3],[0,0,0,3,0,6,0,4,5],[0,4,0,2,0,0,0,6,0],[9,0,3,0,0,0,0,0,0],[0,2,0,0,0,0,1,0,0]]}",
                Solution = "{\"Grid\":[[1,3,8,6,7,2,4,5,9],[7,9,2,8,5,3,6,1,4],[4,6,5,4,9,1,3,8,2],[8,1,4,5,2,7,9,3,6],[6,5,9,1,8,4,2,7,3],[2,7,3,3,6,9,8,4,5],[5,4,1,2,3,8,7,6,9],[9,8,3,7,1,5,5,2,4],[3,2,6,9,4,6,1,9,8]]}",
                MaxScore = 150,
                TimeLimit = 1800 // 30 minutes
            },
            new PuzzleGame
            {
                Title = "Hard Sudoku #1",
                Description = "A challenging puzzle for experienced players",
                Type = PuzzleType.Sudoku,
                Difficulty = DifficultyLevel.Hard,
                InitialState = "{\"Grid\":[[0,0,0,0,0,0,6,8,0],[0,0,0,0,0,3,0,0,0],[7,0,0,0,9,0,5,0,0],[5,0,0,0,0,7,0,0,0],[0,0,0,0,4,5,7,0,0],[0,0,1,0,0,0,0,0,3],[0,0,1,0,0,0,0,0,6],[0,0,8,5,0,0,0,1,0],[0,9,0,0,0,0,4,0,0]]}",
                Solution = "{\"Grid\":[[1,2,3,4,5,6,6,8,9],[4,5,6,7,8,3,1,2,3],[7,8,9,1,9,2,5,3,4],[5,6,7,8,1,7,2,4,1],[8,1,2,3,4,5,7,6,9],[9,3,1,6,7,8,3,5,3],[2,4,1,9,3,1,8,7,6],[3,7,8,5,6,4,9,1,2],[6,9,5,2,1,3,4,8,7]]}",
                MaxScore = 200,
                TimeLimit = 2700 // 45 minutes
            },
            
            // Word Puzzle Games
            new PuzzleGame
            {
                Title = "Animal Word Search",
                Description = "Find hidden animal names in this word puzzle",
                Type = PuzzleType.WordPuzzle,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"Grid\":[],\"Words\":[{\"Word\":\"CAT\",\"IsFound\":false},{\"Word\":\"DOG\",\"IsFound\":false},{\"Word\":\"BIRD\",\"IsFound\":false}]}",
                Solution = "{\"Grid\":[],\"Words\":[{\"Word\":\"CAT\",\"IsFound\":true},{\"Word\":\"DOG\",\"IsFound\":true},{\"Word\":\"BIRD\",\"IsFound\":true}]}",
                MaxScore = 80,
                TimeLimit = 600 // 10 minutes
            },
            new PuzzleGame
            {
                Title = "Technology Terms",
                Description = "Find technology-related words in this challenging puzzle",
                Type = PuzzleType.WordPuzzle,
                Difficulty = DifficultyLevel.Medium,
                InitialState = "{\"Grid\":[],\"Words\":[{\"Word\":\"COMPUTER\",\"IsFound\":false},{\"Word\":\"INTERNET\",\"IsFound\":false},{\"Word\":\"SOFTWARE\",\"IsFound\":false}]}",
                Solution = "{\"Grid\":[],\"Words\":[{\"Word\":\"COMPUTER\",\"IsFound\":true},{\"Word\":\"INTERNET\",\"IsFound\":true},{\"Word\":\"SOFTWARE\",\"IsFound\":true}]}",
                MaxScore = 120,
                TimeLimit = 900 // 15 minutes
            },
            
            // Logic Puzzle Games
            new PuzzleGame
            {
                Title = "Number Sequence #1",
                Description = "Find the pattern and complete the sequence",
                Type = PuzzleType.LogicPuzzle,
                Difficulty = DifficultyLevel.Easy,
                InitialState = "{\"PuzzleType\":\"NumberSequence\",\"State\":{\"sequence\":[2,4,6,8],\"answer\":10,\"pattern\":\"arithmetic\"}}",
                Solution = "{\"PuzzleType\":\"NumberSequence\",\"State\":{\"sequence\":[2,4,6,8,10],\"answer\":10,\"pattern\":\"arithmetic\"}}",
                MaxScore = 60,
                TimeLimit = 300 // 5 minutes
            },
            new PuzzleGame
            {
                Title = "Fibonacci Challenge",
                Description = "Complete this famous mathematical sequence",
                Type = PuzzleType.LogicPuzzle,
                Difficulty = DifficultyLevel.Hard,
                InitialState = "{\"PuzzleType\":\"NumberSequence\",\"State\":{\"sequence\":[1,1,2,3,5],\"answer\":8,\"pattern\":\"fibonacci\"}}",
                Solution = "{\"PuzzleType\":\"NumberSequence\",\"State\":{\"sequence\":[1,1,2,3,5,8],\"answer\":8,\"pattern\":\"fibonacci\"}}",
                MaxScore = 150,
                TimeLimit = 600 // 10 minutes
            }
        };

        context.PuzzleGames.AddRange(puzzleGames);
        await context.SaveChangesAsync();
    }
    
    private static async Task CreateBrainGamesAsync(ApplicationDbContext context)
    {
        // Only create brain games if none exist
        if (await context.Games.AnyAsync())
            return;

        var brainGames = new List<Game>
        {
            new Game
            {
                Name = "Memory Card Matching",
                Description = "Test your memory by matching pairs of cards. Flip cards to find matching pairs and clear the board.",
                Type = GameType.MemoryCardMatching,
                IsActive = true
            },
            new Game
            {
                Name = "Pattern Memory Game",
                Description = "Watch the pattern sequence and repeat it back. Each level adds more complexity to challenge your memory.",
                Type = GameType.PatternMemory,
                IsActive = true
            },
            new Game
            {
                Name = "Number Recall Game",
                Description = "Memorize sequences of numbers and recall them in the correct order. Great for improving working memory.",
                Type = GameType.NumberRecall,
                IsActive = true
            },
            new Game
            {
                Name = "Image Memory Game",
                Description = "Study images briefly and then identify them from a larger set. Enhance your visual memory skills.",
                Type = GameType.ImageMemory,
                IsActive = true
            },
            new Game
            {
                Name = "Number Guessing Game",
                Description = "Use logic and deduction to guess the secret number with minimal attempts. Sharpen your analytical thinking.",
                Type = GameType.NumberGuessing,
                IsActive = true
            },
            new Game
            {
                Name = "Arithmetic Challenge",
                Description = "Solve math problems quickly and accurately. Improve your mental calculation speed and accuracy.",
                Type = GameType.ArithmeticChallenge,
                IsActive = true
            },
            new Game
            {
                Name = "Fast Calculation Game",
                Description = "Race against time to solve as many math problems as possible. Perfect for building mental math fluency.",
                Type = GameType.FastCalculation,
                IsActive = true
            },
            new Game
            {
                Name = "Sequence Completion",
                Description = "Identify patterns in number or shape sequences and complete them. Develop logical reasoning skills.",
                Type = GameType.SequenceCompletion,
                IsActive = true
            }
        };

        context.Games.AddRange(brainGames);
        await context.SaveChangesAsync();
    }
}