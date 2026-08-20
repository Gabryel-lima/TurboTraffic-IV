using System.Windows.Forms;

public struct KeybindEntry
{
    public readonly Keys Key;
    public readonly string Label;
    public readonly string Description;

    public KeybindEntry(Keys key, string label, string description)
    {
        Key = key;
        Label = label;
        Description = description;
    }
}

public static class KeybindRegistry
{
    public static readonly KeybindEntry[] Entries =
    {
        new KeybindEntry(Keys.F1, "F1", "Abrir/fechar o HUD do TurboTraffic-IV"),
        new KeybindEntry(Keys.F5, "F5", "Explodir o player"),
        new KeybindEntry(Keys.F6, "F6", "Levitar peds ao redor do player"),
    };
}
