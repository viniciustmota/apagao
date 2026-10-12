# 03 — Núcleo de IA (máquina de estados)

**Status:** Implementada · **Marco:** 2 · **Código:** `Scripts/AI/Core/StateMachine.cs`

## Objetivo
Uma máquina de estados genérica, reaproveitada por todos os inimigos, em que cada estado é uma classe.

## Requisitos
- **R1** `State<TOwner>` com `Enter`, `Tick`, `Exit` e `Name`.
- **R2** `StateMachine<TOwner>` com `Current`, `Previous`, `TimeInState` e `ChangeState`.
- **R3** Trocar para o mesmo estado não faz nada; trocar chama `Exit` do atual e `Enter` do novo, nesta ordem.
- **R4** Evento `Changed(anterior, novo)` para áudio, HUD e rede.
- **R5** `Previous` permite estados temporários voltarem ao anterior (ex.: Paralisado, spec 11).
- **R6** Sem alocação por frame.

## Critérios de aceite
- [ ] A Sentinela percorre todos os 5 estados sem erro no Console.
- [ ] O label de debug mostra o estado correto em tempo real.

## Design técnico
- O controlador do inimigo cria as instâncias de estado no `Awake` e as expõe como propriedades (`Owner.Patrulha`).
- Estados não guardam referência a outros estados; pedem ao `Owner`.
- Estados temporários: ao sair, `Fsm.ChangeState(Fsm.Previous)`.

## Fora do escopo
Behaviour trees, FSM hierárquica. Reavaliar só se um inimigo passar de 8 estados.
