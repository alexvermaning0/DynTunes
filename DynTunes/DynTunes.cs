using System.Reflection;
using DynTunes.Connectors;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;

namespace DynTunes;

public partial class DynTunes : ResoniteMod
{
    public override string Name => nameof(DynTunes);
    public override string Author => "jvyden";
    public override string Version => typeof(DynTunes).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    public override string Link => "https://github.com/jvyden/" + nameof(DynTunes);
    
    public static ModConfiguration? Config { get; private set; }

    public static IMusicConnector Connector;

    public override void OnEngineInit()
    {
        Harmony harmony = new("xyz.jvyden." + nameof(DynTunes));
        Config = GetConfiguration();
        Config?.Save(true);
        harmony.PatchAll();

        Engine.Current.OnReady += () =>
        {
            // Choose connector based on platform
            if (OperatingSystem.IsWindows())
            {
                Connector = TryLoadWindowsConnector() ?? new DummyMusicConnector();
            }
            else if (OperatingSystem.IsLinux())
            {
                Connector = new MPRISMusicConnector();
            }
            else
            {
                Connector = new DummyMusicConnector();
            }
        };
    }

    private static IMusicConnector? TryLoadWindowsConnector()
    {
        try
        {
            // Try to load the Windows-specific assembly
            string assemblyPath = Path.Combine(
                Path.GetDirectoryName(typeof(DynTunes).Assembly.Location) ?? "",
                "..",
                "rml_libs",
                "DynTunes.Windows.dll"
            );

            if (!File.Exists(assemblyPath))
            {
                Warn("DynTunes.Windows.dll not found. Falling back to dummy connector.");
                Warn($"Expected path: {assemblyPath}");
                return null;
            }

            Assembly windowsAssembly = Assembly.LoadFrom(assemblyPath);
            
            // Try multiple possible type names
            string[] possibleTypeNames = new[]
            {
                "DynTunes.Windows.Connectors.WindowsMusicConnector",
                "DynTunes.Connectors.WindowsMusicConnector"
            };

            Type? connectorType = null;
            foreach (string typeName in possibleTypeNames)
            {
                connectorType = windowsAssembly.GetType(typeName);
                if (connectorType != null)
                {
                    Msg($"Found Windows connector type: {typeName}");
                    break;
                }
            }

            if (connectorType == null)
            {
                Warn("WindowsMusicConnector type not found in DynTunes.Windows.dll");
                Warn("Available types:");
                foreach (Type type in windowsAssembly.GetTypes())
                {
                    Warn($"  - {type.FullName}");
                }
                return null;
            }

            object? instance = Activator.CreateInstance(connectorType);
            if (instance is IMusicConnector connector)
            {
                Msg("Successfully loaded Windows Media connector");
                return connector;
            }

            Warn("Failed to create Windows connector instance");
            return null;
        }
        catch (Exception ex)
        {
            Error($"Failed to load Windows connector: {ex}");
            return null;
        }
    }
}