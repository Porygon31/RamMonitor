# RamMonitor

> A professional Windows RAM monitoring & management tool, built with .NET 8 + WPF.
> Inspired by Task Manager, Process Explorer and Sysinternals RAMMap.

![Status](https://img.shields.io/badge/status-v1.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 🎯 Présentation

**RamMonitor** combine les meilleures fonctionnalités de Task Manager (vue temps réel),
Process Explorer (actions sur les process) et RAMMap (détails standby cache, modified list,
pagefile) — enrichi d'un système d'**alertes**, **détection de fuites mémoire** par régression
linéaire, et **export multi-format** pour rapports d'incident.

L'application démarre en utilisateur standard et propose une élévation à la demande
(pattern Process Explorer) pour débloquer les fonctionnalités avancées.

---

## ✨ Fonctionnalités

### Monitoring

- RAM globale temps réel (Total / Used / Available / Commit / Pagefile)
- Détails avancés : Standby cache, modified page list, kernel paged/non-paged, hardware reserved
- Liste des process avec Working Set / Private / Virtual / Pools / Handles / Threads
- Graphiques LiveCharts2 — courbes RAM% et Commit%
- Historique persistant SQLite (15 min → 7 jours)
- Détection de fuites : régression linéaire sur Working Set, R² ≥ 0.85

### Alertes

- Seuils paramétrables (RAM, commit, pagefile, process WS)
- Notifications toast Windows (anti-empilage)
- Anti-spam configurable
- Historique persistant 24h consultable

### Actions

- Kill process, Empty Working Set, Set priority (Idle → Realtime)
- Mini Dump / Full Dump via `MiniDumpWriteDump`
- Clear Standby Cache, Flush Modified Pages, Empty All Working Sets (admin)

### Export

- **CSV** — dossier multi-fichiers Excel-friendly
- **JSON** — single file structuré
- **PNG** — capture du graphe via SkiaSharp headless
- **HTML** — rapport autonome single-file (dark theme inline)

---

## 🧰 Prérequis

### Exécution
- Windows 10 (1809+) ou Windows 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Compilation
- .NET 8 SDK (8.0.100+)
- Visual Studio 2022 17.8+ ou VS Code + C# Dev Kit ou ligne de commande `dotnet`
- Plateforme cible : **x64**

---

## 🔨 Build & exécution

### Visual Studio
1. Ouvrir `RamMonitor.sln`
2. Configuration **Release | x64**
3. Projet de démarrage : **RamMonitor.App**
4. **F5**

### Ligne de commande

```powershell
dotnet restore RamMonitor.sln
dotnet build RamMonitor.sln -c Release
dotnet run --project src/RamMonitor.App/RamMonitor.App.csproj -c Release

# Tests
dotnet test tests/RamMonitor.Tests/RamMonitor.Tests.csproj
```

### Publish single-file

```powershell
dotnet publish src/RamMonitor.App/RamMonitor.App.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o publish/
```

---

## ⚙️ Configuration

Tout est dans `src/RamMonitor.App/appsettings.json` :

```json
{
  "RamMonitor": {
    "Polling": { "FastIntervalMs": 1000, "SlowIntervalMs": 5000 },
    "Database": { "RetentionDays": 7 },
    "Alerts": {
      "GlobalRamPercentWarning": 80,
      "GlobalRamPercentCritical": 92,
      "EnableToasts": true
    },
    "LeakDetection": {
      "WindowMinutes": 30, "MinSamples": 20,
      "GrowthRateBytesPerMinuteThreshold": 1048576, "MinR2": 0.85
    }
  }
}
```

Surcharge possible par variable d'environnement :

```powershell
$env:RAMMONITOR_RamMonitor__Polling__FastIntervalMs = "500"
.\RamMonitor.exe
```

---

## 🖱 Utilisation

| Onglet | Rôle |
|---|---|
| 📊 Dashboard | Vue temps réel — graphe + cartes synthèse |
| ⚙ Processes | Tableau live + actions (kill, EmptyWS, dump, priority) |
| 📈 History | Requêtes SQLite + export 4 formats |
| 🔔 Alerts | Alertes live + chargement historique 24h |
| 🔧 Settings | Paramètres runtime + actions Memory Manager (admin) |

### Élévation à la demande

Bouton **🛡 Run as Administrator** dans la sidebar (visible uniquement en mode standard).
Pattern Process Explorer : relance avec UAC plutôt qu'élévation au démarrage.

---

## 📁 Structure du projet

```
RamMonitor_CSharp_WPF/
├── src/
│   ├── RamMonitor.Core/              # Logique métier pure
│   ├── RamMonitor.Infrastructure/    # Win32 P/Invoke, SQLite, Export
│   └── RamMonitor.App/               # WPF (Views + ViewModels + DI)
├── tests/
│   └── RamMonitor.Tests/             # xUnit + FluentAssertions
├── RamMonitor.sln
├── Directory.Build.props
└── global.json
```

---

## 🔐 Sécurité & élévation

| Opération | Admin requis |
|---|---|
| Monitoring global / process owned | Non |
| Kill / EmptyWS / Dump process d'un autre user | Oui (`SeDebug`) |
| Clear Standby / Flush Modified | Oui (`SeProfileSingleProcess`) |
| Modifier pagefile | Oui (`SeCreatePagefile`) |

Les actions admin sont **automatiquement désactivées** quand l'app n'est pas élevée.

L'API `NtSetSystemInformation` est **non documentée** par Microsoft (reverse-engineered
depuis Sysinternals RAMMap). Stable depuis Vista jusqu'à Windows 11.

---

## 🩺 Troubleshooting

| Problème | Solution |
|---|---|
| Crash au démarrage | Consulter `logs/rammonitor-YYYYMMDD.log` |
| Standby Cache = 0 | App non élevée — cliquer sur "Run as Administrator" |
| Toasts absents | Vérifier notifications Windows activées + option Settings |
| SQLite "database is locked" | Une autre instance tourne en parallèle |
| Graphes figés | Vérifier `FastIntervalMs` dans Settings |
| `dbghelp.dll` introuvable | Sur Windows Server Core, installer "Debug Tools" |

---

## 🛣 Roadmap

- [ ] Persistance des règles d'alerte personnalisées (table SQLite dédiée)
- [ ] Tray icon + menu "Top RAM consumers"
- [ ] Localisation FR/EN dynamique
- [ ] Monitoring CPU + disque
- [ ] Module ETW pour événements process temps réel
- [ ] Driver WDM/KMDF (v2.0) pour accès aux structures noyau

---

## 📜 Licence

MIT.

---

## 🙏 Stack technique

- .NET 8 + WPF + CommunityToolkit.Mvvm
- LiveCharts2 (SkiaSharp)
- Microsoft.Data.Sqlite + Dapper
- Serilog
- Microsoft.Toolkit.Uwp.Notifications
- xUnit + FluentAssertions + Moq

---

*Built with ❤️ on Windows 11.*
