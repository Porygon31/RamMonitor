using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Core.Services;

/// <summary>
/// Évalue les règles d'alerte contre un snapshot et applique l'anti-spam.
/// L'anti-spam est scoped par clé (id de règle pour les règles globales,
/// "id:pid" pour les règles par process), ce qui permet à 2 process distincts
/// d'alerter dans le même tick sans s'auto-bloquer.
/// </summary>
public sealed class AlertEvaluator : IAlertEvaluator
{
    // Dernier déclenchement par clé d'anti-spam. Pas de purge nécessaire :
    // une centaine d'entrées max sur la durée de vie de l'app.
    private readonly Dictionary<string, DateTimeOffset> _lastFired = new(StringComparer.Ordinal);
    private List<AlertRule> _rules = new();

    /// <inheritdoc />
    public IReadOnlyList<AlertRule> Rules => _rules;

    /// <inheritdoc />
    public void SetRules(IEnumerable<AlertRule> rules)
    {
        _rules = rules?.ToList() ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <inheritdoc />
    public IReadOnlyList<AlertRecord> Evaluate(MemorySnapshot memory,
        IReadOnlyList<ProcessSnapshot> processes, DateTimeOffset now)
    {
        var alerts = new List<AlertRecord>();

        foreach (var rule in _rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            switch (rule.Kind)
            {
                case AlertRuleKind.GlobalRamPercent:
                    if (memory.UsedPercent >= rule.Threshold && ShouldFire(rule, now))
                    {
                        alerts.Add(BuildMemoryAlert(rule, now, memory.UsedPercent,
                            "Global RAM usage high",
                            $"RAM usage is at {memory.UsedPercent:F1}% (threshold {rule.Threshold:F0}%)"));
                    }
                    break;

                case AlertRuleKind.PagefilePercent:
                    if (memory.PagefileUsedPercent >= rule.Threshold && ShouldFire(rule, now))
                    {
                        alerts.Add(BuildMemoryAlert(rule, now, memory.PagefileUsedPercent,
                            "Pagefile usage high",
                            $"Pagefile usage is at {memory.PagefileUsedPercent:F1}% (threshold {rule.Threshold:F0}%)"));
                    }
                    break;

                case AlertRuleKind.CommitPercent:
                    if (memory.CommitPercent >= rule.Threshold && ShouldFire(rule, now))
                    {
                        alerts.Add(BuildMemoryAlert(rule, now, memory.CommitPercent,
                            "Commit charge high",
                            $"Commit usage is at {memory.CommitPercent:F1}% (threshold {rule.Threshold:F0}%)"));
                    }
                    break;

                case AlertRuleKind.ProcessWorkingSetMb:
                    // Seuil en MB → conversion en octets une fois pour la boucle.
                    var thresholdBytes = (ulong)(rule.Threshold * 1024 * 1024);
                    foreach (var p in processes)
                    {
                        if (p.WorkingSetBytes < thresholdBytes)
                        {
                            continue;
                        }

                        // Clé scoped par PID : permet d'alerter sur plusieurs process à la fois.
                        var key = $"{rule.Id}:{p.ProcessId}";
                        if (!ShouldFire(key, rule.MinReNotifyInterval, now))
                        {
                            continue;
                        }

                        alerts.Add(new AlertRecord
                        {
                            Timestamp = now,
                            RuleId = rule.Id,
                            Kind = rule.Kind,
                            Severity = rule.Severity,
                            Title = $"Process memory high: {p.Name}",
                            Message = $"{p.Name} (PID {p.ProcessId}) Working Set "
                                      + $"{p.WorkingSetBytes / (1024.0 * 1024.0):F0} MB"
                                      + $" (threshold {rule.Threshold:F0} MB)",
                            RelatedProcessId = p.ProcessId,
                            RelatedProcessName = p.Name,
                            MeasuredValue = p.WorkingSetBytes / (1024.0 * 1024.0),
                            ThresholdValue = rule.Threshold
                        });
                    }
                    break;
            }
        }

        return alerts;
    }

    /// <inheritdoc />
    public IReadOnlyList<AlertRecord> EvaluateLeak(LeakReport leak, DateTimeOffset now)
    {
        // Pas de fuite → pas d'alerte (même si la règle existe).
        if (!leak.IsSuspectedLeak)
        {
            return Array.Empty<AlertRecord>();
        }

        var rule = _rules.FirstOrDefault(r => r.Kind == AlertRuleKind.LeakDetected && r.Enabled);
        if (rule is null)
        {
            return Array.Empty<AlertRecord>();
        }

        // Anti-spam scoped par PID pour permettre des alertes sur plusieurs process.
        var key = $"{rule.Id}:{leak.ProcessId}";
        if (!ShouldFire(key, rule.MinReNotifyInterval, now))
        {
            return Array.Empty<AlertRecord>();
        }

        return new[]
        {
            new AlertRecord
            {
                Timestamp = now,
                RuleId = rule.Id,
                Kind = AlertRuleKind.LeakDetected,
                Severity = rule.Severity,
                Title = $"Suspected memory leak: {leak.ProcessName}",
                Message = $"{leak.ProcessName} (PID {leak.ProcessId}) is growing at "
                          + $"{leak.GrowthRateBytesPerMinute / (1024.0 * 1024.0):F2} MB/min "
                          + $"(R²={leak.RSquared:F2}, {leak.SampleCount} samples)",
                RelatedProcessId = leak.ProcessId,
                RelatedProcessName = leak.ProcessName,
                MeasuredValue = leak.GrowthRateBytesPerMinute,
                ThresholdValue = null
            }
        };
    }

    /// <summary>Construit une alerte mémoire globale (factorisation des 3 cases similaires).</summary>
    private static AlertRecord BuildMemoryAlert(AlertRule rule, DateTimeOffset now,
        double measured, string title, string message) => new()
    {
        Timestamp = now,
        RuleId = rule.Id,
        Kind = rule.Kind,
        Severity = rule.Severity,
        Title = title,
        Message = message,
        MeasuredValue = measured,
        ThresholdValue = rule.Threshold
    };

    private bool ShouldFire(AlertRule rule, DateTimeOffset now)
        => ShouldFire(rule.Id, rule.MinReNotifyInterval, now);

    /// <summary>
    /// Vrai si l'anti-spam autorise un nouveau déclenchement pour la clé donnée.
    /// Met à jour <see cref="_lastFired"/> en cas de succès (side-effect intentionnel).
    /// </summary>
    private bool ShouldFire(string key, TimeSpan minInterval, DateTimeOffset now)
    {
        if (_lastFired.TryGetValue(key, out var last) && now - last < minInterval)
        {
            return false;
        }

        _lastFired[key] = now;
        return true;
    }
}
