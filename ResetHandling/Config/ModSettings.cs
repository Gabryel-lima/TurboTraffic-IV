using GTA;

public static class ModSettings
{
    // ATENÇÃO ao ler o .ini gerado: ele sai "de cabeça para baixo", com o nome
    // da opção como seção e "TurboTraffic" como chave:
    //
    //     [ChaosRadius]
    //     TurboTraffic=140.0000
    //
    // Não é bug. A assinatura da API do ScriptHookDotNet é
    // GetValue(chave, seção, padrão) — chave primeiro, seção depois — e não o
    // (seção, chave) habitual de arquivos .ini. Leitura e escrita usam a mesma
    // ordem, então o round-trip é correto e os valores persistem. Trocar a
    // ordem agora invalidaria todos os .ini já salvos dos jogadores.
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

    public const float ChaosMagnetPullMin = 10.0f;
    public const float ChaosMagnetPullMax = 300.0f;
    public const float ChaosMagnetPullStep = 10.0f;

    // Empurrão do Modo Solto é bem menor que o do Legado de propósito: sem IA
    // segurando o carro, o impulso acumula no vetor inteiro a cada frame e
    // escala muito mais rápido.
    public const float ChaosFreeRidePushMin = 5.0f;
    public const float ChaosFreeRidePushMax = 200.0f;
    public const float ChaosFreeRidePushStep = 5.0f;

    private static SettingsFile _file;

    // Raio de alcance é global: vale para qualquer modo ativo, não é
    // duplicado por modo — evita que o menu infle a cada modo novo.
    public static float ChaosRadius { get; private set; }

    // Se os modos de movimento também alcançam veículos marcados como
    // necessários para missão (o carro perseguido, o alvo, o comparsa).
    //
    // Vale SÓ para os impulsos. A invencibilidade e a limpeza de detritos
    // continuam respeitando a marcação de missão em qualquer caso: empurrar o
    // carro do alvo muda a perseguição, mas blindá-lo ou apagá-lo trava a
    // missão de vez, sem volta.
    public static bool ChaosAffectMissionVehicles { get; private set; }

    // Se os impulsos também alcançam veículos SEM ninguém ao volante.
    //
    // Ligado, todo veículo do raio carrega o efeito — inclusive o carro que
    // acabou de nascer no trânsito e cujo motorista o jogo ainda não colocou
    // no banco, que era um dos casos em que o impulso parecia "não pegar".
    // O preço é que carro estacionado também sai voando.
    public static bool ChaosAffectDriverlessVehicles { get; private set; }

    // Modo Legado: empurrão aditivo e cumulativo (v = v + forward*Força),
    // sem teto. Independente do Modo Velocidade Máxima abaixo.
    public static bool ChaosEnabled { get; private set; }
    public static float ChaosTurboPower { get; private set; }

    // Modo Velocidade Máxima: mecânica DIFERENTE de propósito — fixa a
    // velocidade direto no alvo (v = forward*Alvo), sem acumular. Ligado
    // e desligado de forma totalmente independente do Modo Legado.
    public static bool ChaosMaxSpeedModeEnabled { get; private set; }
    public static float ChaosMaxSpeed { get; private set; }

    // Modo Ímã: terceira mecânica, também independente das outras duas. Não
    // decide velocidade — reescreve só a DIREÇÃO do veículo para apontar ao
    // jogador. "Força de atração" é o piso de velocidade usado quando o carro
    // está lento demais para ser puxado sozinho.
    public static bool ChaosMagnetModeEnabled { get; private set; }
    public static float ChaosMagnetPull { get; private set; }

    // Modo Solto: desliga a IA de trajeto do motorista e empurra o carro sem
    // recolocá-lo no eixo frontal, deixando a física resolver o resto.
    public static bool ChaosFreeRideModeEnabled { get; private set; }
    public static float ChaosFreeRidePush { get; private set; }

    // Dano é global: vale para qualquer veículo tocado por qualquer modo
    // ativo, não pertence a um modo específico.
    public static bool ChaosInvincible { get; private set; }
    public static bool ChaosCrumple { get; private set; }

    // Limpeza de detritos: apaga sucata fora do campo de visão para liberar
    // vagas de tráfego, mantendo o fluxo de veículos novos constante em vez de
    // deixar a rua entupida de carcaças.
    public static bool ChaosDebrisCleanup { get; private set; }

    // Invencibilidade do JOGADOR — não confundir com ChaosInvincible, que vale
    // para os veículos de NPC no raio. Esta é sobre o personagem: com ela
    // ligada, ele não perde vida e não morre.
    public static bool PlayerInvincible { get; private set; }

    public static void Load()
    {
        // Defaults usados caso o .ini ainda não exista. O caos no trânsito nasce
        // DESLIGADO: o mod só passa a agir depois que o jogador ligar pelo HUD.
        ChaosRadius = 120.0f;

        // Nasce LIGADA: a perseguição é justamente onde o caos no trânsito faz
        // mais graça, e deixar o carro do alvo imune ao turbo destoava de tudo
        // o mais que se move na tela.
        ChaosAffectMissionVehicles = true;

        // Nasce LIGADA: "forçar todos os veículos" é o comportamento pedido —
        // nenhum carro do raio deve ficar de fora do efeito por um detalhe de
        // quem está (ou ainda não está) no banco do motorista.
        ChaosAffectDriverlessVehicles = true;

        ChaosEnabled = false;
        ChaosTurboPower = 70.0f;
        ChaosMaxSpeedModeEnabled = false;
        ChaosMaxSpeed = 150.0f;
        ChaosMagnetModeEnabled = false;
        ChaosMagnetPull = 80.0f;
        ChaosFreeRideModeEnabled = false;
        ChaosFreeRidePush = 25.0f;
        ChaosInvincible = false;

        // "Amassar" nasce LIGADO porque ligado é o comportamento padrão do
        // jogo: desligado (o default antigo) fazia todo veículo tocado virar
        // lataria indeformável sem o jogador ter pedido nada.
        ChaosCrumple = true;

        // Limpeza de detritos nasce LIGADA: é otimização, não efeito.
        ChaosDebrisCleanup = true;

        // Invencibilidade do jogador nasce DESLIGADA: tira toda a dificuldade
        // do jogo, então tem que ser uma escolha explícita.
        PlayerInvincible = false;

        try
        {
            _file = SettingsFile.Open(ModPaths.SettingsFilePath);
            _file.Load();

            ChaosRadius = Clamp(_file.GetValueFloat(Category, "ChaosRadius", 120.0f), ChaosRadiusMin, ChaosRadiusMax);
            ChaosAffectMissionVehicles = _file.GetValueBool(Category, "ChaosAffectMissionVehicles", true);
            ChaosAffectDriverlessVehicles = _file.GetValueBool(Category, "ChaosAffectDriverlessVehicles", true);
            ChaosEnabled = _file.GetValueBool(Category, "ChaosEnabled", false);
            ChaosTurboPower = Clamp(_file.GetValueFloat(Category, "ChaosTurboPower", 70.0f), ChaosTurboPowerMin, ChaosTurboPowerMax);
            ChaosMaxSpeedModeEnabled = _file.GetValueBool(Category, "ChaosMaxSpeedModeEnabled", false);
            ChaosMaxSpeed = Clamp(_file.GetValueFloat(Category, "ChaosMaxSpeed", 150.0f), ChaosMaxSpeedMin, ChaosMaxSpeedMax);
            ChaosMagnetModeEnabled = _file.GetValueBool(Category, "ChaosMagnetModeEnabled", false);
            ChaosMagnetPull = Clamp(_file.GetValueFloat(Category, "ChaosMagnetPull", 80.0f), ChaosMagnetPullMin, ChaosMagnetPullMax);
            ChaosFreeRideModeEnabled = _file.GetValueBool(Category, "ChaosFreeRideModeEnabled", false);
            ChaosFreeRidePush = Clamp(_file.GetValueFloat(Category, "ChaosFreeRidePush", 25.0f), ChaosFreeRidePushMin, ChaosFreeRidePushMax);
            ChaosInvincible = _file.GetValueBool(Category, "ChaosInvincible", false);
            ChaosCrumple = _file.GetValueBool(Category, "ChaosCrumple", true);
            ChaosDebrisCleanup = _file.GetValueBool(Category, "ChaosDebrisCleanup", true);
            PlayerInvincible = _file.GetValueBool(Category, "PlayerInvincible", false);
        }
        catch (System.Exception ex)
        {
            _file = null;
            ModLog.WriteException("ModSettings.Load", ex);
        }

        ModLog.Write("Config carregada: raio=" + ChaosRadius
            + " afetaMissao=" + ChaosAffectMissionVehicles + " afetaSemMotorista=" + ChaosAffectDriverlessVehicles
            + " legado=" + ChaosEnabled + " forca=" + ChaosTurboPower
            + " modoVelMax=" + ChaosMaxSpeedModeEnabled + " velAlvo=" + ChaosMaxSpeed
            + " modoIma=" + ChaosMagnetModeEnabled + " atracao=" + ChaosMagnetPull
            + " modoSolto=" + ChaosFreeRideModeEnabled + " impulso=" + ChaosFreeRidePush
            + " invencivel=" + ChaosInvincible + " amassa=" + ChaosCrumple
            + " limpaDetritos=" + ChaosDebrisCleanup
            + " playerInvencivel=" + PlayerInvincible);
    }

    public static void ToggleChaosEnabled()
    {
        ChaosEnabled = !ChaosEnabled;
        Persist();
    }

    public static void ToggleChaosAffectDriverlessVehicles()
    {
        ChaosAffectDriverlessVehicles = !ChaosAffectDriverlessVehicles;
        Persist();
    }

    public static void ToggleChaosAffectMissionVehicles()
    {
        ChaosAffectMissionVehicles = !ChaosAffectMissionVehicles;
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

    public static void ToggleChaosMagnetModeEnabled()
    {
        ChaosMagnetModeEnabled = !ChaosMagnetModeEnabled;
        Persist();
    }

    public static void AdjustChaosMagnetPull(float delta)
    {
        ChaosMagnetPull = Clamp(ChaosMagnetPull + delta, ChaosMagnetPullMin, ChaosMagnetPullMax);
        Persist();
    }

    public static void ToggleChaosFreeRideModeEnabled()
    {
        ChaosFreeRideModeEnabled = !ChaosFreeRideModeEnabled;
        Persist();
    }

    public static void AdjustChaosFreeRidePush(float delta)
    {
        ChaosFreeRidePush = Clamp(ChaosFreeRidePush + delta, ChaosFreeRidePushMin, ChaosFreeRidePushMax);
        Persist();
    }

    public static void ToggleChaosDebrisCleanup()
    {
        ChaosDebrisCleanup = !ChaosDebrisCleanup;
        Persist();
    }

    public static void TogglePlayerInvincible()
    {
        PlayerInvincible = !PlayerInvincible;
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
            _file.SetValue(Category, "ChaosAffectMissionVehicles", ChaosAffectMissionVehicles);
            _file.SetValue(Category, "ChaosAffectDriverlessVehicles", ChaosAffectDriverlessVehicles);
            _file.SetValue(Category, "ChaosEnabled", ChaosEnabled);
            _file.SetValue(Category, "ChaosTurboPower", ChaosTurboPower);
            _file.SetValue(Category, "ChaosMaxSpeedModeEnabled", ChaosMaxSpeedModeEnabled);
            _file.SetValue(Category, "ChaosMaxSpeed", ChaosMaxSpeed);
            _file.SetValue(Category, "ChaosMagnetModeEnabled", ChaosMagnetModeEnabled);
            _file.SetValue(Category, "ChaosMagnetPull", ChaosMagnetPull);
            _file.SetValue(Category, "ChaosFreeRideModeEnabled", ChaosFreeRideModeEnabled);
            _file.SetValue(Category, "ChaosFreeRidePush", ChaosFreeRidePush);
            _file.SetValue(Category, "ChaosInvincible", ChaosInvincible);
            _file.SetValue(Category, "ChaosCrumple", ChaosCrumple);
            _file.SetValue(Category, "ChaosDebrisCleanup", ChaosDebrisCleanup);
            _file.SetValue(Category, "PlayerInvincible", PlayerInvincible);
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
