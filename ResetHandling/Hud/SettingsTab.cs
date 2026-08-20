using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public class SettingsTab : IHudTab
{
    private static readonly string[] Rows = { "Caos no trânsito", "Raio de alcance", "Força de aceleração" };

    private const float ValueColumnOffset = 340f;

    private int _selectedIndex;

    public void MoveSelection(int direction)
    {
        int count = Rows.Length;
        _selectedIndex = ((_selectedIndex + direction) % count + count) % count;
    }

    public void Secondary() { }

    public void Confirm()
    {
        if (_selectedIndex == 0)
            ModSettings.ToggleChaosEnabled();
    }

    public void AdjustValue(int direction)
    {
        switch (_selectedIndex)
        {
            case 0:
                ModSettings.ToggleChaosEnabled();
                break;
            case 1:
                ModSettings.AdjustChaosRadius(direction * ModSettings.ChaosRadiusStep);
                break;
            case 2:
                ModSettings.AdjustChaosTurboPower(direction * ModSettings.ChaosTurboPowerStep);
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
            case 0: return ModSettings.ChaosEnabled ? "Ativado" : "Desativado";
            case 1: return ModSettings.ChaosRadius.ToString("0") + " m";
            case 2: return ModSettings.ChaosTurboPower.ToString("0");
            default: return string.Empty;
        }
    }
}
