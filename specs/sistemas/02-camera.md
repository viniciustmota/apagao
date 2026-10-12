# 02 — Câmera

**Status:** Implementada · **Marco:** 1 · **Código:** `Scripts/Player/ThirdPersonCamera.cs`

## Objetivo
Câmera por cima do ombro que favorece a leitura do ambiente para a furtividade.

## Requisitos
- **R1** Órbita com o mouse: yaw livre, pitch entre −35° e 70°.
- **R2** Câmera a 3,5 m atrás do pivô, deslocada 0,6 m para o ombro direito.
- **R3** Pivô a 1,6 m em pé e 1,0 m agachado, com transição suave.
- **R4** Se houver obstáculo atrás, a câmera se aproxima (SphereCast, ignora o próprio Caio).
- **R5** Cursor preso durante o jogo; Esc solta, clique esquerdo prende.
- **R6** *(Marco 7)* Ao mirar (botão direito), distância cai para 1,8 m e o FOV para 50°.
- **R7** *(Marco 7)* Trocar de ombro com Q.

## Critérios de aceite
- [ ] Encostado numa parede, a câmera não atravessa a parede.
- [ ] Agachar abaixa a câmera sem tranco.
- [ ] Esc libera o mouse para usar o Editor.

## Design técnico
- `LateUpdate`, depois do movimento. Leitura direta de `Mouse.current.delta`.
- Sensibilidade 0,12°/pixel.

## Questões abertas
- Migrar para Cinemachine quando houver mira e cutscenes? Proposta: decidir no marco 7.
