using System.Windows;
using System.Windows.Markup;

// Thèmes WPF localisés dans le même assembly (utilisation des ResourceDictionary).
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

// Permet d'utiliser "clr-namespace:RamMonitor.App.Converters" comme "rm:" dans le XAML.
[assembly: XmlnsDefinition("https://schemas.rammonitor.io/wpf", "RamMonitor.App.Converters")]
[assembly: XmlnsDefinition("https://schemas.rammonitor.io/wpf", "RamMonitor.App.ViewModels")]
[assembly: XmlnsPrefix("https://schemas.rammonitor.io/wpf", "rm")]
