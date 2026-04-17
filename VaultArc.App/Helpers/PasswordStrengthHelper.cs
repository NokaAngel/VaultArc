namespace VaultArc.App.Helpers;

internal static class PasswordStrengthHelper
{
    public enum Strength { Weak, Fair, Good, Strong, VeryStrong }

    public static (Strength Level, string Label, double Percent) Evaluate(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (Strength.Weak, "Enter a password", 0);

        var score = 0;

        if (password.Length >= 6) score++;
        if (password.Length >= 10) score++;
        if (password.Length >= 16) score++;
        if (password.Any(char.IsUpper) && password.Any(char.IsLower)) score++;
        if (password.Any(char.IsDigit)) score++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) score++;

        var uniqueRatio = (double)password.Distinct().Count() / password.Length;
        if (uniqueRatio > 0.7) score++;

        return score switch
        {
            <= 1 => (Strength.Weak, "Weak", 15),
            2 => (Strength.Fair, "Fair", 35),
            3 or 4 => (Strength.Good, "Good", 60),
            5 => (Strength.Strong, "Strong", 80),
            _ => (Strength.VeryStrong, "Very strong", 100)
        };
    }

    public static string GetColor(Strength level) => level switch
    {
        Strength.Weak => "#E53935",
        Strength.Fair => "#FB8C00",
        Strength.Good => "#FDD835",
        Strength.Strong => "#43A047",
        Strength.VeryStrong => "#2E7D32",
        _ => "#888888"
    };
}
