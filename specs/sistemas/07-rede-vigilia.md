# 07 — Rede da VIGÍLIA (mente coletiva)

**Status:** Implementada · **Marco:** 4 · **Código:** `AI/Hive/`

## Objetivo
As máquinas compartilham tudo o que veem. Apagar uma delas tem consequência. O jogador precisa pensar na ordem: antena primeiro, robôs depois.

## Requisitos

### Compartilhamento
- **R1** Cada área tem uma `HiveNetwork`. Todo inimigo tem um `HiveMember` que se registra na rede da sua área.
- **R2** Quando um membro vê o Caio (em Suspeito com visibilidade ≥ 0,5, ou em Combate), reporta à rede a posição. A rede repassa na hora a todos os membros: `LastKnownPosition` e alerta elevado.
- **R3** **Uma vez visto, marcado:** a primeira detecção confirmada (Combate) liga `PlayerSpotted` na rede até o fim do ato. Esconder-se tira a posição, nunca a certeza.
- **R4** Com `PlayerSpotted`, membros que não estão em combate:
  - a até 40 m da posição reportada → Investigando, na velocidade de perseguição;
  - mais longe → continuam a patrulha em alerta elevado.

### Check-in
- **R5** Cada membro faz check-in a cada 15 s (fase aleatória por membro).
- **R6** Membro destruído **sem estar em alerta** não reporta nada. A rede só descobre no próximo check-in perdido.
- **R7** Membro destruído **fora da Patrulha** (Suspeito, Investigando, Combate ou Procurando) transmite um sinal de morte imediato com a posição.
- **R8** Check-in perdido → a rede manda os 2 membros mais próximos investigar a última posição do desaparecido e liga o alerta elevado de todos.

### Contra-jogadas
- **R9** Cada área tem uma `HiveAntenna`. Sabotada (facão: segurar E por 2 s; ou 1 descarga de PEM), a rede da área fica offline.
- **R10** Rede offline: sem compartilhamento, sem check-in, sem sinal de morte. Cada robô age só com os próprios sentidos.
- **R11** Robô paralisado pelo PEM fica offline por 3 s: um takedown nessa janela não gera sinal de morte (cai no R6).
- **R12** A sabotagem da antena gera ruído de 8 m e muda o visual (luz vermelha apaga).

### Feedback
- **R13** HUD mostra o relógio do próximo check-in só quando um membro está desaparecido (ver spec 20). *Hoje no `DebugHUD` provisório.*
- **R14** Debug: caixa da área e linhas da rede até os membros no Scene view (selecionando a rede).

## Critérios de aceite
- [ ] A Sentinela 1 te vê; a Sentinela 2, do outro lado da vila, vai até o ponto.
- [ ] Takedown furtivo na Sentinela 1: nada acontece até o próximo check-in; aí a Sentinela 2 vai investigar o corpo.
- [ ] Com a antena sabotada, o mesmo takedown nunca é descoberto pela rede.
- [ ] PEM + takedown de frente não gera alerta imediato.

## Design técnico
```csharp
public class HiveNetwork : MonoBehaviour   // uma por área
{
    bool Online; bool PlayerSpotted; Vector3 LastReportedPosition; float LastReportTime;
    void Register(HiveMember m); void Unregister(HiveMember m);
    void ReportSighting(HiveMember from, Vector3 pos);
    void ReportDeath(HiveMember m, bool aware);
    event Action<Vector3> SightingBroadcast; event Action<HiveMember> MemberMissing;
}
public class HiveMember : MonoBehaviour { HiveNetwork Network; bool Offline; float lastCheckIn; }
public class HiveAntenna : MonoBehaviour, IInteractable, IStunnable { HiveNetwork network; }
```
- Inimigos implementam `IHiveAware` (`IsAware`, `OnHiveSighting(pos, confirmed, converge)`, `OnHiveMemberMissing(pos, investigate)`) e decidem a transição de estado.
- A rede calcula quem converge (raio de 40 m) e quem investiga um desaparecido (os 2 mais próximos).
- A área de cada rede é uma caixa (`areaSize`, sem colisor); o membro se registra na rede cuja área contém sua posição inicial.

## Questões abertas
- A rede deve ter um "relé" entre áreas (o Capataz ouve tudo)? Proposta: só no ato 4.
