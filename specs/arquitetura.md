# Arquitetura

O jogo é organizado em camadas. Uma camada só depende das camadas abaixo dela, e o contato entre sistemas da mesma camada acontece por interfaces e eventos.

```
┌──────────────────────────────────────────────────────────────┐
│ Jogo         Campanha · Objetivos · Rádio · HUD · Áudio       │
├──────────────────────────────────────────────────────────────┤
│ Conteúdo     Sentinela · Matilha · Drones · Brutamontes ·     │
│              Capataz · Facão · Armas · PEM · Crafting         │
├──────────────────────────────────────────────────────────────┤
│ Sistemas     StateMachine · VisionSensor · NoiseSystem ·      │
│              HiveNetwork · Health · Inventory · Interaction   │
├──────────────────────────────────────────────────────────────┤
│ Base         PlayerController · ThirdPersonCamera ·           │
│              NavMeshBaker · Input System · URP                │
└──────────────────────────────────────────────────────────────┘
```

## Pastas e namespaces

```
Assets/_Apagao/
  Scripts/
    Player/        Apagao.Player       Movimento, câmera, escalada, ações do Caio
    AI/Core/       Apagao.AI           State, StateMachine
    AI/Senses/     Apagao.AI           VisionSensor, HearingSensor
    AI/Hive/       Apagao.AI.Hive      HiveNetwork, HiveMember, HiveAntenna
    AI/<Inimigo>/  Apagao.AI.<Inimigo> Controlador + States/ de cada inimigo
    AI/Drones/     Apagao.AI.Drones    DroneMotor, DroneAI, Olho, Vespa, Brasa (FSM por enum)
    Combat/        Apagao.Combat       Health, DamageInfo, Facao, armas, PEM
    Crafting/      Apagao.Crafting     Recursos, inventário, receitas, bancada
    Systems/       Apagao.Systems      NoiseSystem, NavMeshBaker, interação, checkpoints
    Game/          Apagao.Game         Campanha, objetivos, rádio
    UI/            Apagao.UI           HUD, menus
  Editor/          Apagao.EditorTools  Builders de cena e ferramentas
  Data/                                ScriptableObjects de balanceamento
  Scenes/  Materials/  Prefabs/  Terrain/  Audio/
```

## Interfaces centrais

```csharp
// Combate
public interface IDamageable { void TakeDamage(in DamageInfo info); bool IsDead { get; } }
public interface IStunnable  { void Stun(float seconds); bool IsStunned { get; } }

// Percepção
public interface INoiseListener { void OnNoise(in NoiseEvent e); }

// Mundo
public interface IInteractable { string Prompt { get; } bool CanInteract(GameObject who); void Interact(GameObject who); }
```

`DamageInfo`, `NoiseEvent` e `DamageType` são definidos nas specs 08 e 06.

## Padrão de cada inimigo

Todo inimigo segue o mesmo formato da Sentinela:

1. **Controlador** (`<Inimigo>AI : MachineAI`). A base `MachineAI` (`AI/Core/`) já traz sentidos, suspeita, rede, vida com recuo, takedown, carcaça, indicador e as ações comuns (`MoveTo`, `FaceTowards`, `FillSuspicion`). A subclasse define os estados e as reações (`HandleNoise`, `HandleHiveSighting`, `EnterCombat`).
2. **Estados** (`<Nome>State : State<<Inimigo>AI>`), um por arquivo, decidem as transições.
3. **Componentes compartilhados:** `VisionSensor`, `HearingSensor`, `Health`, `HiveMember`.
4. **Feedback:** indicador de alerta acima da cabeça e label de debug com o estado.

## Fluxo de um alerta (visão geral)

```
Caio faz barulho ──► NoiseSystem.Emit ──► HearingSensor do robô ──► Suspeito / Investigando
Robô vê o Caio  ──► VisionSensor ──► FSM: Combate ──► HiveMember.Report ──► HiveNetwork
HiveNetwork ──► todos os membros da área: LastKnownPosition + alerta elevado + convergência
Robô destruído sem alerta ──► falta no check-in ──► HiveNetwork manda os mais próximos investigar
Antena sabotada ──► HiveNetwork da área offline ──► robôs agem só com os próprios sentidos
```

## Camadas de física

| Layer | Uso |
| --- | --- |
| Default | Cenário geral |
| Player | Caio |
| Enemy | Robôs e drones |
| Obstacle | Bloqueia visão e projéteis (paredes, caixotes) |
| Vegetation | Mato alto: esconde da visão comum, não da térmica, não bloqueia movimento |
| Water | Água: esconde da térmica |

As layers ainda não existem no projeto; são criadas junto com a spec 06 (ruído) e a 15 (drones). Até lá, `VisionSensor` usa `~0` e ignora o próprio alvo.

## Decisões registradas

| Data | Decisão | Motivo |
| --- | --- | --- |
| 2026-10-12 | Controle e câmera próprios em vez do Starter Assets | Menos dependências e código que o projeto entende por inteiro |
| 2026-10-12 | NavMesh gerado em runtime sem o pacote AI Navigation | Evita dependência de versão; o mapa é pequeno |
| 2026-10-12 | Cena de teste gerada por código | Recriável a qualquer momento, sem trabalho manual no Editor |
| 2026-10-12 | FSM com uma classe por estado | Escala melhor que `switch` quando os estados passam de 5 |
| 2026-10-12 | Base `MachineAI` para máquinas terrestres | Sentinela e Matilha compartilhavam 70% do código; drones terão base própria (voo) |
| 2026-10-12 | Drones com base `DroneAI` + `DroneMotor` e FSM por `enum` | Voam sem NavMesh; estados de poucas linhas não justificam uma classe cada (exceção à regra de uma classe por estado) |
| 2026-10-12 | Sons sintetizados em `Resources/Audio`, tocados por nome (`Sfx`) | Sem depender de download nem licença; trocar um som é sobrescrever o `.wav` |
| 2026-10-12 | Checkpoint por `SaveId` + evento `SaveId.Gone` | O jogo (camada de cima) ouve o que some sem que inimigos e itens dependam dele |
| 2026-10-12 | Objetivos como dados serializados no `Campaign` | Mais simples que ScriptableObject enquanto há uma só cena; migra junto com `ActDefinition` |
| 2026-10-12 | `FireArea.Spawn` com raio, duração e dano | O mesmo fogo serve ao molotov (4 m, 8 s) e à Brasa (6 m, 15 s) |
