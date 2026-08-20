using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public interface IHudTab
{
    void MoveSelection(int direction);
    void AdjustValue(int direction);
    void Confirm();
    void Secondary();
    void Draw(Graphics g, Font font, float x, float y, float width, float height);
}
