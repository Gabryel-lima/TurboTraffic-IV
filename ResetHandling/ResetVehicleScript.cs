using GTA;
using GTA.Native;
using System;
using System.Windows.Forms;

public class NPCTurboBoost : Script
{
    private readonly HudManager _hud = new HudManager();
    private bool _tutorialShown;

    public NPCTurboBoost()
    {
        ModSettings.Load();

        Tick += OnTick;
        KeyDown += OnKeyDown;
        PerFrameDrawing += OnPerFrameDrawing;
        Interval = 0; //ms
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

            Function.Call("SET_CAR_VELOCITY", v, boostedVel.X, boostedVel.Y, boostedVel.Z);
            Function.Call("SET_CAR_FORWARD_SPEED", v, boostedVel.Length());
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
    }

    private void OnKeyDown(object sender, GTA.KeyEventArgs e)
    {
        if (_hud.IsOpen)
        {
            _hud.HandleKey(e.Key, e.Shift);
            return;
        }

        if (e.Key == Keys.F1)
        {
            _hud.Toggle();
            return;
        }

        if (e.Key == Keys.F5)
        {
            ExplodePlayer();
            return;
        }

        if (e.Key == Keys.F6)
        {
            ElevatePeds();
        }
    }

    private void OnPerFrameDrawing(object sender, GraphicsEventArgs e)
    {
        _hud.Draw(e.Graphics);
    }

    private void OnTick(object sender, EventArgs e)
    {
        ShowTutorialIfNeeded();

        if (ModSettings.ChaosEnabled)
        {
            ApplyTurboLogic();
        }
    }
}
