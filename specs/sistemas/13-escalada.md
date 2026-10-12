# 13 — Escalada e telhados

**Status:** Aprovada · **Marco:** 9

## Objetivo
Os telhados são a rota alternativa da vila: seguros contra cães, expostos a drones.

## Requisitos
- **R1** Ledge climb: correndo ou pulando em direção a uma borda entre 1,2 m e 2,4 m acima dos pés, o Caio sobe automaticamente.
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
- `LedgeClimber : MonoBehaviour` no Caio: raycast frontal no peito, raycast para baixo a partir de cima da borda para achar o topo.
- `PlayerController` ganha estado `Escalando` (desliga gravidade e input).
- O `NavMeshBaker` marca superfícies acima de 2,5 m do terreno como área não caminhável para o agente "Cão".
