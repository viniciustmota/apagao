# 13 — Escalada e telhados

**Status:** Implementada · **Marco:** 9 · **Código:** `Player/LedgeClimber.cs`, `Player/PlayerController.cs`, `Player/PlayerFootsteps.cs`

## Objetivo
Os telhados são a rota alternativa da vila: seguros contra cães, expostos a drones.

## Requisitos
- **R1** Ledge climb: de frente para uma borda entre 1,2 m e 2,6 m acima dos pés, **Espaço sobe** em vez de pular; no ar, empurrar contra a borda também agarra. *(Altura máxima ajustada de 2,4 para 2,6 m para o caixote de 1 m alcançar o telhado de 3 m.)*
- **R2** Só sobe se o topo tiver espaço livre para a cápsula em pé (`CheckCapsule`).
- **R3** Subida em 0,6 s por curva (sobe, depois avança), controle travado durante.
- **R4** Pular entre telhados com vão de até 2 m usa o pulo normal.
- **R5** Descer: andar para fora da borda. Queda acima de 2 m faz ruído (spec 06); acima de 6 m causa dano de 10 por metro excedente.
- **R6** Agachado na borda, o Caio não cai (trava na borda).
- **R7** Telhados e áreas acima de 2,5 m ficam fora do NavMesh dos cães (área `Telhado`).

## Critérios de aceite
- [ ] Do chão, sobe numa casa de 3 m usando os caixotes, e numa de 2,4 m direto.
- [ ] Atravessa a fileira norte inteira pelos telhados.
- [ ] Um cão perseguindo fica embaixo, rodeando a casa.

## Design técnico
- `LedgeClimber` no Caio (roda antes do `PlayerController` e marca `JumpSuppressed` quando usa o Espaço): raycast frontal no peito, raycast para baixo depois da face da parede para achar o topo, `OverlapCapsule` para conferir espaço.
- Durante a subida: `InputLocked` e `CharacterController` desligado; movimento em duas fases (sobe, avança).
- ~~Área de NavMesh para telhados~~ Decidido: não é preciso. Os telhados não têm rampa até o chão, então ficam desconectados no NavMesh e nenhum agente chega lá. O `MoveTo` manda o robô para o ponto mais próximo alcançável, embaixo da casa.
- HUD mostra "Espaço — Escalar" quando há borda à frente.
