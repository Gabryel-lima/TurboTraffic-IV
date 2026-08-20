using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public class VehicleSpawnTab : IHudTab
{
    private const float SpawnDistance = 6.0f;
    private const int VisibleRows = 8;

    private int _selectedIndex;
    private bool _spawnInside = true;

    public void MoveSelection(int direction)
    {
        int count = VehicleCatalog.Vehicles.Length;
        _selectedIndex = ((_selectedIndex + direction) % count + count) % count;
    }

    public void AdjustValue(int direction) { }

    public void Secondary()
    {
        _spawnInside = !_spawnInside;
    }

    public void Confirm()
    {
        Ped player = Game.LocalPlayer.Character;
        if (player == null || !player.Exists())
            return;

        VehicleEntry entry = VehicleCatalog.Vehicles[_selectedIndex];
        Model model = new Model(entry.ModelName);

        Vector3 spawnPos = World.GetGroundPosition(player.Position + (player.Direction * SpawnDistance));

        Vehicle vehicle = World.CreateVehicle(model, spawnPos);
        if (vehicle == null || !vehicle.Exists())
            return;

        vehicle.Heading = player.Heading;

        if (_spawnInside)
        {
            player.WarpIntoVehicle(vehicle, VehicleSeat.Driver);
        }
    }

    public void Draw(Graphics g, Font font, float x, float y, float width, float height)
    {
        int count = VehicleCatalog.Vehicles.Length;
        int start = _selectedIndex - (VisibleRows / 2);
        if (start < 0) start = 0;
        if (start + VisibleRows > count) start = System.Math.Max(0, count - VisibleRows);

        string currentCategory = null;
        float lineY = y;

        for (int i = start; i < count && i < start + VisibleRows; i++)
        {
            VehicleEntry entry = VehicleCatalog.Vehicles[i];
            if (entry.Category != currentCategory)
            {
                currentCategory = entry.Category;
                g.DrawText(currentCategory, x, lineY, Color.Orange, font);
                lineY += 18f;
            }

            bool selected = i == _selectedIndex;
            string prefix = selected ? "> " : "   ";
            Color color = selected ? Color.Yellow : Color.White;
            g.DrawText(prefix + entry.DisplayName, x, lineY, color, font);
            lineY += 18f;
        }

        string modeLabel = _spawnInside ? "Dentro" : "Fora";
        g.DrawText("Espaço: spawnar [" + modeLabel + "]   Enter: confirmar", x, y + height - 16f, Color.Gainsboro, font);
    }
}
