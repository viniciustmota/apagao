# 20 — Interface (HUD)

**Status:** Rascunho · **Marco:** 5 a 11

## Objetivo
HUD mínimo: o jogador lê o mundo, não a tela.

## Requisitos
- **R1** *(M5)* Vida: barra discreta no canto, visível só ao tomar dano ou com vida < 50.
- **R2** *(M5)* Prompt de interação ("E — Finalizar", "E — Coletar") preso ao objeto.
- **R3** *(M3, existente)* Indicador de alerta sobre cada inimigo.
- **R4** *(M4)* Indicador de ameaça fora da tela: seta na borda apontando para quem está te vendo.
- **R5** *(M4)* Relógio de check-in, só quando um membro da rede está desaparecido (spec 07).
- **R6** *(M7)* Mira e munição só com arma em mãos; cargas de PEM.
- **R7** *(M8)* Menu radial de crafting; contador de recursos ao coletar.
- **R8** *(M11)* Objetivo atual ao trocar de objetivo (3 s) e ao segurar Tab.
- **R9** *(M11)* Legendas (spec 19), menu de pausa (continuar, recomeçar checkpoint, opções, sair).
- **R10** Opções: sensibilidade do mouse, volume, inverter eixo Y.

## Design técnico
- UI Toolkit para menus; `Canvas` world-space para prompts e indicadores.
- HUD só escuta eventos (`Health.Damaged`, `Inventory.Changed`, `HiveNetwork.MemberMissing`); nunca consulta por frame.
