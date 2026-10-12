# 18 — Campanha, objetivos e checkpoints

**Status:** Rascunho · **Marco:** 11

## Objetivo
Encadear os 5 atos do GDD numa campanha de 45 a 60 minutos, cada ato ensinando uma mecânica antes de testá-la.

## Requisitos
- **R1** Os atos e suas mecânicas seguem o GDD:

| Ato | Zona | Objetivo | Mecânica ensinada |
| --- | --- | --- | --- |
| Prólogo — Escuro | Casa do Caio e rua, noite | Sair da vila sem ser visto | Movimento, agachar, ruído |
| 1 — A Roça | Mata e sítio do pai | Pegar o facão | Takedown, saque, crafting simples |
| 2 — O Almoxarifado | Galpão da estação | Montar o PEM | PEM, combo PEM + facão, bancada |
| 3 — Pelos Telhados | Rua principal | Chegar à estação | Escalada, rota vertical |
| 4 — A Estação | Interior da estação | Destruir o Núcleo | Tudo; chefe |

- **R2** Uma única cena da ilha; cada ato ativa e desativa grupos de inimigos e objetos (`ActDefinition`).
- **R3** Objetivos como dados (`ObjectiveData`): texto, condição (chegar a um gatilho, coletar item, destruir alvo), próximo objetivo.
- **R4** Checkpoints automáticos no início de cada ato e ao usar uma bancada.
- **R5** O checkpoint salva: ato, objetivo, posição, vida, inventário, munição, estado das antenas e inimigos destruídos.
- **R6** Ao recarregar um checkpoint, as redes voltam a `PlayerSpotted = false` (a perseguição recomeça limpa).
- **R7** Persistência em JSON em `Application.persistentDataPath` (um slot no MVP).

## Critérios de aceite
- [ ] Morrer no meio do ato 2 volta ao início do ato 2 com o inventário daquele momento.
- [ ] Fechar e abrir o jogo continua do último checkpoint.
