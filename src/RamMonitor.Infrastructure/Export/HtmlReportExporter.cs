using System.Globalization;
using System.Text;
using System.Net;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Core.Utils;

namespace RamMonitor.Infrastructure.Export;

/// <summary>
/// Exporteur HTML : produit un fichier .html unique et auto-suffisant
/// (pas de dépendance externe — CSS inline). Idéal pour rapports d'incident
/// à partager par mail ou archiver tel quel.
/// </summary>
public sealed class HtmlReportExporter : IExporter
{
    public string FormatId => "html";
    public string DisplayName => "Rapport HTML";
    public string FileExtension => ".html";

    public async ValueTask<string> ExportAsync(SystemSnapshot snapshot, string targetFolder,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetFolder);
        var stamp = snapshot.Timestamp.ToLocalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(targetFolder, $"rammonitor_report_{stamp}.html");

        var html = BuildHtml(snapshot);
        await File.WriteAllTextAsync(path, html, new UTF8Encoding(false), cancellationToken)
            .ConfigureAwait(false);

        return path;
    }

    private static string BuildHtml(SystemSnapshot s)
    {
        var sb = new StringBuilder(64 * 1024);

        sb.Append("""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title>RamMonitor Report</title>
            <style>
                :root { color-scheme: dark; }
                body { background:#1e1f22; color:#e6e6e6; font-family:'Segoe UI',sans-serif;
                       margin:2rem; line-height:1.5; }
                h1 { color:#7ec5ff; border-bottom:1px solid #444; padding-bottom:.5rem; }
                h2 { color:#9ee493; margin-top:2rem; }
                .meta { color:#9aa0a6; margin-bottom:2rem; }
                .grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(220px,1fr));
                        gap:1rem; margin:1rem 0; }
                .card { background:#2b2d31; padding:1rem; border-radius:.5rem; border:1px solid #3a3d42; }
                .card .label { color:#9aa0a6; font-size:.85rem; text-transform:uppercase; letter-spacing:.05em; }
                .card .value { font-size:1.4rem; font-weight:600; margin-top:.25rem; color:#fff; }
                .bar { height:6px; background:#3a3d42; border-radius:3px; margin-top:.5rem; overflow:hidden; }
                .bar > div { height:100%; background:linear-gradient(90deg,#5cb85c,#f0ad4e,#d9534f);
                             background-size:200px; }
                table { width:100%; border-collapse:collapse; margin-top:1rem; }
                th, td { padding:.5rem .75rem; text-align:left; border-bottom:1px solid #3a3d42;
                         font-variant-numeric:tabular-nums; }
                th { background:#2b2d31; color:#9ee493; }
                tr:hover { background:#26282c; }
                .num { text-align:right; font-family:Consolas,monospace; }
                .pill { display:inline-block; padding:.1rem .5rem; border-radius:.75rem;
                        background:#3a3d42; color:#e6e6e6; font-size:.75rem; }
                footer { color:#666; margin-top:3rem; font-size:.8rem; text-align:center; }
            </style>
            </head>
            <body>
            """);

        sb.Append(CultureInfo.InvariantCulture, $"<h1>RamMonitor — System Snapshot</h1>");
        sb.Append("<div class=\"meta\">");
        sb.Append(CultureInfo.InvariantCulture, $"<strong>Captured:</strong> {Escape(s.Timestamp.ToLocalTime().ToString("F", CultureInfo.InvariantCulture))} &nbsp;|&nbsp; ");
        sb.Append(CultureInfo.InvariantCulture, $"<strong>Machine:</strong> {Escape(s.MachineName)} &nbsp;|&nbsp; ");
        sb.Append(CultureInfo.InvariantCulture, $"<strong>User:</strong> {Escape(s.UserName)} &nbsp;|&nbsp; ");
        sb.Append(CultureInfo.InvariantCulture, $"<strong>OS:</strong> {Escape(s.OsVersion)} &nbsp;|&nbsp; ");
        sb.Append(CultureInfo.InvariantCulture, $"<strong>Elevated:</strong> <span class=\"pill\">{(s.IsElevated ? "Yes" : "No")}</span>");
        sb.Append("</div>");

        // ---- Cartes synthèse mémoire ----
        sb.Append("<h2>Memory Overview</h2>");
        sb.Append("<div class=\"grid\">");
        AppendCard(sb, "Total Physical", ByteFormatter.Format(s.Memory.TotalPhysicalBytes));
        AppendCard(sb, "Used", $"{ByteFormatter.Format(s.Memory.UsedPhysicalBytes)} ({s.Memory.UsedPercent:F1}%)");
        AppendCard(sb, "Available", ByteFormatter.Format(s.Memory.AvailablePhysicalBytes));
        AppendCard(sb, "Commit", $"{ByteFormatter.Format(s.Memory.CommitTotalBytes)} / {ByteFormatter.Format(s.Memory.CommitLimitBytes)}");
        AppendCard(sb, "Standby Cache", ByteFormatter.Format(s.Memory.StandbyCacheBytes));
        AppendCard(sb, "Modified List", ByteFormatter.Format(s.Memory.ModifiedPageListBytes));
        AppendCard(sb, "Kernel Paged", ByteFormatter.Format(s.Memory.KernelPagedBytes));
        AppendCard(sb, "Kernel Non-Paged", ByteFormatter.Format(s.Memory.KernelNonPagedBytes));
        AppendCard(sb, "Pagefile Used %", $"{s.Memory.PagefileUsedPercent:F1}%");
        AppendCard(sb, "Hardware Reserved", ByteFormatter.Format(s.Memory.HardwareReservedBytes));
        sb.Append("</div>");

        // ---- Pagefiles ----
        if (s.Pagefiles.Count > 0)
        {
            sb.Append("<h2>Pagefiles</h2><table><thead><tr><th>Path</th><th class=\"num\">Current</th><th class=\"num\">Peak</th><th class=\"num\">Initial</th><th class=\"num\">Max</th><th>Mode</th></tr></thead><tbody>");
            foreach (var pf in s.Pagefiles)
            {
                sb.Append("<tr>");
                sb.Append(CultureInfo.InvariantCulture, $"<td>{Escape(pf.Path)}</td>");
                sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{ByteFormatter.Format(pf.CurrentSizeBytes)}</td>");
                sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{ByteFormatter.Format(pf.PeakUsageBytes)}</td>");
                sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{ByteFormatter.Format(pf.InitialSizeBytes)}</td>");
                sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{ByteFormatter.Format(pf.MaximumSizeBytes)}</td>");
                sb.Append(CultureInfo.InvariantCulture, $"<td>{(pf.IsSystemManaged ? "System managed" : "Custom")}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
        }

        // ---- Top process par working set ----
        sb.Append("<h2>Top Processes by Working Set</h2>");
        sb.Append("<table><thead><tr><th>PID</th><th>Name</th><th class=\"num\">Working Set</th>"
                  + "<th class=\"num\">Private</th><th class=\"num\">Virtual</th>"
                  + "<th class=\"num\">Handles</th><th class=\"num\">Threads</th></tr></thead><tbody>");

        foreach (var p in s.Processes.OrderByDescending(x => x.WorkingSetBytes).Take(100))
        {
            sb.Append("<tr>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{p.ProcessId}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td>{Escape(p.Name)}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{ByteFormatter.Format(p.WorkingSetBytes)}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{ByteFormatter.Format(p.PrivateBytes)}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{ByteFormatter.Format(p.VirtualSizeBytes)}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{p.HandleCount}</td>");
            sb.Append(CultureInfo.InvariantCulture, $"<td class=\"num\">{p.ThreadCount}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");

        sb.Append("<footer>Generated by RamMonitor — single-file HTML report (no external assets).</footer>");
        sb.Append("</body></html>");

        return sb.ToString();
    }

    private static void AppendCard(StringBuilder sb, string label, string value)
    {
        sb.Append("<div class=\"card\">");
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"label\">{Escape(label)}</div>");
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"value\">{Escape(value)}</div>");
        sb.Append("</div>");
    }

    /// <summary>Échappe le HTML pour éviter toute injection depuis des noms de process exotiques.</summary>
    private static string Escape(string s) => global::System.Net.WebUtility.HtmlEncode(s);
}
