using System.Drawing;
using System.Windows.Forms;
using GTA;
using Font = GTA.Font;
using Graphics = GTA.Graphics;

public enum HudTab
{
    Commands = 0,
    VehicleSpawn = 1,
    Settings = 2
}

public class HudManager
{
    private const float PanelX = 20f;
    private const float PanelY = 20f;
    private const float PanelWidth = 360f;
    private const float PanelHeight = 300f;
    private const float TabHeight = 24f;
    private const float TabWidth = 116f;

    private static readonly string[] TabNames = { "Comandos", "Spawn de Veículos", "Configurações" };

    // Fontes são criadas na primeira vez que o HUD é desenhado, e não no
    // construtor: elas dependem do device Direct3D, que pode ainda não estar
    // pronto no momento em que o script é carregado.
    private Font _titleFont;
    private Font _tabFont;
    private Font _bodyFont;
    private Font _hintFont;
    private bool _fontsReady;
    private bool _loggedFirstDraw;

    private readonly CommandsTab _commandsTab = new CommandsTab();
    private readonly VehicleSpawnTab _vehicleSpawnTab = new VehicleSpawnTab();
    private readonly SettingsTab _settingsTab = new SettingsTab();

    private HudTab _activeTab = HudTab.Commands;

    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        ModLog.Write("HUD " + (IsOpen ? "aberto" : "fechado"));
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void HandleKey(Keys key, bool shift)
    {
        if (!IsOpen)
            return;

        switch (key)
        {
            case Keys.Escape:
                IsOpen = false;
                break;
            case Keys.Tab:
                CycleTab(shift ? -1 : 1);
                break;
            case Keys.Up:
                CurrentTab.MoveSelection(-1);
                break;
            case Keys.Down:
                CurrentTab.MoveSelection(1);
                break;
            case Keys.Left:
                CurrentTab.AdjustValue(-1);
                break;
            case Keys.Right:
                CurrentTab.AdjustValue(1);
                break;
            case Keys.Enter:
                CurrentTab.Confirm();
                break;
            case Keys.Space:
                CurrentTab.Secondary();
                break;
        }
    }

    private IHudTab CurrentTab
    {
        get
        {
            if (_activeTab == HudTab.VehicleSpawn) return _vehicleSpawnTab;
            if (_activeTab == HudTab.Settings) return _settingsTab;
            return _commandsTab;
        }
    }

    private void CycleTab(int direction)
    {
        int next = ((int)_activeTab + direction + TabNames.Length) % TabNames.Length;
        _activeTab = (HudTab)next;
    }

    private void EnsureFonts()
    {
        if (_fontsReady)
            return;

        _titleFont = new Font(16f, FontScaling.Pixel, true, false) { Color = Color.White, Effect = FontEffect.Edge };
        _tabFont = new Font(12f, FontScaling.Pixel, true, false) { Effect = FontEffect.Edge };
        _bodyFont = new Font(13f, FontScaling.Pixel) { Color = Color.White, Effect = FontEffect.Edge };
        _hintFont = new Font(11f, FontScaling.Pixel) { Color = Color.Gainsboro, Effect = FontEffect.Edge };
        _fontsReady = true;

        ModLog.Write("Fontes do HUD criadas.");
    }

    public void Draw(Graphics g)
    {
        if (!IsOpen)
            return;

        try
        {
            EnsureFonts();

            // Sem isto, as coordenadas podem ser interpretadas em unidades de
            // tela (0..1) em vez de pixels, e o painel é desenhado inteiramente
            // fora da área visível.
            g.Scaling = FontScaling.Pixel;

            if (!_loggedFirstDraw)
            {
                ModLog.Write("Primeiro desenho do HUD executado.");
                _loggedFirstDraw = true;
            }

            g.DrawRectangle(PanelX, PanelY, PanelWidth, PanelHeight, Color.FromArgb(215, 15, 15, 15));
            g.DrawRectangle(PanelX, PanelY, PanelWidth, 28f, Color.FromArgb(235, 180, 30, 30));
            g.DrawText("TurboTraffic-IV", PanelX + 10f, PanelY + 5f, Color.White, _titleFont);

            float tabY = PanelY + 34f;
            for (int i = 0; i < TabNames.Length; i++)
            {
                float tabX = PanelX + 10f + (i * TabWidth);
                bool active = (int)_activeTab == i;
                Color bg = active ? Color.FromArgb(255, 235, 140, 0) : Color.FromArgb(160, 70, 70, 70);
                g.DrawRectangle(tabX, tabY, TabWidth - 6f, TabHeight, bg);
                g.DrawText(TabNames[i], tabX + 4f, tabY + 4f, active ? Color.Black : Color.White, _tabFont);
            }

            float contentY = tabY + TabHeight + 10f;
            float contentHeight = (PanelY + PanelHeight) - contentY - 22f;
            CurrentTab.Draw(g, _bodyFont, PanelX + 12f, contentY, PanelWidth - 24f, contentHeight);

            g.DrawText("Tab: trocar aba   Setas: navegar   Esc: fechar",
                PanelX + 10f, PanelY + PanelHeight - 18f, Color.Gainsboro, _hintFont);
        }
        catch (System.Exception ex)
        {
            ModLog.WriteException("HudManager.Draw", ex);
        }
    }
}
