# 14 — Matilha (cães robô)

**Status:** Implementada (rastro na água pendente) · **Marco:** 6 · **Código:** `AI/Matilha/`, `Player/PlayerScent.cs`

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
- `MatilhaAI : MachineAI` (mesma base da Sentinela) com estados `PatrulhaCaoState`, `SuspeitoCaoState`, `RastreandoState`, `CombateCaoState`, `ProcurandoCaoState`.
- `MatilhaGroup` no objeto pai: o primeiro cão vivo lidera a patrulha e **segura**; os outros andam em formação e **flanqueiam** a ±110° da direção do líder. Um cão em combate ou rastreando puxa o grupo inteiro (`Rally`).
- `PlayerScent` no Caio: buffer circular, um ponto por segundo, 20 s de vida; agachado não deixa rastro. O cão segue sempre o ponto mais novo num raio de 6 m; rastro com menos de 2,5 s e o Caio a menos de 5 m = achou (combate, mesmo sem ver).
- Sons fracos (< 0,25) deixam o cão em Suspeito com a cabeça inclinada; sons mais fortes levam direto a Rastreando.
- Visão 10 m / 140°, audição ×2, vida 60, juntas (golpe por trás) ×2.
