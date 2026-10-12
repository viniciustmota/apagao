# 08 — Vida e dano

**Status:** Aprovada · **Marco:** 5

## Objetivo
Um modelo único de dano para o Caio, os robôs e os objetos destrutíveis.

## Requisitos
- **R1** `IDamageable.TakeDamage(in DamageInfo)` e `IsDead`.
- **R2** `DamageInfo`: quantidade, tipo, ponto, direção, origem.
- **R3** Tipos: `Corte`, `Bala`, `Fogo`, `Explosao`, `Eletrico`.
- **R4** Componente `Health` com vida máxima, eventos `Damaged` e `Died`, e multiplicadores por tipo.
- **R5** **Caio:** 100 de vida, sem regeneração. Cura só com kit (+50, 2 s de uso, interrompido por dano).
- **R6** Morte do Caio → tela escura de 2 s → recomeça no último checkpoint (spec 18).
- **R7** Feedback de dano no Caio: vinheta vermelha, câmera treme, som abafado com vida < 30.
- **R8** Robôs: ponto fraco por trás (×3) definido pelo ângulo entre a direção do golpe e o `forward` do robô (> 120°).
- **R9** Robô destruído: desliga a FSM, faz o sinal de morte para a rede (spec 07), vira sucata com saque (spec 12).

| Alvo | Vida | Bala | Corte | Fogo | Explosão | Elétrico |
| --- | --- | --- | --- | --- | --- | --- |
| Caio | 100 | ×1 | ×1 | ×1 | ×1 | ×1 |
| Sentinela | 100 | ×1 | ×0,6 | ×0,5 | ×1,5 | paralisa |
| Matilha (cão) | 60 | ×0,8 | ×1 (juntas ×2) | ×1,5 | ×1,5 | paralisa |
| Olho | 20 | ×1 | — | ×1 | ×2 | destrói |
| Vespa | 10 | ×1 | — | ×1 | ×2 | destrói |
| Brasa | 60 | ×1 | — | imune | ×1,5 | derruba |
| Brutamontes | 400 | ×0,25 frente | ×0,5 | ×3 juntas | ×1 | paralisa 1,5 s |

## Critérios de aceite
- [ ] Três rajadas da Sentinela matam o Caio com vida cheia (≈ 7 balas acertadas).
- [ ] Morrer recomeça no checkpoint com o inventário do checkpoint.
- [ ] Atirar nas costas da Sentinela causa visivelmente mais dano.

## Design técnico
```csharp
public enum DamageType { Corte, Bala, Fogo, Explosao, Eletrico }
public readonly struct DamageInfo { float Amount; DamageType Type; Vector3 Point; Vector3 Direction; GameObject Source; }
public class Health : MonoBehaviour, IDamageable { float max; DamageMultipliers multipliers; event Action<DamageInfo> Damaged; event Action Died; }
```
- `DamageMultipliers` é um ScriptableObject por tipo de inimigo.
- Dano elétrico em robôs chama `IStunnable.Stun` em vez de tirar vida.
