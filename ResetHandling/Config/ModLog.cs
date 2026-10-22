using System;
using System.IO;

// Log de diagnóstico gravado em Documentos\TurboTraffic-IV\TurboTrafficIV.log.
// Serve para descobrir, sem depender de nada desenhado na tela, se o script
// carregou, se as teclas estão chegando e se o evento de desenho por frame
// está disparando.
//
// Cada sessão do jogo começa um log novo; o da sessão anterior vira
// TurboTrafficIV.anterior.log. Antes o arquivo só crescia: o limite de linhas
// valia por sessão, e depois de algumas semanas o log tinha dezenas de
// milhares de linhas de sessões misturadas, sem data para separá-las.
public static class ModLog
{
    // O script roda a cada frame: sem estes limites, um erro repetido gravaria
    // milhares de linhas por segundo e travaria o jogo no disco.
    private const int MaxLines = 2000;
    private const int RepeatSuppressionSeconds = 5;

    private static bool _sessionStarted;
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
            if (!_sessionStarted)
                StartSession();

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

    // Roda uma vez por sessão, antes da primeira linha: o log atual vira o
    // "anterior" (substituindo o que houver) e um arquivo novo começa com a
    // data, que as linhas não carregam — elas só têm a hora.
    private static void StartSession()
    {
        _sessionStarted = true;

        if (File.Exists(ModPaths.LogFile))
        {
            if (File.Exists(ModPaths.PreviousLogFile))
                File.Delete(ModPaths.PreviousLogFile);

            File.Move(ModPaths.LogFile, ModPaths.PreviousLogFile);
        }

        File.WriteAllText(ModPaths.LogFile, "Sessão de " + DateTime.Now.ToString("yyyy-MM-dd") + Environment.NewLine);
    }

    public static void WriteException(string context, Exception ex)
    {
        Write("ERRO em " + context + ": " + ex.Message);
    }
}
