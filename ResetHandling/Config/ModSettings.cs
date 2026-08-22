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

    public const float ChaosMaxSpeedMin = 10.0f;
    public const float ChaosMaxSpeedMax = 400.0f;
    public const float ChaosMaxSpeedStep = 10.0f;

    private static SettingsFile _file;

    // Raio de alcance é global: vale para qualquer modo ativo, não é
    // duplicado por modo — evita que o menu infle a cada modo novo.
    public static float ChaosRadius { get; private set; }

    // Modo Legado: empurrão aditivo e cumulativo (v = v + forward*Força),
    // sem teto. Independente do Modo Velocidade Máxima abaixo.
    public static bool ChaosEnabled { get; private set; }
    public static float ChaosTurboPower { get; private set; }

    // Modo Velocidade Máxima: mecânica DIFERENTE de propósito — fixa a
    // velocidade direto no alvo (v = forward*Alvo), sem acumular. Ligado
    // e desligado de forma totalmente independente do Modo Legado.
    public static bool ChaosMaxSpeedModeEnabled { get; private set; }
    public static float ChaosMaxSpeed { get; private set; }

    // Dano é global: vale para qualquer veículo tocado por qualquer modo
    // ativo, não pertence a um modo específico.
    public static bool ChaosInvincible { get; private set; }
    public static bool ChaosCrumple { get; private set; }

    public static void Load()
    {
        // Defaults usados caso o .ini ainda não exista. O caos no trânsito nasce
        // DESLIGADO: o mod só passa a agir depois que o jogador ligar pelo HUD.
        ChaosRadius = 120.0f;
        ChaosEnabled = false;
        ChaosTurboPower = 70.0f;
        ChaosMaxSpeedModeEnabled = false;
        ChaosMaxSpeed = 150.0f;
        ChaosInvincible = false;
        ChaosCrumple = false;

        try
        {
            _file = SettingsFile.Open(ModPaths.SettingsFilePath);
            _file.Load();

            ChaosRadius = Clamp(_file.GetValueFloat(Category, "ChaosRadius", 120.0f), ChaosRadiusMin, ChaosRadiusMax);
            ChaosEnabled = _file.GetValueBool(Category, "ChaosEnabled", false);
            ChaosTurboPower = Clamp(_file.GetValueFloat(Category, "ChaosTurboPower", 70.0f), ChaosTurboPowerMin, ChaosTurboPowerMax);
            ChaosMaxSpeedModeEnabled = _file.GetValueBool(Category, "ChaosMaxSpeedModeEnabled", false);
            ChaosMaxSpeed = Clamp(_file.GetValueFloat(Category, "ChaosMaxSpeed", 150.0f), ChaosMaxSpeedMin, ChaosMaxSpeedMax);
            ChaosInvincible = _file.GetValueBool(Category, "ChaosInvincible", false);
            ChaosCrumple = _file.GetValueBool(Category, "ChaosCrumple", false);
        }
        catch (System.Exception ex)
        {
            _file = null;
            ModLog.WriteException("ModSettings.Load", ex);
        }

        ModLog.Write("Config carregada: raio=" + ChaosRadius
            + " legado=" + ChaosEnabled + " forca=" + ChaosTurboPower
            + " modoVelMax=" + ChaosMaxSpeedModeEnabled + " velAlvo=" + ChaosMaxSpeed
            + " invencivel=" + ChaosInvincible + " amassa=" + ChaosCrumple);
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

    public static void ToggleChaosMaxSpeedModeEnabled()
    {
        ChaosMaxSpeedModeEnabled = !ChaosMaxSpeedModeEnabled;
        Persist();
    }

    public static void ToggleChaosInvincible()
    {
        ChaosInvincible = !ChaosInvincible;
        Persist();
    }

    public static void ToggleChaosCrumple()
    {
        ChaosCrumple = !ChaosCrumple;
        Persist();
    }

    public static void AdjustChaosMaxSpeed(float delta)
    {
        ChaosMaxSpeed = Clamp(ChaosMaxSpeed + delta, ChaosMaxSpeedMin, ChaosMaxSpeedMax);
        Persist();
    }

    private static void Persist()
    {
        if (_file == null)
            return;

        try
        {
            _file.SetValue(Category, "ChaosRadius", ChaosRadius);
            _file.SetValue(Category, "ChaosEnabled", ChaosEnabled);
            _file.SetValue(Category, "ChaosTurboPower", ChaosTurboPower);
            _file.SetValue(Category, "ChaosMaxSpeedModeEnabled", ChaosMaxSpeedModeEnabled);
            _file.SetValue(Category, "ChaosMaxSpeed", ChaosMaxSpeed);
            _file.SetValue(Category, "ChaosInvincible", ChaosInvincible);
            _file.SetValue(Category, "ChaosCrumple", ChaosCrumple);
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
