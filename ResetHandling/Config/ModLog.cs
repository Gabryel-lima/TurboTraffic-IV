using System;
using System.IO;

// Log de diagnóstico gravado em scripts\TurboTrafficIV.log (dentro da pasta do
// jogo). Serve para descobrir, sem depender de nada desenhado na tela, se o
// script carregou, se as teclas estão chegando e se o evento de desenho por
// frame está disparando.
public static class ModLog
{
    private const string LogPath = "scripts\\TurboTrafficIV.log";
    private static bool _failed;

    public static void Write(string message)
    {
        if (_failed)
            return;

        try
        {
            File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }
        catch
        {
            // Nunca deixa o log derrubar o mod.
            _failed = true;
        }
    }

    public static void WriteException(string context, Exception ex)
    {
        Write("ERRO em " + context + ": " + ex);
    }
}
