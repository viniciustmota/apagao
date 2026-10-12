# 14 — Matilha (cães robô)

**Status:** Aprovada · **Marco:** 6

## Objetivo
O inimigo mais assustador do jogo. Caçam em grupo, seguem som e rastro, e impedem o jogador de ficar parado escondido.

## Requisitos
- **R1** Grupos de 2 a 3 cães com um `MatilhaGroup` coordenando.
- **R2** Sentidos: visão curta (10 m, 140°), audição forte (raio de ruído ×2), faro (rastro).
- **R3** O Caio deixa um ponto de rastro a cada 1 s, que dura 20 s. Agachado e na água não deixa rastro.
- **R4** Estados: Patrulha, Suspeito, **Rastreando** (segue o rastro mais recente), Combate, Procurando, Paralisado.
- **R5** Velocidade de 7 m/s em combate: mais rápido que o Caio correndo.
- **R6** Combate em grupo: um cão **segura** (encara e avança devagar), os outros **flanqueiam** em ângulos de ±60°.
- **R7** Ataque: bote de 20 de dano com 1 s de recarga, precedido de 0,4 s de aviso (agacha e vibra).
- **R8** Não sobem em telhados nem caixotes acima de 1 m (spec 13).
- **R9** Fraquezas: juntas (facão ×2), PEM paralisa o grupo, isca elétrica atrai e prende 5 s, molotov ×1,5.
- **R10** Fator medo: pisadas metálicas que aceleram, cabeça inclinando para escutar ao ouvir algo.

## Critérios de aceite
- [ ] Escondido parado num canto, a Matilha te acha pelo rastro em menos de 30 s.
- [ ] No telhado, os cães rodeiam a casa e não sobem.
- [ ] Em campo aberto, os cães cercam pelos lados em vez de vir em fila.
- [ ] PEM + 2 golpes de facão por cão é suficiente para sobreviver a um grupo de 3.

## Design técnico
- `MatilhaAI` segue o padrão da Sentinela (spec 03), com os mesmos sentidos e um `ScentTracker`.
- `MatilhaGroup` decide os papéis (`Segurar`, `Flanquear`) e as posições de flanco no NavMesh.
- `PlayerScent` no Caio: buffer circular de 20 posições.
