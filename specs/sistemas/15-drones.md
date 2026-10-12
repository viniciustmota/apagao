# 15 — Drones: Olho, Vespa e Brasa

**Status:** Rascunho · **Marco:** 10

## Objetivo
Os drones tiram a segurança vertical: o mato e o telhado deixam de ser esconderijos garantidos.

## Requisitos comuns
- **R1** Voo próprio, sem NavMesh: seguem rotas de waypoints em altitude fixa, desviando de obstáculos por raycast.
- **R2** Todos são membros da rede (spec 07).
- **R3** PEM destrói Olho e Vespa e derruba a Brasa (spec 11).

## Olho (drone de busca)
- **R4** Patrulha a 12 m de altura, 4 m/s. Visão térmica para baixo: cone de 60°, alcance 20 m.
- **R5** A térmica ignora o mato; é bloqueada por telhados, interiores e água (Caio submerso no raso fica invisível).
- **R6** Não ataca. Ao ver o Caio: segue a 10 m de altura, reporta a posição à rede a cada 1 s.
- **R7** À noite, holofote (`Spotlight`) varre o chão; o cone de luz é o cone de visão.
- **R8** Vida 20: um tiro de revólver derruba.

## Vespa (drone kamikaze)
- **R9** Fica pousada em ninhos; a rede a lança quando `PlayerSpotted` e o Caio está a até 40 m.
- **R10** Aproxima a 10 m/s; a 8 m, trava no alvo com zumbido crescente por 1 s, depois mergulha em linha reta a 18 m/s no ponto travado.
- **R11** Explosão: raio de 3 m, 60 de dano no centro com queda linear. A esquiva (spec 01) no último segundo evita o dano.
- **R12** Vida 10; a espingarda derruba grupos.

## Brasa (drone incendiário)
- **R13** Sobrevoa a área a 8 m e despeja fogo num círculo de 6 m na última posição conhecida.
- **R14** O fogo dura 15 s, causa 10 de dano/s e **queima a cobertura**: mato e madeira viram cinza (deixam de esconder).
- **R15** Imune a fogo. Vida 60. Ao cair pelo PEM, fica 6 s no chão vulnerável ao facão.

## Critérios de aceite
- [ ] Escondido no mato, o Olho te encontra; na água ou sob um telhado, não.
- [ ] Uma Vespa sempre pode ser evitada com esquiva bem cronometrada.
- [ ] Depois da Brasa, a área de mato queimado não esconde mais.

## Design técnico
- `DroneMotor` compartilhado (voo, altitude, desvio); `OlhoAI`, `VespaAI`, `BrasaAI` com FSMs pequenas.
- Mato queimável: `Vegetation` com componente `Flammable` que troca o visual e remove a layer.

## Questões abertas
- O Olho deve ter bateria e pousar para recarregar (janela de oportunidade)?
