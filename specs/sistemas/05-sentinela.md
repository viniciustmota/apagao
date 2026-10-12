# 05 — Sentinela

**Status:** Implementada (sem saque) · **Marco:** 2–5 · **Código:** `Scripts/AI/Sentinela/`

## Objetivo
O inimigo base: humanoide que patrulha, desconfia, investiga, combate e procura. Silencioso; só se ouve o servo motor.

## Requisitos
- **R1** Patrulha em loop por waypoints, pausa de 1,5 s em cada um, a 1,6 m/s.
- **R2** Ver o Caio → Suspeito: para, encara o vulto, medidor de suspeita sobe conforme a visibilidade (cheio em 1,5 s com visibilidade 1).
- **R3** Sem ver, o medidor desce 0,25/s. Vazio → volta à Patrulha.
- **R4** Medidor cheio → Investigando: vai até a última posição vista a 2,2 m/s e olha em volta por 4 s.
- **R5** Ver o Caio a menos de 3 m, em qualquer estado → Combate imediato.
- **R6** Combate: persegue a 3,5 m/s até 8 m, para e mira. Perde de vista por 2 s → Procurando.
- **R7** Procurando: vasculha pontos aleatórios num raio de 8 m por 12 s. Desiste → Patrulha.
- **R8** Depois de entrar em Combate, fica em **alerta elevado**: visão ×1,25 e suspeita ×1,5 até o fim do ato.
- **R9** *(Marco 5)* Tiro: rajada de 3 balas a cada 2 s, 15 de dano cada; precisão cai com a distância e se o Caio está correndo.
- **R10** *(Marco 4)* Reage a ruído (spec 06) e à rede (spec 07).
- **R11** *(Marco 7)* Estado Paralisado (spec 11).
- **R12** Vida 100. Ponto fraco: bateria nas costas, dano ×3 por trás.
- **R13** Saque ao ser destruída: 2 sucatas e 50% de chance de 1 componente (spec 12).

## Critérios de aceite
- [ ] Indicador: escondido na patrulha, amarelo crescente em suspeita, laranja investigando, vermelho em combate.
- [ ] Agachado e longe, o jogador consegue sair do campo de visão antes do medidor encher.
- [ ] Depois de um combate, a Sentinela te nota visivelmente mais rápido.
- [ ] Duas Sentinelas não travam uma na outra no NavMesh.

## Design técnico
- `SentinelaAI` (dados, sentidos, ações) + estados `PatrulhaState`, `SuspeitoState`, `InvestigandoState`, `CombateState`, `ProcurandoState`.
- `NavMeshAgent` começa desligado e é ligado no `Start`, depois do `NavMeshBaker`.
- Feedback: esfera de alerta (MaterialPropertyBlock), `LineRenderer` do laser, label `Handles` no Scene view.
- Colisor `CapsuleCollider` só na raiz; partes visuais sem colisor.

## Questões abertas
- ~~O laser deve avisar antes do tiro?~~ Decidido: o laser engrossa durante os 0,5 s de mira antes de cada rajada.
- Levar dano coloca a Sentinela em Combate na hora, com a posição de quem atacou.
