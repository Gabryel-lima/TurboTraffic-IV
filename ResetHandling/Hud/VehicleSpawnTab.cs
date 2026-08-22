using System.Collections.Generic;
using System.Drawing;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public class VehicleSpawnTab : IHudTab
{
    private const float SpawnDistance = 6.0f;

    // Veículo spawnado que fica parado e sem o player dentro por esse tempo
    // some sozinho — evita acumular sucata pelo mapa em sessões longas.
    private const int InactivityTimeoutMs = 45000;
    private const float MovementThreshold = 3.0f;

    private class SpawnedVehicleInfo
    {
        public Vehicle Vehicle;
        public Vector3 LastActivePosition;
        public int LastActiveTime;
    }

    private readonly List<SpawnedVehicleInfo> _spawnedVehicles = new List<SpawnedVehicleInfo>();

    // Uma linha da lista: ou um cabeçalho de categoria, ou um veículo.
    private struct DisplayLine
    {
        public readonly bool IsHeader;
        public readonly string Text;
        public readonly int VehicleIndex;

        public DisplayLine(bool isHeader, string text, int vehicleIndex)
        {
            IsHeader = isHeader;
            Text = text;
            VehicleIndex = vehicleIndex;
        }
    }

    private static readonly DisplayLine[] Lines;
    private static readonly int[] LineOfVehicle;

    static VehicleSpawnTab()
    {
        List<DisplayLine> lines = new List<DisplayLine>();
        LineOfVehicle = new int[VehicleCatalog.Vehicles.Length];

        string currentCategory = null;
        for (int i = 0; i < VehicleCatalog.Vehicles.Length; i++)
        {
            VehicleEntry entry = VehicleCatalog.Vehicles[i];
            if (entry.Category != currentCategory)
            {
                currentCategory = entry.Category;
                lines.Add(new DisplayLine(true, currentCategory, -1));
            }

            LineOfVehicle[i] = lines.Count;
            lines.Add(new DisplayLine(false, entry.DisplayName, i));
        }

        Lines = lines.ToArray();
    }

    private int _selectedIndex;
    private int _scrollOffset;
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
        try
        {
            Ped player = Game.LocalPlayer.Character;
            if (player == null || !player.Exists())
                return;

            VehicleEntry entry = VehicleCatalog.Vehicles[_selectedIndex];
            Model model = new Model(entry.ModelName);

            Vector3 spawnPos = World.GetGroundPosition(player.Position + (player.Direction * SpawnDistance));

            Vehicle vehicle = World.CreateVehicle(model, spawnPos);
            if (vehicle == null || !vehicle.Exists())
            {
                ModLog.Write("Falha ao spawnar veiculo (modelo invalido?): " + entry.ModelName);
                return;
            }

            vehicle.Heading = player.Heading;

            if (_spawnInside)
                player.WarpIntoVehicle(vehicle, VehicleSeat.Driver);

            _spawnedVehicles.Add(new SpawnedVehicleInfo
            {
                Vehicle = vehicle,
                LastActivePosition = vehicle.Position,
                LastActiveTime = Game.GameTime
            });

            ModLog.Write("Veiculo spawnado: " + entry.ModelName + (_spawnInside ? " (dentro)" : " (fora)"));
        }
        catch (System.Exception ex)
        {
            ModLog.WriteException("VehicleSpawnTab.Confirm", ex);
        }
    }

    // Roda todo tick. Um veículo spawnado é considerado ativo enquanto o
    // player está dentro dele OU ele se move além do limiar — caso contrário,
    // depois de InactivityTimeoutMs parado e vazio, é removido do mundo.
    public void Update()
    {
        if (_spawnedVehicles.Count == 0)
            return;

        int now = Game.GameTime;
        var stale = new List<SpawnedVehicleInfo>();

        foreach (SpawnedVehicleInfo info in _spawnedVehicles)
        {
            Vehicle v = info.Vehicle;

            if (v == null || !v.Exists())
            {
                stale.Add(info);
                continue;
            }

            Ped player = Game.LocalPlayer.Character;
            bool playerInside = player != null && player.Exists()
                && player.isInVehicle() && player.CurrentVehicle == v;

            if (playerInside)
            {
                info.LastActivePosition = v.Position;
                info.LastActiveTime = now;
                continue;
            }

            if (v.Position.DistanceTo(info.LastActivePosition) > MovementThreshold)
            {
                info.LastActivePosition = v.Position;
                info.LastActiveTime = now;
                continue;
            }

            if (now - info.LastActiveTime >= InactivityTimeoutMs)
            {
                ModLog.Write("Veiculo spawnado removido por inatividade.");
                v.Delete();
                stale.Add(info);
            }
        }

        foreach (SpawnedVehicleInfo info in stale)
            _spawnedVehicles.Remove(info);
    }

    public void Draw(Graphics g, Font font, float x, float y, float width, float height)
    {
        int maxRows = HudTheme.MaxRows(height);

        // Mantém o item selecionado sempre dentro da janela visível, sem nunca
        // desenhar mais linhas do que cabem na área de conteúdo.
        int selectedLine = LineOfVehicle[_selectedIndex];
        if (selectedLine < _scrollOffset)
            _scrollOffset = selectedLine;
        if (selectedLine >= _scrollOffset + maxRows)
            _scrollOffset = selectedLine - maxRows + 1;

        int maxOffset = Lines.Length - maxRows;
        if (maxOffset < 0) maxOffset = 0;
        if (_scrollOffset > maxOffset) _scrollOffset = maxOffset;
        if (_scrollOffset < 0) _scrollOffset = 0;

        float lineY = y;
        int end = _scrollOffset + maxRows;
        if (end > Lines.Length) end = Lines.Length;

        for (int i = _scrollOffset; i < end; i++)
        {
            DisplayLine line = Lines[i];

            if (line.IsHeader)
            {
                HudTheme.DrawCell(g, font, x, lineY, line.Text, HudTheme.SectionText);
            }
            else
            {
                bool selected = line.VehicleIndex == _selectedIndex;
                HudTheme.DrawRow(g, font, x + 14f, lineY, width - 14f, line.Text, selected);
            }

            lineY += HudTheme.RowHeight;
        }

        string modeLabel = _spawnInside ? "Dentro" : "Fora";
        HudTheme.DrawTabHint(g, font, x, y, height,
            "Enter spawnar     Espaço: entrar [" + modeLabel + "]     "
            + (_selectedIndex + 1) + "/" + VehicleCatalog.Vehicles.Length);
    }

    // Expostos para a verificação de layout.
    public static int TotalLines { get { return Lines.Length; } }
}
