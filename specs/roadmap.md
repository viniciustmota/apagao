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
- [ ] Cura com kit (depende do crafting, marco 8) — spec 08
- [ ] Afiar facão (depende do crafting, marco 8) — spec 09

## Marco 6 — Matilha
- [ ] Controlador da Matilha reaproveitando a FSM — spec 14
- [ ] Rastro do Caio e estado Rastreando — spec 14
- [ ] Coordenação de grupo: segurar e flanquear — spec 14

## Marco 7 — Armas e PEM
- [ ] `WeaponData` (ScriptableObject) + mira — spec 10
- [ ] Revólver e espingarda de pesca — spec 10
- [ ] PEM: cone, paralisia, cargas — spec 11
- [ ] `IStunnable` e estado Paralisado na Sentinela e na Matilha — spec 11
- [ ] Takedown frontal em robô paralisado — spec 09 + 11
- [ ] Esquiva do Caio — spec 01

## Marco 8 — Crafting e saque
- [ ] Recursos e inventário com limites — spec 12
- [ ] Coleta (`IInteractable`) e saque de máquinas — spec 12
- [ ] Menu radial de crafting e bancada — spec 12
- [ ] Arremessáveis: bomba de ruído, molotov, isca elétrica — spec 12

## Marco 9 — Telhados
- [ ] Detecção de borda e ledge climb — spec 13
- [ ] Ruído de queda e regras de altura — spec 13
- [ ] NavMesh: Matilha não sobe em telhado — spec 13 + 14

## Marco 10 — Drones
- [ ] Olho: patrulha aérea, térmica, holofote — spec 15
- [ ] Vespa: mergulho e explosão — spec 15
- [ ] Brasa: fogo que queima cobertura — spec 15

## Marco 11 — Ilha e campanha
- [ ] Greybox da ilha inteira com as zonas — spec 22
- [ ] Brutamontes — spec 16
- [ ] Campanha: atos, objetivos e checkpoints — spec 18
- [ ] Rádio da Lia e falas do Capataz — spec 19
- [ ] HUD completo — spec 20

## Marco 12 — Chefe e polimento
- [ ] Capataz em 3 fases — spec 17
- [ ] Áudio e música dinâmica — spec 21
- [ ] Arte final dos robôs e da vila
- [ ] Build para Linux e Windows e teste com outras pessoas
