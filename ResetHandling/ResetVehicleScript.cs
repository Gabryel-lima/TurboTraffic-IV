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
        Keys.F1, Keys.F5, Keys.F6, Keys.F7,
        Keys.Up, Keys.Down, Keys.Left, Keys.Right,
        Keys.Enter, Keys.Escape, Keys.Space, Keys.Tab
    };

    // Identidade por handle nativo. Toda coleção de Vehicle/Ped precisa deste
    // comparador — o padrão do .NET compara por referência nesses tipos, e a
    // biblioteca entrega um wrapper novo a cada tick para o mesmo carro. Ver
    // HandleObjectComparer.cs.
    private static readonly HandleObjectComparer<Vehicle> VehicleByHandle = new HandleObjectComparer<Vehicle>();
    private static readonly HandleObjectComparer<Ped> PedByHandle = new HandleObjectComparer<Ped>();

    private readonly HudManager _hud = new HudManager();
    private readonly HashSet<Keys> _keysDownLastFrame = new HashSet<Keys>();
    // Veículos cujas flags de dano foram alteradas por este mod — precisam ser
    // devolvidas ao padrão quando saem do raio ou o efeito é desligado.
    private readonly HashSet<Vehicle> _tweakedVehicles = new HashSet<Vehicle>(VehicleByHandle);

    // Última combinação de dano já aplicada — usada para não reemitir os
    // mesmos natives em todo frame quando nada mudou. Começa no padrão do
    // jogo (dano normal, amassa normal).
    private bool _appliedInvincible;
    private bool _appliedCrumple = true;

    // Invencibilidade do jogador já aplicada — só serve para saber quando
    // DESFAZER. Ligada, ela é reemitida todo tick (ver ApplyPlayerInvincibility).
    private bool _appliedPlayerInvincible;

    // Motoristas com a IA de trajeto desligada pelo Modo Solto, e o instante
    // da última limpeza de tarefa de cada um — precisam ter a tarefa de
    // trânsito devolvida quando o modo acaba.
    private readonly Dictionary<Ped, int> _freedDrivers = new Dictionary<Ped, int>(PedByHandle);

    // Intervalo entre reemissões do "esqueça o trajeto". Curto o bastante para
    // a IA nunca retomar o volante, longo o bastante para o motorista chegar a
    // reagir entre uma limpeza e outra.
    private const int FreeRideRetaskIntervalMs = 400;

    // Varredura de detritos: cara demais para rodar todo frame, e não precisa —
    // sucata não aparece de um frame para o outro.
    private const int DebrisSweepIntervalMs = 750;
    private const float DebrisMinDistance = 30.0f;
    private const int DebrisMaxDeletionsPerSweep = 12;

    private int _lastDebrisSweepTime;

    // Os modos que ACUMULAM velocidade (Legado e Solto) somam o empurrão uma
    // vez por tick, e o tick roda uma vez por frame (Interval = 0). Sem
    // compensar o tempo de frame, a mesma "Força" rendia o dobro de velocidade
    // a 60fps do que a 30fps — o número do HUD não significava nada fixo.
    //
    // A escala é normalizada em 60fps de propósito: a 60fps ela vale 1.0, então
    // os valores já salvos no .ini continuam produzindo exatamente o efeito a
    // que o jogador se acostumou, e só as outras taxas de quadros passam a
    // bater com esse mesmo efeito.
    private const float BoostReferenceFps = 60.0f;

    // Teto da escala: depois de uma pausa, tela de loading ou travada, o delta
    // entre dois ticks chega a segundos inteiros. Sem limite, o primeiro tick
    // de volta aplicaria esse acúmulo todo de uma vez e mandaria o trânsito
    // para a estratosfera.
    private const float BoostMaxFrameScale = 4.0f;

    private int _lastTurboTime;
    private float _turboFrameScale = 1.0f;

    // Resumo da varredura de impulsos no log (ver LogTurboSweep).
    private const int TurboSweepLogIntervalMs = 5000;
    private int _lastTurboLogTime;
    private string _lastTurboSummary;

    // Tempo que o Modo Velocidade Máxima leva para levar um carro parado até o
    // alvo. Proporcional ao alvo, então a subida demora o mesmo tanto seja o
    // alvo 50 ou 400 — o que muda é a aceleração, não a espera.
    private const float MaxSpeedRampSeconds = 1.5f;

    // Fração do empurrão entregue como força física (ver ApplyPhysicalPush).
    // Alta o bastante para o carro reagir com o corpo, baixa o bastante para
    // não dobrar o efeito que a escrita de velocidade já garante.
    private const float PhysicalForceShare = 0.35f;

    // Quanto da direção o Modo Ímã corrige por frame de referência. 0.15 leva
    // cerca de meio segundo para o carro apontar ao jogador — rápido o
    // suficiente para perseguir, lento o suficiente para ser uma curva.
    private const float MagnetTurnRatePerFrame = 0.15f;


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

        // Com a invencibilidade do jogador ligada, o Die() explícito passaria
        // por cima dela e mataria assim mesmo — a opção diz "não morrer", e ela
        // tem que valer também contra os comandos do próprio mod. A explosão
        // continua acontecendo; só o jogador é que sai dela de pé.
        if (ModSettings.PlayerInvincible)
        {
            Game.DisplayText("TurboTraffic-IV: explosao criada (player invencivel, nao morreu).", 2500);
            return;
        }

        // Mata o player para garantir consistência com a explosão
        Player.Character.Die();
    }

    // Modo Legado: empurrão ADITIVO e cumulativo, sem teto — o formato
    // original do mod. Não sabe nada sobre o Modo Velocidade Máxima.
    private void ApplyLegacyBoost(Vehicle v)
    {
        Vector3 forward = v.Direction;
        float turboPower = ModSettings.ChaosTurboPower * _turboFrameScale;

        // Velocidade ATUAL projetada no eixo do nariz (produto escalar), e não
        // o comprimento do vetor.
        //
        // Com Length(), tudo o que o carro tivesse de movimento virava
        // combustível para o empurrão frontal: a derrapagem lateral entrava na
        // conta, a queda de um viaduto entrava na conta, e um carro andando de
        // ré era cuspido para a FRENTE, porque comprimento não tem sinal. Era
        // esse o motivo de o mesmo valor de Força render empurrões tão
        // diferentes de um carro para o outro. Com Dot, o que se soma é a
        // velocidade que o carro de fato tem PARA ONDE ELE APONTA — o resto do
        // movimento fica onde estava.
        float forwardSpeed = Vector3.Dot(v.Velocity, forward);

        // Usa as propriedades tipadas do wrapper em vez de Function.Call:
        // o native "SET_CAR_VELOCITY" NÃO existe neste ScriptHook (GTA IV
        // 1.2.0.59) e falhava a cada frame, enchendo o log de erros.
        v.Speed = forwardSpeed + turboPower;

        ApplyPhysicalPush(v, forward, turboPower);
    }

    // Modo Velocidade Máxima: mecânica DELIBERADAMENTE diferente do Legado —
    // fixa a velocidade direto no alvo (sem somar ao valor atual), como um
    // cruzeiro constante. Não sabe nada sobre o Modo Legado. Quando os dois
    // modos estão ligados, este roda depois do Legado em ApplyTurboLogic, e
    // como aqui a velocidade é sobrescrita (não somada), o alvo prevalece
    // para aquele tick — essa ordem fixa é a composição entre os modos.
    private void ApplyMaxSpeedMode(Vehicle v)
    {
        Vector3 forward = v.Direction;
        float targetSpeed = ModSettings.ChaosMaxSpeed;
        float currentSpeed = Vector3.Dot(v.Velocity, forward);

        // ACELERA até o alvo, em vez de grudar nele.
        //
        // Antes a velocidade era escrita direto no alvo a cada frame. Isso
        // apagava a física inteira do carro: ele saía de 0 a 500 km/h no
        // primeiro frame, atravessava batidas sem perder um quilômetro por
        // hora (o frame seguinte reimpunha o alvo) e não tinha peso nenhum.
        // Com a rampa, o carro ganha velocidade ao longo de MaxSpeedRampSeconds
        // — ele acelera, a colisão realmente o segura, e ele volta a acelerar
        // depois. O alvo continua sendo alvo; só deixou de ser teletransporte.
        //
        // A rampa é proporcional ao alvo, então chegar lá leva sempre o mesmo
        // tempo, seja o alvo 50 ou 400.
        float step = (targetSpeed / MaxSpeedRampSeconds) * FrameSeconds();
        float delta = targetSpeed - currentSpeed;

        if (delta > step)
            delta = step;
        else if (delta < -step)
            delta = -step;

        v.Speed = currentSpeed + delta;

        // Só empurra fisicamente quando está ACELERANDO: aplicar força para a
        // frente enquanto o carro desacelera brigaria com a própria rampa.
        if (delta > 0.0f)
            ApplyPhysicalPush(v, forward, delta);
    }

    // Modo Ímã: o trânsito converge no jogador. Mecânica deliberadamente
    // ortogonal às outras duas — este modo NÃO decide velocidade, só reescreve
    // a DIREÇÃO. Por isso roda por último em ApplyTurboLogic: Legado e
    // Velocidade Máxima definem o "quão rápido", o Ímã define o "para onde",
    // e os três compõem sem que nenhum precise conhecer o outro.
    private void ApplyMagnetMode(Vehicle v)
    {
        Vector3 toPlayer = Player.Character.Position - v.Position;

        // Zera o componente vertical: perseguir a altura do jogador faz o
        // carro arar o asfalto ou mergulhar de viadutos em vez de dirigir.
        toPlayer.Z = 0.0f;

        // Praticamente em cima do jogador — normalizar aqui daria uma direção
        // instável, com o carro tremendo em volta do alvo.
        if (toPlayer.Length() < 1.0f)
            return;

        Vector3 direction = Vector3.Normalize(toPlayer);

        // Herda a velocidade que o veículo já tem (possivelmente vinda de outro
        // modo), com um piso para que carro parado também seja puxado.
        float speed = v.Speed;
        float pull = ModSettings.ChaosMagnetPull;

        if (speed < pull)
            speed = pull;

        Vector3 desired = direction * speed;

        // CURVA em direção ao jogador, em vez de saltar para a direção nova.
        //
        // Antes a velocidade era substituída inteira pelo vetor apontado ao
        // jogador. O carro trocava de direção instantaneamente, sem inércia:
        // andava de lado, virava de costas para onde estava indo e escorregava
        // pela rua como se estivesse no gelo. Interpolando, a velocidade antiga
        // ainda pesa — o carro descreve uma curva, contorna o quarteirão e
        // chega dirigindo, não deslizando.
        float turn = MagnetTurnRatePerFrame * _turboFrameScale;

        if (turn > 1.0f)
            turn = 1.0f;

        // Só Velocity, sem tocar em Speed: Speed empurra ao longo do eixo
        // FRONTAL do carro, o que jogaria o veículo para onde ele está virado
        // e desfaria justamente a direção que este modo acabou de calcular.
        v.Velocity = Vector3.Lerp(v.Velocity, desired, turn);
    }

    // Modo Solto: empurra o carro DEPOIS de tirar o motorista do trajeto, para
    // ver o veículo se comportar só pela física. É a mecânica mais distinta das
    // outras — os outros modos brigam com a IA a cada frame (ela corrige a
    // direção, freia nas curvas, respeita a pista); aqui a IA sai de cena e o
    // impulso é a única coisa que age sobre o carro.
    private void ApplyFreeRideMode(Vehicle v, Ped driver)
    {
        // Sem motorista não há IA a soltar — o carro já está entregue à física,
        // que é justamente o estado que este modo procura.
        if (driver != null)
            ReleaseDriverAi(driver);

        Vector3 forward = v.Direction;
        float push = ModSettings.ChaosFreeRidePush * _turboFrameScale;

        // Impulso puramente ADITIVO no vetor, sem passar por Speed. Essa é a
        // diferença que dá o comportamento "solto": Speed recoloca o carro no
        // eixo frontal todo frame, matando o componente lateral. Preservando o
        // vetor inteiro, o carro deriva, roda e escorrega de verdade.
        v.Velocity = v.Velocity + (forward * push);

        ApplyPhysicalPush(v, forward, push);
    }

    // Parcela do empurrão entregue como FORÇA de verdade, além da escrita de
    // velocidade.
    //
    // A escrita de velocidade é o que garante o efeito — ela é determinística e
    // funciona mesmo com a IA do motorista puxando o volante para o outro lado.
    // Mas ela também é o que fazia o impulso parecer um teletransporte: o carro
    // trocava de velocidade sem que nada no corpo dele reagisse. A força passa
    // pela simulação: a suspensão comprime, o nariz levanta, o peso vai para a
    // traseira, e o carro sai de fato "empurrado" em vez de "reposicionado".
    //
    // Os dois juntos de propósito, e a força como uma FRAÇÃO do empurrão: força
    // sozinha seria inconsistente demais (carro pesado mal se mexe), e força
    // cheia somada à velocidade dobraria o efeito.
    private void ApplyPhysicalPush(Vehicle v, Vector3 forward, float speedDelta)
    {
        if (speedDelta <= 0.0f)
            return;

        // Vetor em coordenadas do mundo (ApplyForce, não ApplyForceRelative):
        // v.Direction já é o nariz do carro no mundo, então não é preciso
        // adivinhar qual eixo local do modelo aponta para frente.
        v.ApplyForce(forward * (speedDelta * PhysicalForceShare));
    }

    // Desliga o "pathing" do motorista sem apagar o motorista.
    private void ReleaseDriverAi(Ped driver)
    {
        int now = Game.GameTime;
        int lastClear;
        bool known = _freedDrivers.TryGetValue(driver, out lastClear);

        if (!known)
        {
            // Isto é o que faz o motorista continuar existindo: no primeiro
            // impacto forte ele atravessava o para-brisa e ficava para trás na
            // rua, e o carro seguia voando vazio. Preso ao banco, ele vai junto.
            driver.WillFlyThroughWindscreen = false;
            driver.CanBeKnockedOffBike = false;
            driver.Task.AlwaysKeepTask = false;
        }

        if (!known || now - lastClear >= FreeRideRetaskIntervalMs)
        {
            // Limpar a tarefa periodicamente, em vez de bloquear os eventos do
            // ped de uma vez (BlockPermanentEvents), é o que o mantém vivo como
            // personagem: bloqueado ele vira estátua e não reage a nada. Assim
            // ele entra em pânico, tenta agarrar o volante e tenta pular fora
            // entre uma limpeza e outra — e a limpeza seguinte cancela a fuga
            // antes que ela termine, sem nunca deixar a IA de trajeto voltar.
            driver.Task.ClearAllImmediately();
            _freedDrivers[driver] = now;
        }
    }

    // Devolve o motorista ao trânsito normal. Sem isto, desligar o modo (ou
    // simplesmente se afastar) deixaria a cidade cheia de motoristas
    // catatônicos parados no meio da rua — mesmo tipo de vazamento de estado
    // que as flags de dano tinham.
    private void RestoreDriverAi(Ped driver)
    {
        // Isolamento por motorista: a devolução da IA passou a consultar
        // isRequiredForMission, e um handle duvidoso estoura justamente nesse
        // native (ver HasMissionOccupant). Sem este catch, um único motorista
        // problemático derrubaria o tick inteiro — junto com o turbo e o HUD
        // daquele frame.
        try
        {
            RestoreDriverAiCore(driver);
        }
        catch (Exception ex)
        {
            ModLog.WriteException("RestoreDriverAi", ex);
        }
    }

    private void RestoreDriverAiCore(Ped driver)
    {
        driver.BlockPermanentEvents = false;
        driver.WillFlyThroughWindscreen = true;
        driver.CanBeKnockedOffBike = true;
        driver.Task.ClearAllImmediately();

        // Motorista de missão: limpa a tarefa e PARA por aqui, sem impor
        // nenhuma. Dar "dirija por aí" ao alvo de uma perseguição o
        // aposentaria como alvo de vez — a tarefa do mod ficaria por cima da
        // que o script da missão emitiu, e a missão nunca mais andaria. Sem
        // tarefa imposta, o script da missão retoma o motorista sozinho.
        //
        // AlwaysKeepTask volta a true porque é assim que peds de missão são
        // configurados: é o que faz a tarefa da missão sobreviver a sustos,
        // colisões e tiros — exatamente o que o Modo Solto tinha desligado.
        if (driver.isRequiredForMission)
        {
            driver.Task.AlwaysKeepTask = true;
            return;
        }

        if (driver.isInVehicle())
            driver.Task.CruiseWithVehicle(driver.CurrentVehicle, 20.0f, true);
        else
            driver.Task.WanderAround();
    }

    private void RestoreFreedDriversNotTouched(HashSet<Ped> touchedThisTick)
    {
        if (_freedDrivers.Count == 0)
            return;

        var stale = new List<Ped>();

        foreach (Ped p in _freedDrivers.Keys)
        {
            if (touchedThisTick.Contains(p))
                continue;

            stale.Add(p);

            if (p != null && p.Exists() && !p.isDead)
                RestoreDriverAi(p);
        }

        foreach (Ped p in stale)
            _freedDrivers.Remove(p);
    }

    private void RestoreAllFreedDrivers()
    {
        if (_freedDrivers.Count == 0)
            return;

        foreach (Ped p in _freedDrivers.Keys)
        {
            if (p != null && p.Exists() && !p.isDead)
                RestoreDriverAi(p);
        }

        _freedDrivers.Clear();
    }

    // Otimização: sucata fora do campo de visão é apagada para liberar vagas de
    // tráfego. O jogo só mantém um número limitado de veículos ativos — com as
    // ruas entupidas de carcaças ele para de spawnar trânsito novo, e o fluxo
    // constante de veículos (que é o que faz graça) seca. Nada some na tela do
    // jogador: só é candidato o que ele não está olhando.
    private void CleanupOffscreenDebris()
    {
        if (!ModSettings.ChaosDebrisCleanup)
            return;

        int now = Game.GameTime;
        if (now - _lastDebrisSweepTime < DebrisSweepIntervalMs)
            return;

        _lastDebrisSweepTime = now;

        Vehicle playerVeh = null;
        bool playerInVehicle = Player.Character.isInVehicle();

        if (playerInVehicle)
            playerVeh = Player.Character.CurrentVehicle;

        Vector3 playerPos = Player.Character.Position;
        int deleted = 0;

        foreach (Vehicle v in World.GetAllVehicles())
        {
            if (deleted >= DebrisMaxDeletionsPerSweep)
                break;

            if (v == null || !v.Exists())
                continue;

            if (playerInVehicle && v == playerVeh)
                continue;

            if (v.isRequiredForMission)
                continue;

            // A regra central: nada desaparece em cena.
            if (v.isOnScreen)
                continue;

            // Margem extra além da tela: um carro logo atrás do jogador pode
            // voltar ao campo de visão num giro de câmera, e sumir bem na hora
            // em que ele se vira é pior que a carcaça parada.
            if (playerPos.DistanceTo(v.Position) < DebrisMinDistance)
                continue;

            // Isolamento por veículo: um handle problemático faz esta sucata ser
            // pulada, não o tick inteiro morrer. O catch do OnTick abortaria
            // junto o turbo e o HUD daquele frame.
            try
            {
                if (!IsDebris(v))
                    continue;

                List<Ped> occupants = GetOccupants(v);

                // Ped de missão dentro de um carro comum: não mexer no conjunto.
                if (HasMissionOccupant(occupants))
                    continue;

                DeleteVehicleWithOccupants(v, occupants);
                deleted++;
            }
            catch (Exception ex)
            {
                ModLog.WriteException("CleanupOffscreenDebris.veiculo", ex);
            }
        }

        int pedsRemoved = CleanupLooseChaosPeds(playerPos);

        if (deleted > 0 || pedsRemoved > 0)
            ModLog.Write("Limpeza fora do campo de visao: veiculos=" + deleted + " peds=" + pedsRemoved);
    }

    // ESTA era a fábrica de pedestres. DELETE_CAR remove o veículo mas NÃO
    // remove quem está dentro: o jogo simplesmente deposita os ocupantes no
    // chão onde o carro estava. Como IsDebris aceita qualquer carcaça — e uma
    // carcaça costuma ter o motorista VIVO ainda sentado —, cada sucata
    // apagada fora da tela virava um pedestre novo na rua. Marcar o ped como
    // NoLongerNeeded, que era o que estava aqui, não apaga ninguém: só diz ao
    // jogo que o script não precisa mais dele.
    private void DeleteVehicleWithOccupants(Vehicle v, List<Ped> occupants)
    {
        foreach (Ped p in occupants)
        {
            if (p == Player.Character)
                continue;

            _freedDrivers.Remove(p);

            try
            {
                p.Delete();
            }
            catch (Exception ex)
            {
                ModLog.WriteException("DeleteVehicleWithOccupants", ex);
            }
        }

        _tweakedVehicles.Remove(v);
        v.Delete();
    }

    // Enumera SÓ os assentos que o veículo realmente tem.
    //
    // GetPedOnSeat não valida o assento contra o modelo: pedir o banco traseiro
    // de uma moto ou de um carro de dois lugares devolve um handle lixo, que
    // passa por Exists() e derruba o jogo no primeiro native de verdade
    // chamado sobre ele — foi assim que isRequiredForMission estourou um
    // AccessViolationException dentro do tick. PassengerSeats dá a lotação real
    // do modelo, e isSeatFree evita perguntar por um ocupante que não existe.
    private List<Ped> GetOccupants(Vehicle v)
    {
        var occupants = new List<Ped>();

        try
        {
            int passengerSeats = v.PassengerSeats;

            // i == -1 é o banco do motorista; 0..n-1 mapeiam para RightFront,
            // LeftRear e RightRear, na ordem do enum VehicleSeat.
            for (int i = -1; i < passengerSeats; i++)
            {
                VehicleSeat seat = i < 0 ? VehicleSeat.Driver : (VehicleSeat)i;

                if (v.isSeatFree(seat))
                    continue;

                Ped p = v.GetPedOnSeat(seat);

                if (p == null || !p.Exists())
                    continue;

                occupants.Add(p);
            }
        }
        catch (Exception ex)
        {
            // Um assento problemático não pode derrubar o tick inteiro: a lista
            // sai incompleta e a varredura segue.
            ModLog.WriteException("GetOccupants", ex);
        }

        return occupants;
    }

    private bool HasMissionOccupant(List<Ped> occupants)
    {
        foreach (Ped p in occupants)
        {
            try
            {
                if (p.isRequiredForMission)
                    return true;
            }
            catch (Exception ex)
            {
                // Handle duvidoso: trata como "de missão" e deixa o veículo em
                // paz. Melhor manter uma carcaça do que arriscar outro crash.
                ModLog.WriteException("HasMissionOccupant", ex);
                return true;
            }
        }

        return false;
    }

    // Remove os pedestres que o próprio caos criou e que já estão soltos na
    // rua. Escopo deliberadamente estreito: só ped que ESTE mod tirou de um
    // carro (rastreado em _freedDrivers) ou cadáver. A população normal da
    // cidade não é tocada — apagar qualquer pedestre fora da tela esvaziaria
    // as calçadas e daria um mundo morto.
    private int CleanupLooseChaosPeds(Vector3 playerPos)
    {
        if (_freedDrivers.Count == 0)
            return 0;

        var removable = new List<Ped>();

        foreach (Ped p in _freedDrivers.Keys)
        {
            if (p == null || !p.Exists())
            {
                removable.Add(p);
                continue;
            }

            try
            {
                // Ainda dirigindo: é do caos, não é detrito.
                if (p.isInVehicle())
                    continue;

                if (p == Player.Character || p.isRequiredForMission)
                    continue;

                // Mesmas regras dos veículos: nada some em cena nem perto demais.
                if (p.isOnScreen || playerPos.DistanceTo(p.Position) < DebrisMinDistance)
                    continue;

                removable.Add(p);
            }
            catch (Exception ex)
            {
                // Handle que passou por Exists() mas explode num native de
                // verdade: para de rastrear e não encosta mais nele.
                ModLog.WriteException("CleanupLooseChaosPeds", ex);
                removable.Add(p);
            }
        }

        int removed = 0;

        foreach (Ped p in removable)
        {
            _freedDrivers.Remove(p);

            try
            {
                if (p != null && p.Exists())
                {
                    p.Delete();
                    removed++;
                }
            }
            catch (Exception ex)
            {
                ModLog.WriteException("CleanupLooseChaosPeds.Delete", ex);
            }
        }

        return removed;
    }

    private bool IsDebris(Vehicle v)
    {
        // Sucata de verdade: carcaça que não anda mais, capotada, pegando fogo
        // ou já destruída. Carro saudável e parado NÃO conta — senão o veículo
        // que o jogador acabou de spawnar e estacionou sumiria pelas costas
        // dele, e a limpeza viraria um ladrão de carros.
        if (!v.isDriveable || v.isUpsideDown || v.isOnFire || v.Health <= 0)
            return true;

        // isSeatFree antes de pedir o ped: mesmo cuidado de GetOccupants, para
        // nunca tocar num handle que o modelo não tem.
        if (v.isSeatFree(VehicleSeat.Driver))
            return false;

        Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);
        return driver != null && driver.Exists() && driver.isDead;
    }

    // Quanto do empurrão por tick vale neste frame. Só os modos acumulativos
    // (Legado e Solto) usam isto — Velocidade Máxima e Ímã escrevem valores
    // absolutos, que já independem da taxa de quadros.
    private void UpdateTurboFrameScale()
    {
        int now = Game.GameTime;

        // Primeiro tick, ou relógio do jogo reiniciado: assume um frame de
        // referência em vez de um delta gigante ou negativo.
        if (_lastTurboTime == 0 || now < _lastTurboTime)
        {
            _lastTurboTime = now;
            _turboFrameScale = 1.0f;
            return;
        }

        float deltaSeconds = (now - _lastTurboTime) / 1000.0f;
        _lastTurboTime = now;

        float scale = deltaSeconds * BoostReferenceFps;

        if (scale > BoostMaxFrameScale)
            scale = BoostMaxFrameScale;

        if (scale < 0.0f)
            scale = 0.0f;

        _turboFrameScale = scale;
    }

    // Duração deste frame em segundos, derivada da mesma escala (e portanto já
    // com o mesmo teto contra pausas e telas de loading). Serve para as taxas
    // expressas por SEGUNDO, como a rampa do Modo Velocidade Máxima.
    private float FrameSeconds()
    {
        return _turboFrameScale / BoostReferenceFps;
    }

    // Motorista do veículo, ou null. Nunca lança: um handle ruim vira "sem
    // motorista", e o carro segue para as regras de veículo sem condutor em vez
    // de derrubar a varredura.
    private Ped TryGetDriver(Vehicle v)
    {
        try
        {
            Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);

            if (driver == null || !driver.Exists())
                return null;

            return driver;
        }
        catch (Exception ex)
        {
            ModLog.WriteException("TryGetDriver", ex);
            return null;
        }
    }

    // Coloca o veículo em condição de OBEDECER ao impulso.
    //
    // Escrever velocidade num carro que o jogo mantém suspenso não faz nada, e
    // não faz nada em silêncio — nenhum erro, nenhum log, o carro simplesmente
    // segue seu caminho como se o mod não existisse. Era a explicação mais
    // provável para "vários veículos não tomam efeito mesmo com o motorista
    // dentro": trânsito recém-nascido nasce parado e preso enquanto o jogo o
    // encaixa na rua, e um carro estacionado por script fica congelado até
    // alguém encostar nele.
    //
    private void WakeVehicle(Vehicle v)
    {
        try
        {
            // FreezePosition é só de ESCRITA nesta biblioteca — não há como
            // perguntar se o carro está congelado, então a única opção é
            // descongelar sempre. Uma native por carro por frame, que é a
            // mesma ordem de grandeza do resto da varredura.
            v.FreezePosition = false;

            // Motor desligado não impede a escrita de velocidade, mas impede o
            // carro de SEGURAR essa velocidade: sem motor ele desacelera de
            // volta a zero entre um tick e outro, e o efeito vira um solavanco.
            // Esta, ao contrário de FreezePosition, dá para ler — então só
            // escreve quando há o que mudar.
            if (!v.EngineRunning)
                v.EngineRunning = true;
        }
        catch (Exception ex)
        {
            ModLog.WriteException("WakeVehicle", ex);
        }
    }

    // Resumo periódico da varredura. É a resposta para "por que aquele carro
    // não pegou o efeito?" — mostra quantos veículos existem, quantos entraram
    // no raio, quantos receberam impulso e quantos foram recusados por cada
    // filtro. Sai a cada TurboSweepLogIntervalMs para não inundar o log, e só
    // quando alguma contagem muda, para não repetir a mesma linha parada.
    private void LogTurboSweep(int seen, int inRange, int affected,
        int skipNoDriver, int skipUpsideDown, int skipMission, int skipError)
    {
        int now = Game.GameTime;

        if (now - _lastTurboLogTime < TurboSweepLogIntervalMs)
            return;

        string summary = "varredura turbo: total=" + seen
            + " noRaio=" + inRange
            + " impulsionados=" + affected
            + " semMotorista=" + skipNoDriver
            + " capotados=" + skipUpsideDown
            + " missao=" + skipMission
            + " erros=" + skipError;

        if (summary == _lastTurboSummary)
            return;

        _lastTurboLogTime = now;
        _lastTurboSummary = summary;
        ModLog.Write(summary);
    }

    private void ApplyTurboLogic()
    {
        UpdateTurboFrameScale();

        Vehicle playerVeh = null;
        bool playerInVehicle = Player.Character.isInVehicle();

        if (playerInVehicle)
            playerVeh = Player.Character.CurrentVehicle;

        float radius = ModSettings.ChaosRadius;
        bool legacyWanted = ModSettings.ChaosEnabled;
        bool maxSpeedWanted = ModSettings.ChaosMaxSpeedModeEnabled;
        bool magnetWanted = ModSettings.ChaosMagnetModeEnabled;
        bool freeRideWanted = ModSettings.ChaosFreeRideModeEnabled;

        bool driverlessWanted = ModSettings.ChaosAffectDriverlessVehicles;

        var freedThisTick = new HashSet<Ped>(PedByHandle);

        // Contadores da varredura — sem eles, "vários veículos não pegam o
        // efeito" é impossível de investigar: não dá para saber se o carro foi
        // recusado por algum filtro, e por qual, ou se recebeu o impulso e a
        // física é que não obedeceu. Vão para o log a cada poucos segundos.
        int seen = 0, inRange = 0, affected = 0;
        int skipNoDriver = 0, skipUpsideDown = 0, skipMission = 0, skipError = 0;

        foreach (Vehicle v in World.GetAllVehicles())
        {
            if (v == null || !v.Exists())
                continue;

            seen++;

            // Nunca alterar carro do player
            if (playerInVehicle && v == playerVeh)
                continue;

            // Limite de distância (raio global, compartilhado por todos os modos)
            if (Player.Character.Position.DistanceTo(v.Position) > radius)
                continue;

            inRange++;

            // Isolamento por veículo: um handle problemático faz ESTE carro ser
            // pulado, não o tick inteiro morrer — o mesmo padrão já usado na
            // limpeza de detritos. Antes, um único carro ruim derrubava o
            // impulso de todos os outros naquele frame, o que é exatamente o
            // sintoma "vários veículos não tomam efeito".
            try
            {
                // O motorista é pedido DIRETO, sem passar por isSeatFree.
                //
                // A guarda de assento existe para os bancos de PASSAGEIRO, que
                // um modelo pode não ter (ver GetOccupants). O banco do
                // motorista todo veículo tem — então ali ela não protegia de
                // nada e só acrescentava mais um native capaz de responder
                // "livre" e fazer um carro com motorista ser pulado em
                // silêncio. Quem valida o resultado é o Exists() abaixo.
                Ped driver = TryGetDriver(v);

                if (driver != null && driver == Player.Character)
                    continue;

                // Sem motorista: carro estacionado, ou o motorista ainda não
                // foi carregado pelo jogo. Com "afetar sem motorista" ligado
                // (o padrão), o impulso vale igual — é o que faz TODO veículo
                // do raio carregar o efeito, inclusive os que acabaram de
                // aparecer e ainda não têm ninguém ao volante.
                if (driver == null && !driverlessWanted)
                {
                    skipNoDriver++;
                    continue;
                }

                // Veículo/ped marcado como necessário para missão (o carro
                // perseguido, o alvo, o comparsa). Ligado — que é o padrão —, os
                // impulsos alcançam a perseguição também: é onde o caos no
                // trânsito faz mais graça, e o carro do alvo imune ao turbo
                // destoava de tudo o mais que se move na tela.
                //
                // O risco é real e é do jogador: empurrar o alvo pode jogá-lo para
                // longe do trajeto que o script da missão espera, e uma missão
                // pode ficar impossível de completar. Por isso é opção, e por isso
                // ela se limita aos impulsos — invencibilidade e limpeza de
                // detritos continuam respeitando a marcação, sempre.
                if (!ModSettings.ChaosAffectMissionVehicles
                    && (v.isRequiredForMission || (driver != null && driver.isRequiredForMission)))
                {
                    skipMission++;
                    continue;
                }

                // Não turbar veículo capotado. NÃO exigir isOnAllWheels aqui: o
                // Modo Legado é cumulativo e só escala se for reaplicado a cada
                // tick — e o primeiro empurrão já tira as rodas do chão, então
                // exigir "nas quatro rodas" cancelava todos os empurrões
                // seguintes e matava justamente o efeito original do modo.
                if (v.isUpsideDown)
                {
                    skipUpsideDown++;
                    continue;
                }

                // Tira o veículo de qualquer estado em que ele ignoraria o
                // impulso — ver WakeVehicle. É isto que faz carro recém-nascido
                // no trânsito entrar no efeito junto com os outros, em vez de
                // ficar imune até o jogo resolver simulá-lo por conta própria.
                WakeVehicle(v);

                // Cada modo é aplicado por um método próprio que desconhece os
                // outros — evita que a mecânica de um vaze para o outro. A ordem
                // é a composição combinada: Legado, Velocidade Máxima e Solto
                // mexem na velocidade; o Ímã, por último, redireciona o resultado.
                // Combinar Ímã com Solto dá carros sem IA sendo arrastados até o
                // jogador; Solto sozinho é o carro entregue à física pura.
                if (legacyWanted)
                    ApplyLegacyBoost(v);

                if (maxSpeedWanted)
                    ApplyMaxSpeedMode(v);

                if (freeRideWanted)
                {
                    ApplyFreeRideMode(v, driver);

                    if (driver != null)
                        freedThisTick.Add(driver);
                }

                if (magnetWanted)
                    ApplyMagnetMode(v);

                affected++;
            }
            catch (Exception ex)
            {
                skipError++;
                ModLog.WriteException("ApplyTurboLogic.veiculo", ex);
            }
        }

        LogTurboSweep(seen, inRange, affected, skipNoDriver, skipUpsideDown, skipMission, skipError);

        // Motorista que saiu do raio, ou o modo inteiro desligado: a IA volta.
        if (freeRideWanted)
            RestoreFreedDriversNotTouched(freedThisTick);
        else
            RestoreAllFreedDrivers();
    }

    // Dano/invencibilidade é uma passada PRÓPRIA, separada do turbo: roda todo
    // tick e não depende de nenhum modo de movimento estar ligado. Antes as
    // flags eram aplicadas dentro do laço do turbo, então ligar só "Veículos
    // invencíveis" no HUD não fazia absolutamente nada — o laço nem era
    // chamado. Também não exige NPC ao volante: carro estacionado no raio
    // igualmente fica invencível, que é o que o jogador espera ao testar.
    private void ApplyDamageFlags()
    {
        bool invincibleWanted = ModSettings.ChaosInvincible;
        bool crumpleWanted = ModSettings.ChaosCrumple;

        // "Não invencível + amassa" é exatamente o padrão do jogo: não há nada
        // a impor, só a devolver para quem já foi alterado antes.
        if (!invincibleWanted && crumpleWanted)
        {
            _appliedInvincible = false;
            _appliedCrumple = true;
            RestoreAllTweakedVehicles();
            return;
        }

        bool settingsChanged = invincibleWanted != _appliedInvincible || crumpleWanted != _appliedCrumple;

        Vehicle playerVeh = null;
        bool playerInVehicle = Player.Character.isInVehicle();

        if (playerInVehicle)
            playerVeh = Player.Character.CurrentVehicle;

        float radius = ModSettings.ChaosRadius;
        var touchedThisTick = new HashSet<Vehicle>(VehicleByHandle);

        foreach (Vehicle v in World.GetAllVehicles())
        {
            if (v == null || !v.Exists())
                continue;

            if (playerInVehicle && v == playerVeh)
                continue;

            if (Player.Character.Position.DistanceTo(v.Position) > radius)
                continue;

            if (v.isRequiredForMission)
                continue;

            // Reemitir os mesmos natives a cada frame para o mesmo veículo é
            // desperdício puro (são 4 chamadas por carro, por frame). Só
            // reaplica quem ainda não foi tocado ou quando a configuração
            // mudou desde a última aplicação.
            if (!settingsChanged && _tweakedVehicles.Contains(v))
            {
                touchedThisTick.Add(v);
                continue;
            }

            ApplyDamageState(v, invincibleWanted, crumpleWanted);
            touchedThisTick.Add(v);
        }

        _appliedInvincible = invincibleWanted;
        _appliedCrumple = crumpleWanted;

        RestoreVehiclesNotTouched(touchedThisTick);
    }

    // Invencibilidade do JOGADOR — passada própria, independente de tudo o que
    // acontece com o trânsito.
    //
    // Ligada, é reemitida a cada tick em vez de só na mudança: o jogo repõe as
    // flags do personagem sozinho em vários momentos (troca de roupa, início e
    // fim de missão, hospital, cutscene), e um "aplica uma vez" silenciosamente
    // deixaria de valer no primeiro deles. São duas natives por frame — barato
    // perto de morrer achando que estava invencível.
    private void ApplyPlayerInvincibility()
    {
        bool wanted = ModSettings.PlayerInvincible;

        if (!wanted && !_appliedPlayerInvincible)
            return;

        try
        {
            Ped player = Player.Character;

            if (player == null || !player.Exists())
                return;

            // Invincible sozinho corta o dano de vida, mas fogo e explosão
            // continuam derrubando o personagem — mesma lição já aprendida com
            // os veículos em ApplyDamageState. MakeProofTo(balas, fogo,
            // explosões, colisões, corpo a corpo) é o que fecha o resto.
            player.Invincible = wanted;
            player.MakeProofTo(wanted, wanted, wanted, wanted, wanted);

            _appliedPlayerInvincible = wanted;
        }
        catch (Exception ex)
        {
            ModLog.WriteException("ApplyPlayerInvincibility", ex);
        }
    }

    private void ApplyDamageState(Vehicle v, bool invincible, bool crumple)
    {
        // CanBeDamaged sozinho NÃO blinda um carro em GTA IV: ele corta o dano
        // de "saúde", mas colisão, fogo e explosão continuam destruindo o
        // veículo — era por isso que ligar "Veículos invencíveis" parecia não
        // fazer efeito. Quem realmente blinda é SET_CAR_PROOFS, exposto aqui
        // como MakeProofTo(balas, fogo, explosões, colisões, corpo a corpo).
        v.MakeProofTo(invincible, invincible, invincible, invincible, invincible);
        v.CanBeDamaged = !invincible;
        v.CanTiresBurst = !invincible;

        // Carro invencível que continua amassando fica visualmente incoerente
        // (lataria destruída, saúde intacta): invencível manda na deformação.
        v.CanBeVisiblyDamaged = !invincible && crumple;

        _tweakedVehicles.Add(v);
    }

    private void RestoreVehicleDamageDefaults(Vehicle v)
    {
        v.MakeProofTo(false, false, false, false, false);
        v.CanBeDamaged = true;
        v.CanBeVisiblyDamaged = true;
        v.CanTiresBurst = true;
    }

    // As flags de dano são persistentes — o jogo não as reseta sozinho.
    // Qualquer veículo alterado que saiu do raio, deixou de existir ou virou
    // mission car precisa ter o comportamento normal restaurado aqui.
    private void RestoreVehiclesNotTouched(HashSet<Vehicle> touchedThisTick)
    {
        if (_tweakedVehicles.Count == 0)
            return;

        var stale = new List<Vehicle>();

        foreach (Vehicle v in _tweakedVehicles)
        {
            if (touchedThisTick.Contains(v))
                continue;

            stale.Add(v);

            if (v != null && v.Exists())
                RestoreVehicleDamageDefaults(v);
        }

        foreach (Vehicle v in stale)
            _tweakedVehicles.Remove(v);
    }

    // Chamado quando invencível/amassar voltam ao padrão: garante que nenhum
    // veículo fica preso indestrutível depois que o efeito acaba.
    private void RestoreAllTweakedVehicles()
    {
        if (_tweakedVehicles.Count == 0)
            return;

        foreach (Vehicle v in _tweakedVehicles)
        {
            if (v != null && v.Exists())
                RestoreVehicleDamageDefaults(v);
        }

        _tweakedVehicles.Clear();
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
            return;
        }

        // Conserto pela tecla, e não só pela aba Configurações: o momento em
        // que o carro precisa ser consertado é justamente dirigindo, e abrir o
        // HUD trava o personagem no meio da rua.
        if (key == Keys.F7)
        {
            Game.DisplayText(PlayerActions.RepairCurrentVehicle(), 2500);
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
            // Os valores numéricos vão junto dos liga/desliga: sem eles, o log
            // não permitia checar se um ajuste de velocidade feito no HUD
            // chegou de fato à lógica — só se o modo estava ligado.
            + " modoLegado=" + ModSettings.ChaosEnabled + "/" + ModSettings.ChaosTurboPower
            + " modoVelMax=" + ModSettings.ChaosMaxSpeedModeEnabled + "/" + ModSettings.ChaosMaxSpeed
            + " modoIma=" + ModSettings.ChaosMagnetModeEnabled + "/" + ModSettings.ChaosMagnetPull
            + " modoSolto=" + ModSettings.ChaosFreeRideModeEnabled + "/" + ModSettings.ChaosFreeRidePush
            + " raio=" + ModSettings.ChaosRadius
            + " afetaMissao=" + ModSettings.ChaosAffectMissionVehicles
            + " escalaFrame=" + _turboFrameScale.ToString("0.00")
            + " motoristasSemIA=" + _freedDrivers.Count
            + " playerInvencivel=" + ModSettings.PlayerInvincible
            + " invencivel=" + ModSettings.ChaosInvincible
            + " amassa=" + ModSettings.ChaosCrumple
            + " veicAlterados=" + _tweakedVehicles.Count);
    }

    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            WriteHeartbeat();
            ShowTutorialIfNeeded();
            ProcessInput();
            WarnIfDrawingUnavailable();
            _hud.Update();

            // Enquanto o HUD está aberto, o personagem fica travado para que as
            // setas naveguem o menu sem também mover o jogador.
            if (_hud.IsOpen)
                Player.CanControlCharacter = false;

            if (ModSettings.ChaosEnabled || ModSettings.ChaosMaxSpeedModeEnabled
                || ModSettings.ChaosMagnetModeEnabled || ModSettings.ChaosFreeRideModeEnabled)
                ApplyTurboLogic();
            else
                RestoreAllFreedDrivers();

            // Fora do if de propósito: invencibilidade e deformação valem por
            // si só, sem exigir nenhum modo de movimento ligado.
            ApplyDamageFlags();
            ApplyPlayerInvincibility();
            CleanupOffscreenDebris();
        }
        catch (Exception ex)
        {
            ModLog.WriteException("OnTick", ex);
        }
    }
}
