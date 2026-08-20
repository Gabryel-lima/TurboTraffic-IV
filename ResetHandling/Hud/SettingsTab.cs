using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public class SettingsTab : IHudTab
{
    private static readonly string[] Rows = { "Caos no trânsito", "Raio de alcance", "Força de aceleração" };

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
            string prefix = selected ? "> " : "   ";
            Color color = selected ? Color.Yellow : Color.White;
            g.DrawText(prefix + Rows[i] + ": " + GetValueLabel(i), x, lineY, color, font);
            lineY += 20f;
        }

        g.DrawText("Enter ou ←→: alternar caos / ajustar valor", x, y + height - 16f, Color.Gainsboro, font);
    }

    private string GetValueLabel(int index)
    {
        switch (index)
        {
            case 0: return ModSettings.ChaosEnabled ? "Ativado" : "Desativado";
            case 1: return ModSettings.ChaosRadius.ToString("0") + "m";
            case 2: return ModSettings.ChaosTurboPower.ToString("0");
            default: return string.Empty;
        }
    }
}
