using GTA;
using GTA.Native;
using System;
using System.Windows.Forms;

public class NPCTurboBoost : Script
{
    public NPCTurboBoost()
    {
        Tick += OnTick;
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

        float radius = 120.0f;

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

            // --------------------------------------------------------------------
            // NOVO FILTRO: só afeta veículos que realmente têm NPC dentro
            // --------------------------------------------------------------------
            Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);

            // Se NÃO tem motorista → é carro estacionado → IGNORA
            if (driver == null || !driver.Exists())
                continue;

            // Se o motorista é o player (raro mas seguro) → ignora
            if (driver == Player.Character)
                continue;

            // → Daqui pra frente: SOMENTE carros com NPC dentro
            // --------------------------------------------------------------------

            // TURBO ABSURDO quando player está a pé e no veículo
            Vector3 speed = v.Velocity;
            Vector3 forward = v.Direction;

            float turboPower = 70.0f;

            Vector3 boostedVel = speed + (forward * turboPower);

            Function.Call("SET_CAR_VELOCITY", v, boostedVel.X, boostedVel.Y, boostedVel.Z);
            Function.Call("SET_CAR_FORWARD_SPEED", v, boostedVel.Length());
            }   
        }

    private void OnTick(object sender, EventArgs e)
    {
        // ================================
        // ► ATIVAR EXPLOSÃO NO PLAYER
        // ================================

        if (Game.isKeyPressed(Keys.F5))
        {
            ExplodePlayer();
        }

        // ================================
        // ► ATIVAR Levitar dos Peds
        // ================================

        if (Game.isKeyPressed(Keys.F6))
        {
            ElevatePeds();
        }

        // ================================
        // ► Lógica dos carros turbinados
        // ================================

        ApplyTurboLogic();
    }
}
