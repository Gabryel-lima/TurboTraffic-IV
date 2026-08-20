using GTA;

public static class ModSettings
{
    private const string Category = "TurboTraffic";

    public const float ChaosRadiusMin = 20.0f;
    public const float ChaosRadiusMax = 500.0f;
    public const float ChaosRadiusStep = 10.0f;

    public const float ChaosTurboPowerMin = 10.0f;
    public const float ChaosTurboPowerMax = 200.0f;
    public const float ChaosTurboPowerStep = 5.0f;

    private static SettingsFile _file;

    public static bool ChaosEnabled { get; private set; }
    public static float ChaosRadius { get; private set; }
    public static float ChaosTurboPower { get; private set; }

    public static void Load()
    {
        // Defaults usados caso o .ini ainda não exista. O caos no trânsito nasce
        // DESLIGADO: o mod só passa a agir depois que o jogador ligar pelo HUD.
        ChaosEnabled = false;
        ChaosRadius = 120.0f;
        ChaosTurboPower = 70.0f;

        try
        {
            _file = SettingsFile.Open(ModPaths.SettingsFilePath);
            _file.Load();

            ChaosEnabled = _file.GetValueBool(Category, "ChaosEnabled", false);
            ChaosRadius = Clamp(_file.GetValueFloat(Category, "ChaosRadius", 120.0f), ChaosRadiusMin, ChaosRadiusMax);
            ChaosTurboPower = Clamp(_file.GetValueFloat(Category, "ChaosTurboPower", 70.0f), ChaosTurboPowerMin, ChaosTurboPowerMax);
        }
        catch (System.Exception ex)
        {
            _file = null;
            ModLog.WriteException("ModSettings.Load", ex);
        }

        ModLog.Write("Config carregada: caos=" + ChaosEnabled + " raio=" + ChaosRadius + " forca=" + ChaosTurboPower);
    }

    public static void ToggleChaosEnabled()
    {
        ChaosEnabled = !ChaosEnabled;
        Persist();
    }

    public static void AdjustChaosRadius(float delta)
    {
        ChaosRadius = Clamp(ChaosRadius + delta, ChaosRadiusMin, ChaosRadiusMax);
        Persist();
    }

    public static void AdjustChaosTurboPower(float delta)
    {
        ChaosTurboPower = Clamp(ChaosTurboPower + delta, ChaosTurboPowerMin, ChaosTurboPowerMax);
        Persist();
    }

    private static void Persist()
    {
        if (_file == null)
            return;

        try
        {
            _file.SetValue(Category, "ChaosEnabled", ChaosEnabled);
            _file.SetValue(Category, "ChaosRadius", ChaosRadius);
            _file.SetValue(Category, "ChaosTurboPower", ChaosTurboPower);
            _file.Save();
        }
        catch (System.Exception ex)
        {
            ModLog.WriteException("ModSettings.Persist", ex);
        }
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
