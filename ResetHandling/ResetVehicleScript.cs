using GTA;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

public class NPCTurboBoost : Script
{
    // Teclas observadas por frame. O input é feito por polling com detecção de
    // borda (dispara uma vez por pressionada) em vez do evento KeyDown, porque
    // o polling é o mecanismo comprovadamente entregue neste ambiente.
    private static readonly Keys[] WatchedKeys =
    {
        Keys.F1, Keys.F5, Keys.F6,
        Keys.Up, Keys.Down, Keys.Left, Keys.Right,
        Keys.Enter, Keys.Escape, Keys.Space, Keys.Tab
    };

    private readonly HudManager _hud = new HudManager();
    private readonly HashSet<Keys> _keysDownLastFrame = new HashSet<Keys>();

    private bool _tutorialShown;
    private bool _drawingWarningShown;
    private bool _perFrameDrawingSeen;
    private int _ticksWithHudOpen;
    private int _lastHeartbeatTime;

    public NPCTurboBoost()
    {
        ModLog.Write("---- TurboTraffic-IV iniciando ----");
        ModLog.Write("Pasta de dados: " + ModPaths.DataFolder);
        ModSettings.Load();

        Tick += OnTick;
        PerFrameDrawing += OnPerFrameDrawing;
        Interval = 0; //ms

        ModLog.Write("Script carregado. Pressione F1 para abrir o HUD.");
    }

    private void ElevatePeds()
    {
        float radius = 120.0f;
        Vector3 playerPos = Player.Character.Position;

        // --- 1) Elevar o próprio player ---
        Player.Character.Position = new Vector3(
            playerPos.X,
            playerPos.Y,
            playerPos.Z + 1.0f
        );

        // --- 2) Elevar peds no raio ---
        foreach (Ped p in World.GetAllPeds())
        {
            if (p == null || !p.Exists())
                continue;

            // Ignorar player
            if (p == Player.Character)
                continue;

            // Ignorar NPC morto
            if (p.isDead)
                continue;

            // Ignorar peds muito longe
            if (playerPos.DistanceTo(p.Position) > radius)
                continue;

            Vector3 pos = p.Position;

            p.Position = new Vector3(
                pos.X,
                pos.Y,
                pos.Z + 1.0f   // <-- GANHO DE ALTURA
            );
            p.Velocity = Vector3.Zero; // evita queda violenta
        }
    }

    private void ExplodePlayer()
    {
        // Posição atual do jogador
        Vector3 pos = Player.Character.Position;

        // Explosão grande
        World.AddExplosion(pos, ExplosionType.Default, 10.0f);

        // Mata o player para garantir consistência com a explosão
        Player.Character.Die();
    }

    private void ApplyTurboLogic()
    {
        Vehicle playerVeh = null;
        bool playerInVehicle = Player.Character.isInVehicle();

        if (playerInVehicle)
            playerVeh = Player.Character.CurrentVehicle;

        float radius = ModSettings.ChaosRadius;

        foreach (Vehicle v in World.GetAllVehicles())
        {
            if (v == null || !v.Exists())
                continue;

            // Nunca alterar carro do player
            if (playerInVehicle && v == playerVeh)
                continue;

            // Limite de distância
            if (Player.Character.Position.DistanceTo(v.Position) > radius)
                continue;

            // Só afeta veículos que realmente têm NPC dentro (ignora carros estacionados)
            Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);

            if (driver == null || !driver.Exists())
                continue;

            if (driver == Player.Character)
                continue;

            // TURBO ABSURDO quando player está a pé e no veículo
            Vector3 speed = v.Velocity;
            Vector3 forward = v.Direction;

            float turboPower = ModSettings.ChaosTurboPower;

            Vector3 boostedVel = speed + (forward * turboPower);

            // Usa as propriedades tipadas do wrapper em vez de Function.Call:
            // o native "SET_CAR_VELOCITY" NÃO existe neste ScriptHook (GTA IV
            // 1.2.0.59) e falhava a cada frame, enchendo o log de erros.
            v.Velocity = boostedVel;
            v.Speed = boostedVel.Length();
        }
    }

    private void ShowTutorialIfNeeded()
    {
        if (_tutorialShown)
            return;

        if (Player.Character == null || !Player.Character.Exists() || Player.Character.isDead)
            return;

        Game.DisplayText("Pressione F1 para abrir o HUD do TurboTraffic-IV (Comandos, Spawn de Veiculos e Configuracoes).", 8000);
        _tutorialShown = true;
        ModLog.Write("Popup de tutorial exibido.");
    }

    private void ProcessInput()
    {
        bool shift = Game.isKeyPressed(Keys.ShiftKey);

        foreach (Keys key in WatchedKeys)
        {
            bool isDown = Game.isKeyPressed(key);
            bool wasDown = _keysDownLastFrame.Contains(key);

            if (isDown && !wasDown)
            {
                ModLog.Write("Tecla detectada: " + key);
                OnKeyPressed(key, shift);
            }

            if (isDown)
                _keysDownLastFrame.Add(key);
            else
                _keysDownLastFrame.Remove(key);
        }
    }

    private void OnKeyPressed(Keys key, bool shift)
    {
        if (key == Keys.F1)
        {
            _hud.Toggle();

            // Confirmação pelo texto nativo do jogo: não depende do hook de
            // desenho, então distingue "a tecla não chegou" de "chegou mas o
            // painel não foi desenhado".
            Game.DisplayText(_hud.IsOpen ? "TurboTraffic-IV: HUD aberto" : "TurboTraffic-IV: HUD fechado", 1500);

            if (!_hud.IsOpen)
                RestorePlayerControl();
            return;
        }

        if (_hud.IsOpen)
        {
            _hud.HandleKey(key, shift);
            if (!_hud.IsOpen)
                RestorePlayerControl();
            return;
        }

        if (key == Keys.F5)
        {
            ExplodePlayer();
            return;
        }

        if (key == Keys.F6)
        {
            ElevatePeds();
        }
    }

    private void RestorePlayerControl()
    {
        try
        {
            Player.CanControlCharacter = true;
        }
        catch (Exception ex)
        {
            ModLog.WriteException("RestorePlayerControl", ex);
        }
    }

    private void WarnIfDrawingUnavailable()
    {
        if (!_hud.IsOpen)
        {
            _ticksWithHudOpen = 0;
            return;
        }

        if (_perFrameDrawingSeen || _drawingWarningShown)
            return;

        // Se o HUD ficou aberto por vários frames e o evento de desenho nunca
        // disparou, o hook Direct3D não está ativo — avisa pelo texto nativo do
        // jogo, que não depende desse hook.
        _ticksWithHudOpen++;
        if (_ticksWithHudOpen < 120)
            return;

        Game.DisplayText("TurboTraffic-IV: o HUD grafico nao pode ser desenhado (evento de desenho nao disparou). Veja Documentos\\TurboTraffic-IV\\TurboTrafficIV.log", 8000);
        _drawingWarningShown = true;
        ModLog.Write("AVISO: PerFrameDrawing nunca disparou com o HUD aberto — hook Direct3D provavelmente indisponivel.");
    }

    private void OnPerFrameDrawing(object sender, GraphicsEventArgs e)
    {
        if (!_perFrameDrawingSeen)
        {
            _perFrameDrawingSeen = true;
            ModLog.Write("PerFrameDrawing disparou pela primeira vez.");
        }

        _hud.Draw(e.Graphics);
    }

    // Registra periodicamente o estado real do script: responde, sem depender
    // de nada em tela, se o tick roda, se a tecla F1 é enxergada pelo polling e
    // se o evento de desenho por frame já disparou alguma vez.
    private void WriteHeartbeat()
    {
        int now = Game.GameTime;
        if (now - _lastHeartbeatTime < 5000)
            return;

        _lastHeartbeatTime = now;
        ModLog.Write("heartbeat: tick=ok"
            + " F1pressionado=" + Game.isKeyPressed(Keys.F1)
            + " hudAberto=" + _hud.IsOpen
            + " desenhoDisparou=" + _perFrameDrawingSeen
            + " caos=" + ModSettings.ChaosEnabled);
    }

    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            WriteHeartbeat();
            ShowTutorialIfNeeded();
            ProcessInput();
            WarnIfDrawingUnavailable();

            // Enquanto o HUD está aberto, o personagem fica travado para que as
            // setas naveguem o menu sem também mover o jogador.
            if (_hud.IsOpen)
                Player.CanControlCharacter = false;

            if (ModSettings.ChaosEnabled)
                ApplyTurboLogic();
        }
        catch (Exception ex)
        {
            ModLog.WriteException("OnTick", ex);
        }
    }
}
