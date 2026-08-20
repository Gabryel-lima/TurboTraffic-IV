using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public class CommandsTab : IHudTab
{
    public void MoveSelection(int direction) { }
    public void AdjustValue(int direction) { }
    public void Confirm() { }
    public void Secondary() { }

    public void Draw(Graphics g, Font font, float x, float y, float width, float height)
    {
        float lineY = y;
        foreach (KeybindEntry entry in KeybindRegistry.Entries)
        {
            g.DrawText(entry.Label + "  -  " + entry.Description, x, lineY, Color.White, font);
            lineY += 20f;
        }
    }
}
