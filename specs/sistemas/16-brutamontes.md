# 16 — Brutamontes

**Status:** Rascunho · **Marco:** 11

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

## Questões abertas
- Ele pode quebrar paredes de madeira na investida (abrindo novas rotas)?
