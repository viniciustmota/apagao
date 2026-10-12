# 06 — Ruído e audição

**Status:** Implementada · **Marco:** 4 · **Código:** `Systems/NoiseSystem.cs`, `AI/Senses/HearingSensor.cs`, `Player/PlayerFootsteps.cs`

## Objetivo
Todo som relevante vira um evento que as máquinas ouvem. Fazer barulho é a principal forma de se entregar e a principal forma de distrair.

## Requisitos
- **R1** `NoiseSystem.Emit(NoiseEvent)` distribui o evento para todos os ouvintes registrados.
- **R2** `NoiseEvent`: posição, raio (m), intensidade (0–1), tipo e emissor.
- **R3** Ouvintes (`INoiseListener`) se registram no `OnEnable` e saem no `OnDisable`.
- **R4** Cada parede entre a fonte e o ouvinte reduz o raio efetivo em 40% (máximo 2 paredes).
- **R5** Passos do Caio emitem ruído a cada passo, conforme o modo:

| Fonte | Raio | Intensidade |
| --- | --- | --- |
| Agachado | 1,5 m | 0,1 |
| Andando | 4 m | 0,3 |
| Correndo | 10 m | 0,6 |
| Queda de mais de 2 m | 6 m | 0,5 |
| Esquiva | 5 m | 0,4 |
| Takedown | 3 m | 0,3 |
| Facão acertando metal | 8 m | 0,5 |
| Vidro quebrando | 12 m | 0,7 |
| Bomba de ruído | 20 m | 0,8 |
| PEM | 12 m | 0,6 |
| Revólver | 40 m | 1,0 |
| Espingarda | 35 m | 1,0 |

- **R6** Reação da Sentinela ao ouvir:
  - Em Patrulha: suspeita += intensidade × 0,6 e `LastKnownPosition` = ponto do som → Suspeito.
  - Intensidade ≥ 0,8 → direto para Investigando.
  - Em Combate/Procurando: atualiza o ponto de busca.
- **R7** Matilha ouve com raio ×2 (spec 14).
- **R8** O som não revela a posição exata: o ponto investigado tem erro aleatório de até 15% da distância.
- **R9** Gizmo: esfera do raio por 1 s a cada evento (só no Editor).

## Critérios de aceite
- [ ] Correndo atrás de uma Sentinela a 8 m, ela vira e vai investigar.
- [ ] Agachado a 3 m pelas costas, ela não ouve.
- [ ] Bomba de ruído atrai a Sentinela para o ponto do impacto.
- [ ] Um tiro de revólver no meio da vila coloca todas as Sentinelas a até 40 m em Investigando.

## Design técnico
```csharp
public enum NoiseType { Passo, Queda, Impacto, Distracao, Arma, Explosao, Eletrico }
public readonly struct NoiseEvent { Vector3 Position; float Radius; float Intensity; NoiseType Type; GameObject Emitter; }
public static class NoiseSystem { Register, Unregister, Emit }   // lista estática limpa em SubsystemRegistration
public class HearingSensor : MonoBehaviour, INoiseListener { float hearingMultiplier; event Action<NoiseEvent> Heard; }
public class PlayerFootsteps : MonoBehaviour { lê PlayerController.Mode e a distância percorrida }
```
- Oclusão por `RaycastNonAlloc` entre fonte e ouvinte. **Hoje conta qualquer colisor** (exceto o emissor e o próprio ouvinte); passa a contar só a layer Obstacle quando as layers forem criadas.
- Ruído leve deixa a Sentinela em Suspeito por no mínimo 2,5 s, encarando o ponto (`minSuspiciousTime`).

## Questões abertas
- Robôs também emitem ruído que outros robôs ouvem? Proposta: não; a rede (spec 07) já cobre a comunicação entre eles.
