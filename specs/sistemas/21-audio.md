# 21 — Áudio

**Status:** Rascunho · **Marco:** 12

## Objetivo
O som é jogabilidade: cada máquina é reconhecível de longe, e o silêncio é um aviso.

## Requisitos
- **R1** Mixer com grupos: Ambiente, Efeitos, Inimigos, Vozes, Música, Rádio.
- **R2** Assinaturas sonoras: Sentinela (servo motor baixo), Matilha (pisadas metálicas que aceleram), Olho (hélices + scanner), Vespa (zumbido agudo crescente), Brasa (chama + turbina), Brutamontes (passos com tremor).
- **R3** Ambiente: mar, vento nos coqueiros, cigarras. As cigarras silenciam num raio de 15 m em volta de qualquer máquina.
- **R4** Música dinâmica pelo nível global de alerta (maior estado entre os inimigos próximos): Exploração (silêncio/ambiente) → Tensão (Suspeito/Investigando) → Combate.
- **R5** Rádio com filtro passa-banda e ruído; degradação por ato (spec 19).
- **R6** Áudio 3D com atenuação realista; oclusão simples (abafar atrás de paredes).

## Critérios de aceite
- [ ] De olhos fechados, dá para saber qual máquina está perto.
- [ ] O silêncio das cigarras avisa a chegada de uma patrulha antes de vê-la.
