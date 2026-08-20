using System;
using System.IO;

// Resolve uma pasta de dados sempre gravável para o log e as configurações.
//
// A pasta do jogo NÃO serve: instalações em "C:\Program Files (x86)\..." só
// aceitam escrita com elevação, então gravar o .ini/.log lá falha em silêncio
// (ou é redirecionado para o VirtualStore, onde ninguém acha).
public static class ModPaths
{
    private const string FolderName = "TurboTraffic-IV";

    private static string _dataFolder;

    public static string DataFolder
    {
        get
        {
            if (_dataFolder != null)
                return _dataFolder;

            _dataFolder = ResolveDataFolder();
            return _dataFolder;
        }
    }

    public static string LogFile
    {
        get { return Path.Combine(DataFolder, "TurboTrafficIV.log"); }
    }

    public static string SettingsFilePath
    {
        get { return Path.Combine(DataFolder, "ResetHandling.ini"); }
    }

    private static string ResolveDataFolder()
    {
        foreach (Environment.SpecialFolder root in new[]
        {
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.LocalApplicationData
        })
        {
            try
            {
                string baseFolder = Environment.GetFolderPath(root);
                if (string.IsNullOrEmpty(baseFolder))
                    continue;

                string folder = Path.Combine(baseFolder, FolderName);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                return folder;
            }
            catch
            {
                // Tenta a próxima raiz.
            }
        }

        // Último recurso: pasta atual do processo (normalmente a do jogo).
        return string.Empty;
    }
}
