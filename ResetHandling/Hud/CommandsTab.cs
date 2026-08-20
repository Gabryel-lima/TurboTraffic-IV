using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public class CommandsTab : IHudTab
{
    // A navegação do HUD é listada aqui de propósito: com o esquema de abas no
    // Tab, o jogador não descobre a troca de aba sozinho se ela não estiver
    // escrita em algum lugar visível.
    private static readonly string[][] NavigationRows =
    {
        new[] { "Tab / Shift+Tab", "Trocar de aba" },
        new[] { "↑ / ↓", "Navegar os itens da aba" },
        new[] { "← / →", "Ajustar o valor selecionado" },
        new[] { "Enter", "Confirmar (spawnar / alternar)" },
        new[] { "Espaço", "Ação secundária da aba" },
        new[] { "Esc", "Fechar o HUD" },
    };

    private const float DescColumnOffset = 230f;

    public void MoveSelection(int direction) { }
    public void AdjustValue(int direction) { }
    public void Confirm() { }
    public void Secondary() { }

    public void Draw(Graphics g, Font font, float x, float y, float width, float height)
    {
        float labelColumn = x;
        float descColumn = x + DescColumnOffset;
        float lineY = y;

        HudTheme.DrawCell(g, font, labelColumn, lineY, HudTheme.SectionTitleHeight, "TECLAS DO MOD", HudTheme.SectionText);
        lineY += HudTheme.SectionTitleHeight;

        foreach (KeybindEntry entry in KeybindRegistry.Entries)
        {
            HudTheme.DrawCell(g, font, labelColumn, lineY, entry.Label, HudTheme.ValueText);
            HudTheme.DrawCell(g, font, descColumn, lineY, entry.Description, HudTheme.NormalText);
            lineY += HudTheme.RowHeight;
        }

        lineY += HudTheme.SectionGap;
        HudTheme.DrawCell(g, font, labelColumn, lineY, HudTheme.SectionTitleHeight, "NAVEGAÇÃO DO HUD", HudTheme.SectionText);
        lineY += HudTheme.SectionTitleHeight;

        foreach (string[] row in NavigationRows)
        {
            HudTheme.DrawCell(g, font, labelColumn, lineY, row[0], HudTheme.ValueText);
            HudTheme.DrawCell(g, font, descColumn, lineY, row[1], HudTheme.NormalText);
            lineY += HudTheme.RowHeight;
        }
    }

    // Altura total que esta aba ocupa — usada pela verificação de layout para
    // garantir que o conteúdo cabe na área disponível.
    public static float MeasureHeight()
    {
        return HudTheme.SectionTitleHeight
             + (KeybindRegistry.Entries.Length * HudTheme.RowHeight)
             + HudTheme.SectionGap
             + HudTheme.SectionTitleHeight
             + (NavigationRows.Length * HudTheme.RowHeight);
    }
}
