using System;
using GTA;

// Ações imediatas sobre o jogador e o veículo dele.
//
// Ficam aqui, e não dentro do script principal ou de uma aba do HUD, porque
// têm DOIS pontos de entrada — a tecla de atalho e a linha da aba
// Configurações. Duplicar a lógica nos dois faria as duas versões divergirem
// no primeiro ajuste.
public static class PlayerActions
{
    // Saúde cheia em GTA IV. Repair() já devolve o veículo ao estado íntegro,
    // mas não garante os medidores no topo em todos os casos (carro que ficou
    // com o motor fumaçando continua com EngineHealth baixo).
    private const int FullVehicleHealth = 1000;
    private const float FullEngineHealth = 1000.0f;
    private const float FullPetrolTankHealth = 1000.0f;

    private static readonly VehicleWheel[] AllWheels =
    {
        VehicleWheel.FrontLeft, VehicleWheel.FrontRight,
        VehicleWheel.CenterLeft, VehicleWheel.CenterRight,
        VehicleWheel.RearLeft, VehicleWheel.RearRight
    };

    // Conserta o veículo em que o jogador está. Devolve a mensagem já pronta
    // para o texto nativo do jogo — quem chama só exibe, sem decidir nada.
    public static string RepairCurrentVehicle()
    {
        try
        {
            Ped player = Game.LocalPlayer.Character;

            if (player == null || !player.Exists())
                return "TurboTraffic-IV: jogador indisponivel.";

            if (!player.isInVehicle())
                return "TurboTraffic-IV: entre em um veiculo para consertar.";

            Vehicle v = player.CurrentVehicle;

            if (v == null || !v.Exists())
                return "TurboTraffic-IV: veiculo indisponivel.";

            // Vale também para o passageiro: quem está no banco do carona
            // igualmente quer o carro inteiro de volta.
            v.Repair();
            v.Health = FullVehicleHealth;
            v.EngineHealth = FullEngineHealth;
            v.PetrolTankHealth = FullPetrolTankHealth;
            v.CloseAllDoors();
            v.Wash();

            FixBurstTires(v);

            ModLog.Write("Veiculo do jogador consertado: " + v.Model.ToString());
            return "TurboTraffic-IV: veiculo consertado.";
        }
        catch (Exception ex)
        {
            ModLog.WriteException("PlayerActions.RepairCurrentVehicle", ex);
            return "TurboTraffic-IV: falha ao consertar o veiculo (veja o log).";
        }
    }

    // Repair() normalmente já devolve os pneus, mas não em todos os casos —
    // e o pneu furado é justamente o dano que o jogador mais percebe.
    //
    // Cada roda vai no SEU try: VehicleWheel enumera 6 rodas, e um modelo de
    // duas (moto) não tem as outras quatro. É a mesma classe de problema já
    // documentada em GetOccupants para assentos — perguntar por uma roda que o
    // modelo não tem devolve lixo e pode derrubar o jogo. Uma roda inválida
    // aqui não pode cancelar o conserto das que existem.
    private static void FixBurstTires(Vehicle v)
    {
        foreach (VehicleWheel wheel in AllWheels)
        {
            try
            {
                if (v.IsTireBurst(wheel))
                    v.FixTire(wheel);
            }
            catch (Exception ex)
            {
                ModLog.WriteException("PlayerActions.FixBurstTires", ex);
            }
        }
    }
}
