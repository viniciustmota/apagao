# 11 — PEM (bloqueador de drones adaptado)

**Status:** Implementada (efeito em drones no marco 10) · **Marco:** 7 · **Código:** `Combat/Pem.cs`, `AI/Core/MachineAI.cs`

## Objetivo
A arma assinatura do Caio. Não mata: abre janelas. O combo PEM + facão é o coração do combate.

## Requisitos
- **R1** Descarga em cone de 30°, alcance 10 m, instantânea. Consome 1 carga (máximo 2).
- **R2** Recarga entre descargas: 1 s. Ruído de 12 m.
- **R3** Efeito por alvo (`IStunnable.Stun`):

| Alvo | Efeito |
| --- | --- |
| Sentinela | Paralisada por 3 s |
| Matilha | Todos os cães no cone paralisados por 3 s |
| Olho e Vespa | Caem e são destruídos |
| Brasa | Cai, apaga o fogo, fica 6 s no chão (vulnerável) |
| Brutamontes | Paralisado por 1,5 s |
| Antena | Sabotada (spec 07) |
| Capataz | Derruba o escudo na fase 2 (spec 17) |

- **R4** Robô paralisado: FSM no estado `Paralisado`, `NavMeshAgent` parado, visor apagado, faíscas. Offline na rede (spec 07).
- **R5** Ao sair da paralisia, o robô volta ao estado anterior e entra em alerta elevado.
- **R6** Paralisado = takedown disponível de qualquer ângulo (spec 09).
- **R7** O mesmo robô fica imune ao PEM por 5 s depois de acordar.

## Critérios de aceite
- [ ] PEM na Sentinela de frente + takedown em 3 s: a outra Sentinela não reage até o check-in.
- [ ] PEM em um grupo de 3 cães paralisa os 3.
- [ ] Sem cargas, o PEM não dispara e mostra aviso no HUD.

## Design técnico
- `PEM : BaseWeapon` usa `OverlapSphereNonAlloc` + filtro por ângulo (`Vector3.Dot`) + linha de visão.
- ~~`ParalisadoState<T>` genérico~~ Decidido: a paralisia fica na base `MachineAI` como um estado de pausa (não passa pela FSM). Ao acordar, a máquina reinicia o estado em que estava (`Fsm.Restart`) e fica em alerta elevado. Mais simples e vale para qualquer inimigo terrestre.
- A antena implementa `IStunnable`: uma descarga a sabota.
- Indicador de alerta fica azul piscando enquanto o robô está paralisado; o label de debug mostra PARALISADO.
- Visual: onda azul (partículas) e som elétrico.
