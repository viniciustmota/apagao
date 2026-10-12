# 01 — Movimento do jogador

**Status:** Implementada (falta esquiva) · **Marco:** 1 (esquiva no 7) · **Código:** `Scripts/Player/PlayerController.cs`

## Objetivo
Dar ao Caio um movimento responsivo de sobrevivente comum: rápido o bastante para fugir, nunca rápido o bastante para fugir de tudo.

## Requisitos
- **R1** Andar com WASD, relativo à direção da câmera, a 3 m/s.
- **R2** Correr segurando Shift a 6 m/s. Correr agachado levanta o Caio, se houver espaço.
- **R3** Agachar com C ou Ctrl (alternar) a 1,5 m/s; altura cai de 1,8 m para 1,1 m.
- **R4** Não levantar se houver teto a menos de 1,8 m.
- **R5** Pular com Espaço até 1,25 m de altura; agachado, Espaço levanta.
- **R6** O Caio vira suavemente para a direção do movimento (720°/s).
- **R7** Expor `Mode` (`Parado`, `Andando`, `Correndo`, `Agachado`) para o ruído e a percepção.
- **R8** *(Marco 7)* Esquiva com Alt: rolamento de 3 m em 0,4 s na direção do input, invulnerável a explosões nos primeiros 0,25 s, recarga de 0,8 s. Gera ruído médio.
- **R9** *(Marco 9)* Escalada de bordas, definida na spec 13.

## Critérios de aceite
- [ ] Andar, correr e agachar respondem sem atraso perceptível.
- [ ] Agachado embaixo de uma estrutura baixa, apertar C não levanta.
- [ ] Dá para subir caixote de 1 m → caixote de 2 m → telhado de 3 m só pulando.
- [ ] Em descida de rampa o Caio não "quica".
- [ ] *(R8)* A esquiva desvia de uma Vespa se apertada no último segundo.

## Design técnico
- `CharacterController`, gravidade própria (−20 m/s²), aceleração por `Vector3.MoveTowards` (20 m/s²).
- Agachar altera `controller.height` e `center`; a checagem de teto usa `SphereCast` para cima.
- Visual (cápsula) é achatado junto com a altura.

| Parâmetro | Valor |
| --- | --- |
| walkSpeed | 3 m/s |
| runSpeed | 6 m/s |
| crouchSpeed | 1,5 m/s |
| jumpHeight | 1,25 m |
| standHeight / crouchHeight | 1,8 / 1,1 m |

## Fora do escopo
Nadar (entra com a spec 22), animações de personagem, controle (gamepad).

## Questões abertas
- Corrida deve ter estamina? Proposta: não no MVP; o barulho já é o custo de correr.
