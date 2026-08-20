using System;
using System.IO;

// Log de diagnóstico gravado em Documentos\TurboTraffic-IV\TurboTrafficIV.log.
// Serve para descobrir, sem depender de nada desenhado na tela, se o script
// carregou, se as teclas estão chegando e se o evento de desenho por frame
// está disparando.
public static class ModLog
{
    // O script roda a cada frame: sem estes limites, um erro repetido gravaria
    // milhares de linhas por segundo e travaria o jogo no disco.
    private const int MaxLines = 2000;
    private const int RepeatSuppressionSeconds = 5;

    private static bool _failed;
    private static int _lineCount;
    private static string _lastMessage;
    private static DateTime _lastMessageTime = DateTime.MinValue;

    public static void Write(string message)
    {
        if (_failed || _lineCount >= MaxLines)
            return;

        // Suprime repetição da mesma mensagem em rajada.
        if (message == _lastMessage && (DateTime.Now - _lastMessageTime).TotalSeconds < RepeatSuppressionSeconds)
            return;

        _lastMessage = message;
        _lastMessageTime = DateTime.Now;

        try
        {
            File.AppendAllText(ModPaths.LogFile, DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
            _lineCount++;

            if (_lineCount == MaxLines)
                File.AppendAllText(ModPaths.LogFile, "(limite de linhas atingido, log interrompido)" + Environment.NewLine);
        }
        catch
        {
            // Nunca deixa o log derrubar o mod.
            _failed = true;
        }
    }

    public static void WriteException(string context, Exception ex)
    {
        Write("ERRO em " + context + ": " + ex.Message);
    }
}
