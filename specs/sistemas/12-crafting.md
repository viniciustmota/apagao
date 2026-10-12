# 12 — Recursos, inventário e crafting

**Status:** Implementada (menu em lista) · **Marco:** 8 · **Código:** `Crafting/`, `Combat/Throwables.cs`, `Player/PlayerInteractor.cs`, `Player/PlayerHealing.cs`

## Objetivo
Gambiarra como sistema: tudo útil vem de sucata. A escassez obriga a enfrentar máquinas para conseguir componentes.

## Requisitos

### Recursos
- **R1** Cinco recursos: `Sucata`, `Componente`, `Pano`, `Alcool`, `Polvora`. Limite de 10 de cada (Componente: 5).
- **R2** Coleta com E em itens do mundo (`IInteractable`), 0,5 s, ruído baixo.
- **R3** Saque de máquinas destruídas: Sentinela 2 sucatas + 50% de 1 componente; cão 1 sucata + 30% de 1 componente; drones 1 componente garantido.

### Receitas

| Item | Receita | Onde | Limite |
| --- | --- | --- | --- |
| Bomba de ruído | 1 sucata + 1 pólvora | Qualquer lugar | 3 |
| Molotov | 1 pano + 1 álcool | Qualquer lugar | 3 |
| Kit de primeiros socorros | 2 panos + 1 álcool | Qualquer lugar | 2 |
| Carga de PEM | 2 componentes + 1 sucata | Bancada | 2 |
| Isca elétrica | 1 componente + 2 sucatas | Bancada | 2 |
| Munição de revólver (3) | 1 sucata + 1 pólvora | Bancada | — |
| Afiar facão | 1 sucata | Qualquer lugar | — |

- **R4** Crafting simples pelo menu radial (segurar Tab): o tempo continua correndo, 1,5 s por item. *Implementado como lista: segurar Tab e apertar o número da receita. O radial com mouse fica para o polimento.*
- **R5** Bancada (`IInteractable`) abre o menu completo; o tempo também continua.
- **R6** Arremessáveis (T escolhe; segurar G mostra o arco; soltar arremessa):
  - Bomba de ruído: ruído de 20 m no impacto (spec 06).
  - Molotov: área de fogo de 4 m por 8 s, 10 de dano/s, dano ×3 nas juntas.
  - Isca elétrica: sinal que atrai cães a até 25 m e os prende 5 s.

## Critérios de aceite
- [ ] Destruir uma Sentinela e saquear dá recursos visíveis no HUD.
- [ ] Sem os recursos, a receita aparece cinza no menu.
- [ ] Bomba de ruído tira uma Sentinela da porta que o Caio precisa atravessar.

## Design técnico
```csharp
public enum ResourceType { Sucata, Componente, Pano, Alcool, Polvora }
public class Inventory : MonoBehaviour { int Get(ResourceType); bool TrySpend(cost); event Action Changed; }
[CreateAssetMenu] public class Recipe : ScriptableObject { ResourceCost[] cost; ItemData result; bool needsWorkbench; float craftTime; }
public class Pickup : MonoBehaviour, IInteractable { }
public class Workbench : MonoBehaviour, IInteractable { }
```
- **Como ficou:** `Inventory` (recursos + itens `BombaRuido`, `Molotov`, `Isca`, `Kit`), `CraftingSystem` com `RecipeBook` em código (vai para ScriptableObject quando o balanceamento estabilizar), `Pickup` registrado em `InteractableRegistry` (sem colisor), `Workbench` por proximidade (2,5 m).
- Kit usado com **H** (`PlayerHealing`). Munição fabricada vai para a reserva do revólver; carga de PEM vai direto para o PEM.
- Molotov: área de 4 m, 8 s, 10 de dano/s, **queima o Caio também**. A origem do dano é o fogo, então o robô queimado não descobre onde o Caio está.
- Isca: pulsa um som de 12,5 m (os cães ouvem a 25 m) por 6 s e paralisa 5 s os cães que chegam a 2 m (`ILureable`).
- Começa com 2 sucatas, 1 componente, 1 pano, 1 álcool, 1 pólvora e 1 bomba de ruído (para testar).
