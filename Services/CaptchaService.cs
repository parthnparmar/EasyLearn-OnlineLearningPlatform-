namespace EasyLearn.Services;

public interface ICaptchaService
{
    (string question, int answer) GenerateMathCaptcha();
    bool VerifyCaptcha(int userAnswer, int correctAnswer);
}

public class CaptchaService : ICaptchaService
{
    private readonly Random _random = new();

    public (string question, int answer) GenerateMathCaptcha()
    {
        var operations = new[] { "+", "-", "*" };
        var operation = operations[_random.Next(operations.Length)];
        
        int num1, num2, answer;
        string question;

        switch (operation)
        {
            case "+":
                num1 = _random.Next(1, 50);
                num2 = _random.Next(1, 50);
                answer = num1 + num2;
                question = $"{num1} + {num2} = ?";
                break;
            case "-":
                num1 = _random.Next(10, 100);
                num2 = _random.Next(1, num1);
                answer = num1 - num2;
                question = $"{num1} - {num2} = ?";
                break;
            case "*":
                num1 = _random.Next(1, 12);
                num2 = _random.Next(1, 12);
                answer = num1 * num2;
                question = $"{num1} × {num2} = ?";
                break;
            default:
                num1 = _random.Next(1, 20);
                num2 = _random.Next(1, 20);
                answer = num1 + num2;
                question = $"{num1} + {num2} = ?";
                break;
        }

        return (question, answer);
    }

    public bool VerifyCaptcha(int userAnswer, int correctAnswer)
    {
        return userAnswer == correctAnswer;
    }
}