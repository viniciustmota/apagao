# 09 — Facão e takedowns

**Status:** Implementada (sem afiar) · **Marco:** 5 (takedown frontal no 7) · **Código:** `Combat/Facao.cs`, `Combat/TakedownSystem.cs`

## Objetivo
O facão é a ferramenta mais confiável do Caio: silencioso, mas curto e frágil. O takedown é a recompensa da furtividade.

## Requisitos

### Ataque
- **R1** Botão esquerdo: ataque leve (25 de corte, 0,5 s). Segurar e soltar: pesado (50 de corte, 1,1 s).
- **R2** O golpe vai para onde a **câmera** aponta, não para onde o Caio andou por último.
- **R2a** Assistência de mira: se houver um alvo a até 2,8 m e a até 60° da direção da câmera, o Caio vira para ele e o acompanha durante a preparação.
- **R2b** Avanço (lunge) durante a preparação: 0,4 m no leve e 0,8 m no pesado, parando a 0,9 m do alvo.
- **R2c** Hitbox ativa por uma janela (leve 0,10 s, pesado 0,15 s), conferida a cada frame: `OverlapBox` de 1,4 m de largura e até 1,9 m à frente. Cada alvo leva dano uma vez por golpe.
- **R2d** Enquanto carrega o pesado, o Caio fica virado para a câmera (dá para mirar com o mouse).
- **R2e** Preparação: leve 0,08 s, pesado 0,25 s. *(Ajustado após teste: o leve não acertava e o pesado às vezes não pegava.)*
- **R3** Durabilidade 100: leve −5, pesado −10, takedown −15. Afiar com 1 sucata restaura 50 (2 s, fora de combate).
- **R4** Facão com 0 de durabilidade: dano pela metade e sem takedown.
- **R5** Acertar metal faz ruído (8 m) e faísca.
- **R5a** Retorno do acerto: hitstop (leve 0,05 s, pesado 0,09 s), robô pisca branco, recua de 0,15 a 0,5 m, câmera treme. Um losango marca o alvo que a assistência de mira vai acertar. *(Ajuste pós-teste: o golpe acertava, mas não parecia acertar.)*

### Takedown
- **R6** Disponível quando o alvo:
  - está a até 1,8 m;
  - não está em Combate;
  - e o Caio está atrás dele (ângulo > 120° em relação ao `forward` do alvo), **ou** o alvo está paralisado (spec 11).
- **R7** Prompt "E — Finalizar" aparece sobre o alvo quando disponível.
- **R8** Execução: 1,2 s, Caio e alvo travados, câmera aproxima. Mata o alvo. Ruído de 3 m.
- **R9** Cada inimigo tem sua finalização apontando o ponto fraco (Sentinela: bateria nas costas; cão: junta do pescoço).
- **R10** O Brutamontes só aceita takedown com vida < 30% e paralisado.
- **R11** Drones não aceitam takedown.

## Critérios de aceite
- [ ] Chegar agachado por trás da Sentinela mostra o prompt e o takedown a destrói sem alertar a outra.
- [ ] De frente, sem PEM, o prompt nunca aparece.
- [ ] Atacar uma Sentinela em combate com o facão é possível, mas perde-se a troca (ela vence).

## Design técnico
- `Facao : MonoBehaviour` no Caio; `TakedownSystem` procura alvos `ITakedownTarget` no raio e escolhe o mais centralizado na câmera.
- `ITakedownTarget { bool CanBeTakenDown(Transform attacker); void OnTakedown(); Transform AlignPoint; }`.
- Sem animações no MVP: o Caio "dá um tranco" para a frente e o robô cai (física ligada na sucata).

## Questões abertas
- Takedown deve ser arriscado se houver outro robô olhando? Proposta: sim — se outro membro vê o takedown, é Combate imediato.
