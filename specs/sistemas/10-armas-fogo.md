# 10 — Armas de fogo

**Status:** Implementada · **Marco:** 7 · **Código:** `Combat/WeaponData.cs`, `Combat/WeaponHolder.cs`, `Data/Armas/`

## Objetivo
Armas fortes e barulhentas, com munição tão rara que cada tiro é uma decisão.

## Requisitos
- **R1** Botão direito: mirar (câmera aproxima, mira aparece). Esquerdo mirando: atirar.
- **R2** Disparo por raycast instantâneo, com dispersão que aumenta andando (×1,5) e correndo (×3).
- **R3** Cada disparo emite ruído (spec 06).
- **R4** Recarga com R, interrompida por correr ou esquivar.
- **R5** Troca de arma pela roda numérica: 1 facão, 2 revólver, 3 espingarda, 4 PEM, 5 arremessável.

| Arma | Dano | Tiros | Recarga | Alcance | Ruído | Munição máxima | Observação |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Revólver | 50 bala | 6 | 2,5 s | 60 m | 40 m | 12 | Precisa |
| Espingarda de pesca adaptada | 8 × 12 bala | 2 | 3 s | 15 m | 35 m | 6 | Empurra drones; derruba Vespas em grupo |

## Critérios de aceite
- [ ] Um tiro de revólver derruba um Olho.
- [ ] Atirar numa Sentinela na vila atrai todas as próximas (ruído + rede).
- [ ] Com 0 de munição, a arma faz clique e não dispara.

## Design técnico
```csharp
[CreateAssetMenu] public class WeaponData : ScriptableObject { damage, pellets, magazine, reloadTime, range, spread, noiseRadius, maxReserve }
public abstract class BaseWeapon : MonoBehaviour { WeaponData data; abstract void Fire(Ray aim); }
public class HitscanWeapon : BaseWeapon { }  // revólver e espingarda
```
- `WeaponHolder` no Caio controla equipada, munição e recarga. A mira usa o centro da tela: um raio da câmera acha o ponto mirado e o disparo sai do cano em direção a ele (ignorando o próprio Caio).
- Só dispara mirando (segurar o botão direito); sem mira o clique não faz nada, para evitar tiros acidentais que acordam a ilha.
- Munição inicial: revólver 6 + 6, espingarda 2 + 4.
- Rastro de bala: `LineRenderer` por 0,05 s.
