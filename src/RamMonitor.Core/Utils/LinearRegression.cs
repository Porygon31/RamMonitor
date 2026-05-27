namespace RamMonitor.Core.Utils;

/// <summary>
/// Résultat d'une régression linéaire des moindres carrés.
/// </summary>
/// <param name="Slope">Pente (a dans y = a·x + b).</param>
/// <param name="Intercept">Ordonnée à l'origine (b).</param>
/// <param name="RSquared">Coefficient de détermination R² (qualité de l'ajustement, 0..1).</param>
public readonly record struct LinearRegressionResult(double Slope, double Intercept, double RSquared);

/// <summary>
/// Régression linéaire des moindres carrés. Utilisée par le détecteur de fuites
/// pour estimer la pente du Working Set en fonction du temps.
/// </summary>
public static class LinearRegression
{
    /// <summary>
    /// Calcule la régression linéaire (xs, ys) → y = slope·x + intercept.
    /// Cas dégénérés :
    /// <list type="bullet">
    ///   <item>0 ou 1 point : retourne (0, y₀, 0).</item>
    ///   <item>Variance de xs nulle (tous identiques) : retourne (0, moyenne(ys), 0).</item>
    ///   <item>Variance de ys nulle (ligne plate) : retourne (0, ys₀, 1) par convention.</item>
    /// </list>
    /// </summary>
    public static LinearRegressionResult Fit(ReadOnlySpan<double> xs, ReadOnlySpan<double> ys)
    {
        if (xs.Length != ys.Length)
        {
            throw new ArgumentException("xs and ys must have the same length.");
        }

        if (xs.Length < 2)
        {
            return new LinearRegressionResult(0.0, ys.Length == 1 ? ys[0] : 0.0, 0.0);
        }

        // Sommes nécessaires (formule classique en O(n) sans tableau temporaire).
        var n = xs.Length;
        double sumX = 0.0;
        double sumY = 0.0;
        double sumXY = 0.0;
        double sumXX = 0.0;

        for (var i = 0; i < n; i++)
        {
            sumX += xs[i];
            sumY += ys[i];
            sumXY += xs[i] * ys[i];
            sumXX += xs[i] * xs[i];
        }

        var meanX = sumX / n;
        var meanY = sumY / n;
        var denominator = sumXX - n * meanX * meanX; // = Σ(xᵢ - x̄)²

        if (Math.Abs(denominator) < double.Epsilon)
        {
            // Tous les xs identiques : pente indéfinie, on retourne 0 par convention.
            return new LinearRegressionResult(0.0, meanY, 0.0);
        }

        // Formules des moindres carrés.
        var slope = (sumXY - n * meanX * meanY) / denominator;
        var intercept = meanY - slope * meanX;

        // Calcul du R² = 1 - SSres / SStot
        double ssTot = 0.0;
        double ssRes = 0.0;
        for (var i = 0; i < n; i++)
        {
            var predicted = slope * xs[i] + intercept;
            ssRes += (ys[i] - predicted) * (ys[i] - predicted);
            ssTot += (ys[i] - meanY) * (ys[i] - meanY);
        }

        // Si SStot == 0 (ys constants), R² serait 0/0 : convention 1.0 (ajustement parfait trivial).
        var r2 = ssTot < double.Epsilon ? 1.0 : Math.Max(0.0, 1.0 - ssRes / ssTot);
        return new LinearRegressionResult(slope, intercept, r2);
    }
}
