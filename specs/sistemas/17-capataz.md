# 17 — Capataz (chefe final)

**Status:** Implementada (falta testar) · **Marco:** 12

## Objetivo
A luta final, na sala do Núcleo da estação, usando tudo o que o jogador aprendeu: furtividade, PEM, facão e leitura da rede.

## Requisitos
- **R1** Arena: sala do Núcleo com 3 geradores, cobertura baixa e passarelas em altura.
- **R2** O Capataz fala pelos alto-falantes com a voz sintetizada do antigo prefeito (spec 19).
- **R3** **Fase 1 — Escudo:** o Capataz fica protegido no centro e chama reforços (2 Sentinelas por vez). O jogador precisa sabotar os 3 geradores com o facão (segurar E por 3 s) sem ser pego.
- **R4** **Fase 2 — Núcleo exposto:** varreduras de laser em padrões (pular ou agachar). Uma descarga de PEM derruba o escudo por 4 s: janela para golpes de facão no núcleo.
- **R5** **Fase 3 — Desespero:** a sala pega fogo, Vespas entram pelas janelas. O núcleo está aberto, mas o tempo é curto (90 s até a sala ficar inabitável).
- **R6** Vitória: cena final — o link da VIGÍLIA cai e o Caio usa o rádio (spec 19).
- **R7** Checkpoint no início de cada fase.

## Implementação (marco 12)
- Arena: `Sala_do_Nucleo` (34 × 9 × 26 m) dentro do muro da estação, com 3 geradores, cobertura baixa, passarelas a 3,6 m, bancada, rádio e recursos.
- O Capataz é o próprio Núcleo (`NucleoCore`: 600 de vida, escudo que bloqueia todo dano) orquestrado por `CapatazBoss`. Fala pelos alto-falantes como "Capataz" (legenda em vermelho).
- As fases são ligadas pelos objetivos da campanha (tags `capataz1/2/3`); cada uma abre com checkpoint e, ao carregar, o chefe volta na fase certa (geradores lembrados por `ISaveGone`; na fase 3 o Núcleo volta com 60%).
- **Fase 1:** `Generator` (segurar E por 3 s, só com o facão). A cada gerador cortado, 2 Sentinelas de reforço entram pela porta já sabendo onde o Caio está.
- **Fase 2:** dois lasers giram a 45°/s em volta do Núcleo; a cada 7 s alternam entre baixo (pular) e alto (agachar), com 1,4 s de aviso amarelo sem dano. PEM no Núcleo: escudo e lasers caem por 4 s. Termina com o Núcleo em 60%.
- **Fase 3:** escudo desligado de vez, fogo surge pela sala a cada 7 s, 3 Vespas decolam das passarelas, 90 s no relógio; depois disso a sala causa 15 de dano/s.
- **Vitória:** todas as redes caem, as máquinas da ilha são destruídas e os drones caem; o último objetivo é usar o rádio, e a Lia de verdade responde.
- HUD: barra do Núcleo (azul com escudo, laranja sem), estado da fase e relógio da fase 3.

## Critérios de aceite
- [ ] Cada fase pode ser vencida com os recursos encontrados na própria estação.
- [ ] A luta inteira dura entre 6 e 10 minutos para um jogador médio.
