# 04 — Percepção: visão

**Status:** Implementada · **Marco:** 3 · **Código:** `Scripts/AI/Senses/VisionSensor.cs`

## Objetivo
Robôs enxergam o Caio de forma legível e justa: o jogador entende por que foi visto.

## Requisitos
- **R1** Cone de visão com alcance e ângulo configuráveis (padrão Sentinela: 16 m, 110°).
- **R2** Teste de ângulo com `Vector3.Dot` contra o cosseno do meio-ângulo.
- **R3** Oclusão por raycast do olho até o peito do alvo; o primeiro objeto atingido precisa ser o alvo.
- **R4** Retornar `visibility` de 0 a 1: perto e no centro do cone = 1; longe (25%) e na borda (40%) = menos.
- **R5** O peito do alvo é o centro do `CharacterController`: agachado, o Caio some atrás de muros de ~1 m.
- **R6** `RangeMultiplier` para alerta elevado e outros modificadores.
- **R7** *(Marco 4)* Modificadores do alvo, aplicados pelo dono do sensor: agachado ×0,5, correndo ×1,3, no mato ×0,4, na sombra/noite ×0,6.
- **R8** *(Marco 10)* Modo térmico (Olho): ignora a layer Vegetation; Water e telhados bloqueiam.
- **R9** Gizmo do cone quando o robô está selecionado.

## Critérios de aceite
- [ ] Atrás de uma casa, o Caio não é visto mesmo dentro do cone.
- [ ] Agachado atrás de um caixote de 1 m, não é visto; em pé, é.
- [ ] Fora do ângulo (atrás do robô), nunca é visto, mesmo colado.

## Design técnico
- `CanSee(Transform target, out float visibility)`, chamado uma vez por frame pelo dono.
- *(Otimização futura)* rodar a visão a 10 Hz, com fase aleatória por robô.

## Questões abertas
- A visão deve considerar a luz real da cena? Proposta: não; usar volumes "Sombra" marcados à mão.
