# 16 — Brutamontes

**Status:** Implementada (falta testar) · **Marco:** 11

## Objetivo
Um mini-chefe que o jogador aprende a evitar. Enfrentar de frente é quase suicídio; derrubar exige preparo.

## Requisitos
- **R1** Humanoide pesado (robô de carga reaproveitado), 2,8 m de altura, 400 de vida.
- **R2** Lento: 2 m/s; vira devagar (90°/s). Visão em cone estreito (70°, 18 m).
- **R3** Blindagem frontal: dano ×0,25 pela frente. Juntas das pernas e costas expostas.
- **R4** Ataques: soco (40, alcance 2,5 m, aviso de 0,8 s) e investida em linha reta (50, 8 m, aviso de 1,2 s, fica 2 s atordoado se bater em parede).
- **R5** PEM paralisa só 1,5 s. Molotov nas juntas causa ×3.
- **R6** Takedown só com vida < 30% e paralisado (spec 09).
- **R7** Passos fazem o chão tremer (câmera) a até 15 m: o jogador sempre sabe onde ele está.

## Critérios de aceite
- [ ] É possível atravessar a área dele sem lutar.
- [ ] Investida + parede + molotov + PEM + takedown derruba o Brutamontes com os recursos de uma área.

## Design técnico
- `BrutamontesAI : MachineAI` com estados próprios em `States/`: Patrulha, Suspeito, Investigando, Combate, Atordoado, Procurando.
- `CombateBrutoState` tem fases internas: Perseguindo → Soco (braço sobe por 0,8 s) ou PreparandoInvestida (1,2 s; a direção trava nos últimos 0,35 s) → Investida.
- Investida anda com `NavMeshAgent.Move`; parede detectada por SphereCast à frente → `AtordoadoState` (2 s). Recarga de 6 s entre investidas.
- Blindagem: `Health.frontMultiplier` 0,25 (golpes sem direção, como fogo, ignoram). Costas ×1,5. Fogo ×3 (o fogo do molotov pega nas pernas).
- PEM: `stunDurationMultiplier` 0,5 (3 s → 1,5 s). Takedown: `CanBeTakenDown` exige paralisado e vida < 30%.
- Passos: tremor de câmera proporcional à distância, até 15 m. Recuo de golpes ×0,2.
- Novo `DamageType.Impacto` para soco e investida.

### Decisões fechadas
- A investida **não** quebra paredes no MVP (ficaria para quando houver paredes de madeira destrutíveis no mapa).
- Atordoado (parede) não libera takedown sozinho: é a janela para PEM e molotov.
