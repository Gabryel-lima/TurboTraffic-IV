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
    // ---- Métrica do painel -------------------------------------------------
    // Todas as coordenadas aqui são canto superior esquerdo, em pixels.
    //
    // ATENÇÃO: os retângulos de fundo são desenhados por HudTheme.FillRect, e
    // não por g.DrawRectangle direto — a API do jogo recebe o CENTRO do
    // retângulo, não o canto. Era essa a origem do painel/abas desalinhados em
    // relação ao texto: o texto (DrawText) usa o canto, os retângulos usavam o
    // centro, e os dois nunca se encontravam. Ver HudTheme.FillRect.
    private const float PanelX = 24f;
    private const float PanelY = 24f;
    private const float PanelWidth = 720f;
    private const float PanelHeight = 600f;

    private const float TitleBarHeight = 56f;
    private const float TabsGap = 12f;
    private const float TabHeight = 42f;
    private const float TabWidth = 224f;
    private const float TabInnerPadding = 8f;
    private const float SeparatorGap = 10f;
    private const float ContentTopGap = 14f;
    private const float FooterHeight = 34f;
    private const float SidePadding = 22f;
    private const float TabsLeftPadding = 16f;

    // Precisa bater com o tamanho usado em EnsureFonts() — usado por
    // TextMeasure para estimar a largura real do rótulo ao centralizar.
    private const float TabFontSizePx = 20f;

    // Derivadas (mantidas como propriedades para a verificação de layout poder
    // conferir os mesmos números que o desenho usa).
    private static float TabsY { get { return PanelY + TitleBarHeight + TabsGap; } }
    private static float SeparatorY { get { return TabsY + TabHeight + SeparatorGap; } }
    private static float ContentY { get { return SeparatorY + ContentTopGap; } }
    private static float ContentBottom { get { return PanelY + PanelHeight - FooterHeight; } }
    private static float ContentHeight { get { return ContentBottom - ContentY; } }
    private static float ContentX { get { return PanelX + SidePadding; } }
    private static float ContentWidth { get { return PanelWidth - (SidePadding * 2f); } }

    private static readonly string[] TabNames = { "Comandos", "Spawn de Veículos", "Configurações" };

    // Paleta — fundos sólidos (alpha 255): um fundo translúcido deixa o
    // contraste do texto refém da cor da cena por trás (rua clara, céu, etc).
    private static readonly Color PanelBg = Color.FromArgb(255, 18, 18, 20);
    private static readonly Color TitleBg = Color.FromArgb(255, 176, 28, 28);
    private static readonly Color TabActiveBg = Color.FromArgb(255, 235, 140, 0);
    private static readonly Color TabIdleBg = Color.FromArgb(255, 54, 54, 58);
    private static readonly Color SeparatorColor = Color.FromArgb(255, 235, 140, 0);
    private static readonly Color HintColor = Color.Gainsboro;

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

    // Esquema de navegação: Tab/Shift+Tab trocam de aba, ↑/↓ navegam a lista,
    // ←/→ ajustam o valor do item selecionado, Enter confirma e Espaço é a
    // ação secundária. O rodapé e a aba Comandos anunciam o Tab explicitamente,
    // porque sem essa dica o jogador não descobre a troca de aba.
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

        _titleFont = new Font(30f, FontScaling.Pixel, true, false) { Color = Color.White, Effect = FontEffect.Edge };
        _tabFont = new Font(20f, FontScaling.Pixel, true, false) { Effect = FontEffect.Edge };
        _bodyFont = new Font(22f, FontScaling.Pixel) { Color = Color.White, Effect = FontEffect.Edge };
        _hintFont = new Font(16f, FontScaling.Pixel) { Color = HintColor, Effect = FontEffect.Edge };
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

            // Fundo + barra de título
            HudTheme.FillRect(g, PanelX, PanelY, PanelWidth, PanelHeight, PanelBg);
            HudTheme.FillRect(g, PanelX, PanelY, PanelWidth, TitleBarHeight, TitleBg);
            HudTheme.DrawCell(g, _titleFont, PanelX + SidePadding, PanelY, TitleBarHeight, "TurboTraffic-IV", Color.White);

            // Abas — texto centralizado por cálculo manual (largura medida via
            // TextMeasure, altura via Font.GetLineHeight), não via
            // RectangleF/TextAlignment.
            for (int i = 0; i < TabNames.Length; i++)
            {
                float tabX = PanelX + TabsLeftPadding + (i * TabWidth);
                float tabW = TabWidth - TabInnerPadding;
                bool active = (int)_activeTab == i;
                HudTheme.FillRect(g, tabX, TabsY, tabW, TabHeight, active ? TabActiveBg : TabIdleBg);
                HudTheme.DrawCentered(g, _tabFont, tabX, TabsY, tabW, TabHeight, TabFontSizePx, true,
                    TabNames[i], active ? Color.Black : Color.White);
            }

            // Linha de acento separando abas do conteúdo
            HudTheme.FillRect(g, PanelX + TabsLeftPadding, SeparatorY, PanelWidth - (TabsLeftPadding * 2f), 2f, SeparatorColor);

            CurrentTab.Draw(g, _bodyFont, ContentX, ContentY, ContentWidth, ContentHeight);

            HudTheme.DrawCell(g, _hintFont, PanelX + SidePadding, ContentBottom, FooterHeight,
                "Tab trocar aba     ↑↓ navegar     ←→ ajustar     Enter confirmar     Esc fechar", HintColor);
        }
        catch (System.Exception ex)
        {
            ModLog.WriteException("HudManager.Draw", ex);
        }
    }
}
