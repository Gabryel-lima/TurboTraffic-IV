using System.Drawing;

// Mede a largura de um texto usando System.Drawing — não depende do
// renderizador nativo do jogo, só do medidor de fontes do próprio .NET.
//
// Os overloads de GTA.Graphics.DrawText baseados em RectangleF/TextAlignment
// (usados numa tentativa anterior para centralizar o texto das abas) faziam o
// texto ser desenhado fora da caixa esperada — muito provavelmente porque o
// wrapper nativo do ScriptHookDotNet espera uma estrutura Left/Top/Right/Bottom
// e recebe, sem conversão, um RectangleF gerenciado (X/Y/Width/Height), que tem
// layout de campos diferente. Sem acesso ao código-fonte do wrapper para
// confirmar, a decisão segura é não depender desse overload: a centralização é
// calculada aqui, e o texto é sempre desenhado com o overload simples
// DrawText(texto, x, y, cor, fonte), que é o que já funcionava antes.
public static class TextMeasure
{
    private static readonly Bitmap Surface = new Bitmap(1, 1);
    private static readonly System.Drawing.Graphics Measurer = System.Drawing.Graphics.FromImage(Surface);

    public static float Width(string text, float sizePx, bool bold)
    {
        FontStyle style = bold ? FontStyle.Bold : FontStyle.Regular;
        using (Font f = new Font("Arial", sizePx, style, GraphicsUnit.Pixel))
        {
            return Measurer.MeasureString(text, f).Width;
        }
    }
}
