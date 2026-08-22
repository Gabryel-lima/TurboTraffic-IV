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
    private const int RowInvincible = 5;
    private const int RowCrumple = 6;

    private static readonly string[] Rows =
    {
        "Raio de alcance",
        "Modo Legado (empurrão)", "Força de aceleração",
        "Modo Velocidade Máxima", "Velocidade alvo",
        "Veículos invencíveis", "Permitir amassar"
    };

    private const float ValueColumnOffset = 340f;

    private int _selectedIndex;

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
            case RowInvincible:
                ModSettings.ToggleChaosInvincible();
                break;
            case RowCrumple:
                ModSettings.ToggleChaosCrumple();
                break;
        }
    }

    public void AdjustValue(int direction)
    {
        switch (_selectedIndex)
        {
            case RowRadius:
                ModSettings.AdjustChaosRadius(direction * ModSettings.ChaosRadiusStep);
                break;
            case RowLegacyEnabled:
                ModSettings.ToggleChaosEnabled();
                break;
            case RowLegacyPower:
                ModSettings.AdjustChaosTurboPower(direction * ModSettings.ChaosTurboPowerStep);
                break;
            case RowMaxSpeedEnabled:
                ModSettings.ToggleChaosMaxSpeedModeEnabled();
                break;
            case RowMaxSpeedTarget:
                ModSettings.AdjustChaosMaxSpeed(direction * ModSettings.ChaosMaxSpeedStep);
                break;
            case RowInvincible:
                ModSettings.ToggleChaosInvincible();
                break;
            case RowCrumple:
                ModSettings.ToggleChaosCrumple();
                break;
        }
    }

    public void Draw(Graphics g, Font font, float x, float y, float width, float height)
    {
        float lineY = y;

        for (int i = 0; i < Rows.Length; i++)
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

        HudTheme.DrawTabHint(g, font, x, y, height, "←→ ajustar     Enter alternar liga/desliga");
    }

    public static float MeasureHeight()
    {
        return (Rows.Length * HudTheme.RowHeight) + HudTheme.TabHintHeight;
    }

    private string GetValueLabel(int index)
    {
        switch (index)
        {
            case RowRadius: return ModSettings.ChaosRadius.ToString("0") + " m";
            case RowLegacyEnabled: return ModSettings.ChaosEnabled ? "Ativado" : "Desativado";
            case RowLegacyPower: return ModSettings.ChaosTurboPower.ToString("0");
            case RowMaxSpeedEnabled: return ModSettings.ChaosMaxSpeedModeEnabled ? "Ativado" : "Desativado";
            case RowMaxSpeedTarget: return ModSettings.ChaosMaxSpeed.ToString("0");
            case RowInvincible: return ModSettings.ChaosInvincible ? "Ativado" : "Desativado";
            case RowCrumple: return ModSettings.ChaosCrumple ? "Ativado" : "Desativado";
            default: return string.Empty;
        }
    }
}
