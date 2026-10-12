# 18 — Campanha, objetivos e checkpoints

**Status:** Implementada até a chegada à estação (falta testar); o ato 4 completo depende do Capataz (marco 12) · **Marco:** 11

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

## Implementação atual (marco 11)
- `Campaign` guarda os objetivos como dados serializados (`Objective`: texto, tipo Chegar/Usar/SabotarAntena/Destruir, alvo, checkpoint, sinal do rádio, falas de início e de fim). Os atos (`ActDefinition`) entram com a ilha completa.
- `CheckpointManager` salva em `apagao_save.json` (R7): objetivo, posição, vida, inventário, munição, cargas do PEM, durabilidade do facão e a lista de objetos que sumiram.
- `SaveId` (gravado pelo builder: caminho + posição) identifica inimigos, drones, itens e antenas. Quem some chama `SaveId.ReportGone`; ao carregar, o manager desativa esses objetos antes do Awake deles e sabota de novo as antenas, em silêncio.
- Morrer recarrega a cena, que volta ao último checkpoint; a rede recomeça sem `PlayerSpotted` (R6). Fabricar perto da bancada salva (R4). **F9** apaga o save e recomeça.
- Demo na vila de teste: chegar à bancada → derrubar a antena → usar o rádio da estação atrás da igreja → fim da demo.
- Itens coletados pela metade e saques de máquinas não são lembrados (aceito no MVP).

## Atos na Ilha do Amparo
- `ActManager` com `ActDefinition` (título, grupos `Ato_N`, luz). Um grupo fica ligado no seu ato e nos seguintes; atos futuros começam desligados.
- `Objective.act` diz a que ato o objetivo pertence; ao começar um objetivo de outro ato, a cartela "Ato N — Nome" aparece e a luz muda.
- O Caio começa de mãos vazias (`PlayerLoadout`): facão e revólver no sítio, espingarda no barco do manguezal, peças do PEM no almoxarifado (`WeaponPickup`). O checkpoint guarda o que ele já tem.
- Saves separados por cena (`apagao_save_<cena>.json`).
- Sequência: sair da vila → sítio → facão → antena do sítio → almoxarifado → montar o PEM → antena do almoxarifado → rua pelos telhados → entrar na estação → "Fim do conteúdo atual".

## Critérios de aceite
- [ ] Morrer no meio do ato 2 volta ao início do ato 2 com o inventário daquele momento.
- [ ] Fechar e abrir o jogo continua do último checkpoint.
