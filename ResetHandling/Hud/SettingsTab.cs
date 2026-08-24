using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public class SettingsTab : IHudTab
{
    // Índices fixos usados em Confirm/AdjustValue/GetValueLabel — mantidos
    // como consts para não perder a correspondência ao reordenar linhas.
    private const int RowRadius = 0;
    private const int RowLegacyEnabled = 1;
    private const int RowLegacyPower = 2;
    private const int RowMaxSpeedEnabled = 3;
    private const int RowMaxSpeedTarget = 4;
    private const int RowMagnetEnabled = 5;
    private const int RowMagnetPull = 6;
    private const int RowFreeRideEnabled = 7;
    private const int RowFreeRidePush = 8;
    private const int RowInvincible = 9;
    private const int RowCrumple = 10;
    private const int RowDebrisCleanup = 11;

    private static readonly string[] Rows =
    {
        "Raio de alcance",
        "Modo Legado (empurrão)", "Força de aceleração",
        "Modo Velocidade Máxima", "Velocidade alvo",
        "Modo Ímã (perseguição)", "Força de atração",
        "Modo Solto (sem IA)", "Impulso do modo solto",
        "Veículos invencíveis", "Permitir amassar",
        "Limpar detritos fora de vista"
    };

    private const float ValueColumnOffset = 340f;

    private int _selectedIndex;
    private int _scrollOffset;

    public void MoveSelection(int direction)
    {
        int count = Rows.Length;
        _selectedIndex = ((_selectedIndex + direction) % count + count) % count;
    }

    public void Secondary() { }

    public void Update() { }

    public void Confirm()
    {
        switch (_selectedIndex)
        {
            case RowLegacyEnabled:
                ModSettings.ToggleChaosEnabled();
                break;
            case RowMaxSpeedEnabled:
                ModSettings.ToggleChaosMaxSpeedModeEnabled();
                break;
            case RowMagnetEnabled:
                ModSettings.ToggleChaosMagnetModeEnabled();
                break;
            case RowFreeRideEnabled:
                ModSettings.ToggleChaosFreeRideModeEnabled();
                break;
            case RowInvincible:
                ModSettings.ToggleChaosInvincible();
                break;
            case RowCrumple:
                ModSettings.ToggleChaosCrumple();
                break;
            case RowDebrisCleanup:
                ModSettings.ToggleChaosDebrisCleanup();
                break;
        }
    }

    // Nas linhas de liga/desliga, ← e → não podem fazer a mesma coisa: alternar
    // nas duas direções faz segurar ← piscar o valor em vez de desligá-lo. Como
    // ModSettings só expõe Toggle, o alvo é comparado com o estado atual e o
    // toggle só é chamado quando há de fato o que mudar — assim → sempre liga,
    // ← sempre desliga, e repetir a mesma tecla não desfaz o efeito.
    private static void SetBool(bool current, bool target, System.Action toggle)
    {
        if (current != target)
            toggle();
    }

    public void AdjustValue(int direction)
    {
        bool target = direction > 0;

        switch (_selectedIndex)
        {
            case RowRadius:
                ModSettings.AdjustChaosRadius(direction * ModSettings.ChaosRadiusStep);
                break;
            case RowLegacyEnabled:
                SetBool(ModSettings.ChaosEnabled, target, ModSettings.ToggleChaosEnabled);
                break;
            case RowLegacyPower:
                ModSettings.AdjustChaosTurboPower(direction * ModSettings.ChaosTurboPowerStep);
                break;
            case RowMaxSpeedEnabled:
                SetBool(ModSettings.ChaosMaxSpeedModeEnabled, target, ModSettings.ToggleChaosMaxSpeedModeEnabled);
                break;
            case RowMaxSpeedTarget:
                ModSettings.AdjustChaosMaxSpeed(direction * ModSettings.ChaosMaxSpeedStep);
                break;
            case RowMagnetEnabled:
                SetBool(ModSettings.ChaosMagnetModeEnabled, target, ModSettings.ToggleChaosMagnetModeEnabled);
                break;
            case RowMagnetPull:
                ModSettings.AdjustChaosMagnetPull(direction * ModSettings.ChaosMagnetPullStep);
                break;
            case RowFreeRideEnabled:
                SetBool(ModSettings.ChaosFreeRideModeEnabled, target, ModSettings.ToggleChaosFreeRideModeEnabled);
                break;
            case RowFreeRidePush:
                ModSettings.AdjustChaosFreeRidePush(direction * ModSettings.ChaosFreeRidePushStep);
                break;
            case RowInvincible:
                SetBool(ModSettings.ChaosInvincible, target, ModSettings.ToggleChaosInvincible);
                break;
            case RowCrumple:
                SetBool(ModSettings.ChaosCrumple, target, ModSettings.ToggleChaosCrumple);
                break;
            case RowDebrisCleanup:
                SetBool(ModSettings.ChaosDebrisCleanup, target, ModSettings.ToggleChaosDebrisCleanup);
                break;
        }
    }

    public void Draw(Graphics g, Font font, float x, float y, float width, float height)
    {
        // Mesma rolagem da aba de spawn: a lista de opções cresce a cada modo
        // novo e já não cabe na área de conteúdo, então desenha-se só a janela
        // que cabe, com o item selecionado sempre dentro dela.
        int maxRows = HudTheme.MaxRows(height);

        if (_selectedIndex < _scrollOffset)
            _scrollOffset = _selectedIndex;
        if (_selectedIndex >= _scrollOffset + maxRows)
            _scrollOffset = _selectedIndex - maxRows + 1;

        int maxOffset = Rows.Length - maxRows;
        if (maxOffset < 0) maxOffset = 0;
        if (_scrollOffset > maxOffset) _scrollOffset = maxOffset;
        if (_scrollOffset < 0) _scrollOffset = 0;

        int end = _scrollOffset + maxRows;
        if (end > Rows.Length) end = Rows.Length;

        float lineY = y;

        for (int i = _scrollOffset; i < end; i++)
        {
            bool selected = i == _selectedIndex;

            if (selected)
                HudTheme.FillRect(g, x - HudTheme.RowTextInset, lineY, width + (HudTheme.RowTextInset * 2f), HudTheme.RowHeight, HudTheme.SelectionBg);

            Color textColor = selected ? HudTheme.SelectionText : HudTheme.NormalText;
            Color valueColor = selected ? HudTheme.SelectionText : HudTheme.ValueText;

            HudTheme.DrawCell(g, font, x, lineY, Rows[i], textColor);
            HudTheme.DrawCell(g, font, x + ValueColumnOffset, lineY, GetValueLabel(i), valueColor);

            lineY += HudTheme.RowHeight;
        }

        // Contador no rodapé: com rolagem, sem ele não dá para saber que existe
        // mais opção fora da tela.
        HudTheme.DrawTabHint(g, font, x, y, height,
            "←→ ajustar     Enter alternar liga/desliga     "
            + (_selectedIndex + 1) + "/" + Rows.Length);
    }

    private string GetValueLabel(int index)
    {
        switch (index)
        {
            case RowRadius: return ModSettings.ChaosRadius.ToString("0") + " m";
            case RowLegacyEnabled: return ModSettings.ChaosEnabled ? "Ativado" : "Desativado";

            // Força e impulso são somados à velocidade por segundo, não uma
            // velocidade final: mostrar só o número não dizia de que grandeza
            // se tratava, e "Velocidade alvo 400" parecia um valor plausível
            // quando na verdade são 1440 km/h.
            case RowLegacyPower: return ModSettings.ChaosTurboPower.ToString("0") + " m/s por seg";
            case RowMaxSpeedEnabled: return ModSettings.ChaosMaxSpeedModeEnabled ? "Ativado" : "Desativado";
            case RowMaxSpeedTarget: return SpeedLabel(ModSettings.ChaosMaxSpeed);
            case RowMagnetEnabled: return ModSettings.ChaosMagnetModeEnabled ? "Ativado" : "Desativado";
            case RowMagnetPull: return SpeedLabel(ModSettings.ChaosMagnetPull);
            case RowFreeRideEnabled: return ModSettings.ChaosFreeRideModeEnabled ? "Ativado" : "Desativado";
            case RowFreeRidePush: return ModSettings.ChaosFreeRidePush.ToString("0") + " m/s por seg";
            case RowInvincible: return ModSettings.ChaosInvincible ? "Ativado" : "Desativado";
            case RowCrumple: return ModSettings.ChaosCrumple ? "Ativado" : "Desativado";
            case RowDebrisCleanup: return ModSettings.ChaosDebrisCleanup ? "Ativado" : "Desativado";
            default: return string.Empty;
        }
    }

    // Velocidade real do jogo é m/s; km/h vem junto porque é a unidade em que
    // o jogador pensa ao decidir "rápido demais" ou não.
    private static string SpeedLabel(float metersPerSecond)
    {
        return metersPerSecond.ToString("0") + " m/s  (" + (metersPerSecond * 3.6f).ToString("0") + " km/h)";
    }
}
