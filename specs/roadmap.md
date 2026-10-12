# Roadmap e tarefas

Cada marco termina com algo jogável. Uma tarefa só é marcada quando foi testada no Play.

Legenda: `[x]` validada · `[~]` implementada, falta testar · `[ ]` a fazer

## Marco 1 — Base ✅
- [~] Controle do Caio: andar, correr, agachar, pular — spec 01
- [~] Câmera em terceira pessoa — spec 02
- [~] Builder da ilha de teste (terreno, mar, vila, caixotes, igreja)
- [x] Repositório Git + GitHub

## Marco 2 — Sentinela patrulhando
- [~] `State` e `StateMachine` genéricos — spec 03
- [~] NavMesh em runtime (`NavMeshBaker`)
- [~] Patrulha por waypoints — spec 05

## Marco 3 — Visão e FSM completa
- [~] `VisionSensor`: cone, alcance, oclusão, visibilidade — spec 04
- [~] Estados Suspeito, Investigando, Combate, Procurando — spec 05
- [~] Indicador de alerta, laser e label de debug
- [ ] Ajustar tempos de detecção depois do teste

## Marco 4 — Ruído e rede da VIGÍLIA (implementado, falta testar)
- [~] `NoiseSystem` + `NoiseEvent` + `INoiseListener` — spec 06
- [~] `HearingSensor` na Sentinela e reação ao som — spec 06
- [~] Passos e quedas do Caio emitem ruído por modo de movimento — spec 06
- [~] `HiveNetwork` + `HiveMember`: visão compartilhada e convergência — spec 07
- [~] Check-in periódico e investigação de membro desaparecido — spec 07
- [~] `HiveAntenna` sabotável (segurar E) — spec 07
- [~] Limpeza de estáticos para Fast Enter Play Mode
- [~] `DebugHUD` provisório: estado da rede, relógio de check-in e prompt da antena
- [ ] Oclusão de som só pela layer Obstacle (hoje conta qualquer colisor) — spec 06

## Marco 5 — Vida, facão e takedown (implementado, falta testar)
- [~] `Health`, `DamageInfo`, `IDamageable`, multiplicadores e ponto fraco nas costas — spec 08
- [~] Morte do Caio e reinício (recarrega a cena até existir checkpoint) — spec 08
- [~] Sentinela atira: mira de 0,5 s (laser engrossa) + rajada de 3 — spec 05 + 08
- [~] Sentinela atingida entra em combate; destruída vira carcaça no chão — spec 05
- [~] Facão: ataque leve e pesado, durabilidade — spec 09
- [~] Ajuste pós-teste do facão: golpe na direção da câmera, assistência de mira, avanço e janela de acerto — spec 09 R2
- [~] Retorno do acerto: hitstop, brilho, recuo, tremor de câmera e marcador de alvo — spec 09 R5a
- [~] Takedown furtivo por trás (E) — spec 09
- [~] HUD mínimo: vida, facão, prompt de takedown, vinheta de dano, tela de morte — spec 20
- [~] Cura com kit (H, 2 s, interrompida por dano) — spec 08
- [~] Afiar facão (receita no crafting) — spec 09

## Marco 6 — Matilha (implementado, falta testar)
- [~] Base `MachineAI` extraída da Sentinela (sentidos, rede, vida, takedown, carcaça, indicador) — arquitetura
- [~] Controlador da Matilha reaproveitando a FSM — spec 14
- [~] Rastro do Caio (`PlayerScent`) e estado Rastreando — spec 14
- [~] Coordenação de grupo (`MatilhaGroup`): formação, alerta conjunto, segurar e flanquear — spec 14
- [~] Bote com aviso de 0,4 s e mordida de 20 — spec 14
- [ ] Água apaga o rastro (depende da água caminhável, spec 22) — spec 14

## Marco 7 — Armas e PEM (implementado, falta testar)
- [~] `WeaponData` (ScriptableObject em `Data/Armas`) + mira com botão direito e câmera aproximada — spec 10
- [~] Revólver e espingarda de pesca: hitscan pelo centro da tela, dispersão, recarga, ruído — spec 10
- [~] PEM: cone de 30°, 10 m, paralisia de 3 s, 2 cargas — spec 11
- [~] `IStunnable` na base `MachineAI` (paralisia, offline na rede, imunidade de 5 s) e na antena — spec 11
- [~] Takedown frontal em robô paralisado — spec 09 + 11
- [~] Esquiva do Caio (Alt) com invulnerabilidade a explosões — spec 01
- [~] HUD: arma em mãos, munição, cargas do PEM, mira e avisos — spec 20

## Marco 8 — Crafting e saque (implementado, falta testar)
- [~] Recursos e inventário com limites — spec 12
- [~] Interação genérica (`IInteractable` + `PlayerInteractor`) e coleta segurando E — spec 12
- [~] Saque de máquinas destruídas (`LootEntry` na `MachineAI`) — spec 12
- [~] Menu de crafting (segurar Tab + número) e bancada — spec 12
- [~] Arremessáveis: bomba de ruído, molotov (fogo 8 s) e isca elétrica (prende cães) — spec 12
- [~] Recursos e bancada espalhados pela ilha de teste (`ItensBuilder`)
- [ ] Menu radial com o mouse (hoje é lista com números) — spec 12
- [ ] Receitas em ScriptableObject (hoje em `RecipeBook`) — spec 12

## Marco 9 — Telhados (implementado, falta testar)
- [~] Detecção de borda e ledge climb (`LedgeClimber`, Espaço de frente para a borda) — spec 13
- [~] Agachado não cai da beira — spec 13 R6
- [~] Dano de queda acima de 6 m (10 por metro) — spec 13 R5
- [~] Galpões e muretas na vila de teste: rotas pelo alto e cobertura
- [~] Matilha não sobe em telhado: telhados não se ligam ao chão no NavMesh — spec 13 + 14

## Marco 10 — Drones (implementado, falta testar)
- [~] `DroneMotor` (voo sem NavMesh, desvio subindo) e base `DroneAI` (rede, PEM, queda, saque) — spec 15
- [~] Olho: patrulha a 12 m, térmica para baixo (telhado e água escondem), holofote, segue e reporta — spec 15
- [~] Vespa: pousada na igreja, decola com o Caio marcado, trava 1 s, mergulha e explode (esquiva evita) — spec 15
- [~] Brasa: despeja fogo de 6 m por 15 s na última posição, imune a fogo, PEM derruba por 6 s — spec 15
- [~] Câmera olha mais para cima (−60°) para mirar em drones — spec 02
- [ ] Fogo queima mato e cobertura (depende do mato, spec 22) — spec 15

## Marco 11 — Ilha e campanha
- [ ] Greybox da ilha inteira com as zonas — spec 22
- [~] Brutamontes: soco, investida, parede atordoa, blindagem frontal, PEM curto, takedown fraco, tremor dos passos — spec 16
- [~] Quintal de teste atrás da fileira sul com muro para a investida
- [ ] Campanha: atos, objetivos e checkpoints — spec 18
- [ ] Rádio da Lia e falas do Capataz — spec 19
- [ ] HUD completo — spec 20

## Marco 12 — Chefe e polimento
- [ ] Capataz em 3 fases — spec 17
- [ ] Áudio e música dinâmica — spec 21
- [ ] Arte final dos robôs e da vila
- [ ] Build para Linux e Windows e teste com outras pessoas
