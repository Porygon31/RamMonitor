using CommunityToolkit.Mvvm.ComponentModel;
using RamMonitor.Core.Models;
using RamMonitor.Core.Utils;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Wrap un <see cref="ProcessSnapshot"/> avec des propriétés formatées
/// (texte "1.2 GB" au lieu d'octets bruts). Permet à la View de binder
/// directement sans converters.
/// </summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    public int ProcessId { get; }
    public string Name { get; }
    public string? ExecutablePath { get; }
    public bool IsSystem { get; }

    [ObservableProperty] private ulong _workingSetBytes;
    [ObservableProperty] private ulong _privateBytes;
    [ObservableProperty] private ulong _virtualSizeBytes;
    [ObservableProperty] private ulong _pagefileBytes;
    [ObservableProperty] private uint _handleCount;
    [ObservableProperty] private uint _threadCount;
    [ObservableProperty] private DateTimeOffset _lastUpdated;

    public string WorkingSetText => ByteFormatter.Format(WorkingSetBytes);
    public string PrivateText => ByteFormatter.Format(PrivateBytes);
    public string VirtualSizeText => ByteFormatter.Format(VirtualSizeBytes);
    public string PagefileText => ByteFormatter.Format(PagefileBytes);

    public ProcessRowViewModel(ProcessSnapshot snapshot)
    {
        ProcessId = snapshot.ProcessId;
        Name = snapshot.Name;
        ExecutablePath = snapshot.ExecutablePath;
        IsSystem = snapshot.IsSystem;
        Update(snapshot);
    }

    /// <summary>Met à jour les compteurs sans recréer l'objet (préserve le tri/sélection WPF).</summary>
    public void Update(ProcessSnapshot snapshot)
    {
        WorkingSetBytes = snapshot.WorkingSetBytes;
        PrivateBytes = snapshot.PrivateBytes;
        VirtualSizeBytes = snapshot.VirtualSizeBytes;
        PagefileBytes = snapshot.PagefileBytes;
        HandleCount = snapshot.HandleCount;
        ThreadCount = snapshot.ThreadCount;
        LastUpdated = snapshot.Timestamp;

        OnPropertyChanged(nameof(WorkingSetText));
        OnPropertyChanged(nameof(PrivateText));
        OnPropertyChanged(nameof(VirtualSizeText));
        OnPropertyChanged(nameof(PagefileText));
    }
}
