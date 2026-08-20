using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

// Métricas e cores compartilhadas pelas abas, para que todas fiquem alinhadas
// entre si e para que a altura ocupada seja calculável (nenhuma aba pode
// transbordar a área de conteúdo do painel).
//
// Todo texto é desenhado com o overload DrawText(texto, x, y, cor, fonte) — ver
// TextMeasure.cs para o motivo de não usar os overloads baseados em
// RectangleF/TextAlignment.
public static class HudTheme
{
    public const float RowHeight = 34f;
    public const float SectionTitleHeight = 34f;
    public const float SectionGap = 12f;
    public const float TabHintHeight = 30f;

    // Recuo horizontal da faixa de seleção além do texto.
    public const float RowTextInset = 10f;

    // Fundos sólidos (alpha 255) para o texto nunca competir com a cena do
    // jogo por trás — um fundo translúcido some contraste conforme o cenário.
    public static readonly Color SelectionBg = Color.FromArgb(255, 235, 140, 0);
    public static readonly Color SelectionText = Color.Black;
    public static readonly Color NormalText = Color.White;
    public static readonly Color SectionText = Color.FromArgb(255, 255, 186, 92);
    public static readonly Color MutedText = Color.Gainsboro;
    public static readonly Color ValueText = Color.FromArgb(255, 150, 220, 255);

    // ------------------------------------------------------------------------
    // Preenchimento de retângulo — SEMPRE use este helper, nunca
    // g.DrawRectangle direto.
    //
    // A assinatura real da API é:
    //     DrawRectangle(Single CenterX, Single CenterY, Single Width, Single Height, Color)
    // Ou seja, os dois primeiros argumentos são o CENTRO do retângulo, não o
    // canto superior esquerdo (é o mesmo contrato da native DRAW_RECT do GTA).
    // Passar o canto superior esquerdo desenha tudo deslocado para cima e para
    // a esquerda por METADE do tamanho do próprio retângulo — foi exatamente
    // isso que fazia o painel sair pela borda da tela e as faixas das abas
    // ficarem fora de registro com o texto (que é desenhado por DrawText, esse
    // sim a partir do canto superior esquerdo).
    //
    // Este helper recebe canto superior esquerdo + tamanho, como o resto do
    // layout do HUD assume, e converte para centro na hora de chamar a API.
    public static void FillRect(Graphics g, float left, float top, float width, float height, Color color)
    {
        g.DrawRectangle(left + (width / 2f), top + (height / 2f), width, height, color);
    }

    // Quantas linhas de RowHeight cabem na altura útil de uma aba (descontando
    // a faixa de dica no rodapé da aba).
    public static int MaxRows(float contentHeight)
    {
        float usable = contentHeight - TabHintHeight;
        int rows = (int)(usable / RowHeight);
        return rows < 1 ? 1 : rows;
    }

    // Centraliza verticalmente dentro de uma faixa de altura 'height', usando a
    // altura de linha real da fonte (Font.GetLineHeight) — dado da própria API
    // do jogo, não um valor chutado.
    public static void DrawCell(Graphics g, Font font, float x, float y, float height, string text, Color color)
    {
        float lineHeight = font.GetLineHeight(FontScaling.Pixel);
        float textY = y + ((height - lineHeight) / 2f);
        g.DrawText(text, x, textY, color, font);
    }

    public static void DrawCell(Graphics g, Font font, float x, float y, string text, Color color)
    {
        DrawCell(g, font, x, y, RowHeight, text, color);
    }

    // Centraliza nos dois eixos dentro de uma caixa width x height. A largura
    // do texto vem de TextMeasure (System.Drawing), não de RectangleF/DrawText.
    public static void DrawCentered(Graphics g, Font font, float x, float y, float width, float height,
        float fontSizePx, bool bold, string text, Color color)
    {
        float textWidth = TextMeasure.Width(text, fontSizePx, bold);
        float lineHeight = font.GetLineHeight(FontScaling.Pixel);
        float textX = x + System.Math.Max(0f, (width - textWidth) / 2f);
        float textY = y + ((height - lineHeight) / 2f);
        g.DrawText(text, textX, textY, color, font);
    }

    // Desenha uma linha de lista, com faixa de destaque quando selecionada.
    public static void DrawRow(Graphics g, Font font, float x, float y, float width, string text, bool selected)
    {
        if (selected)
            FillRect(g, x - RowTextInset, y, width + (RowTextInset * 2f), RowHeight, SelectionBg);

        DrawCell(g, font, x, y, text, selected ? SelectionText : NormalText);
    }

    public static void DrawTabHint(Graphics g, Font font, float x, float contentY, float contentHeight, string text)
    {
        DrawCell(g, font, x, contentY + contentHeight - TabHintHeight, TabHintHeight, text, MutedText);
    }
}
