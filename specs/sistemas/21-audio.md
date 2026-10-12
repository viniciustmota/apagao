# 21 — Áudio

**Status:** Implementada com sons provisórios (falta testar) · **Marco:** 10.5 (adiantado do 12)

## Objetivo
O som é jogabilidade: cada máquina é reconhecível de longe, e o silêncio é um aviso.

## Requisitos
- **R1** Mixer com grupos: Ambiente, Efeitos, Inimigos, Vozes, Música, Rádio.
- **R2** Assinaturas sonoras: Sentinela (servo motor baixo), Matilha (pisadas metálicas que aceleram), Olho (hélices + scanner), Vespa (zumbido agudo crescente), Brasa (chama + turbina), Brutamontes (passos com tremor).
- **R3** Ambiente: mar, vento nos coqueiros, cigarras. As cigarras silenciam num raio de 15 m em volta de qualquer máquina.
- **R4** Música dinâmica pelo nível global de alerta (maior estado entre os inimigos próximos): Exploração (silêncio/ambiente) → Tensão (Suspeito/Investigando) → Combate.
- **R5** Rádio com filtro passa-banda e ruído; degradação por ato (spec 19).
- **R6** Áudio 3D com atenuação realista; oclusão simples (abafar atrás de paredes).

## Implementação atual
- Sons gerados por síntese em `Tools/gerar_sons.py` → `Assets/_Apagao/Resources/Audio/*.wav` (56 arquivos). Trocar por um som real = salvar um `.wav` com o mesmo nome.
- `AudioManager` (em Sistemas): pool de 32 fontes 3D, `Sfx.Play`/`Sfx.Play2D` por nome, volumes por categoria (R1 sem AudioMixer: multiplicadores no componente; o mixer entra com o polimento).
- `LoopSound`: som contínuo preso ao objeto (motor da Sentinela, hélices, chama da Brasa, antena, fogo, isca), com volume e pitch em tempo real.
- R2: Sentinela = servo + passo metálico; Matilha = patinhas rápidas + servo agudo + rosnado no bote; Olho = hélice + scanner ao travar; Vespa = hélice aguda + bipes que aceleram; Brasa = hélice grave + chama; Brutamontes = servo grave + passos pesados + motor da investida.
- R3: mar, vento e cigarras; as cigarras calam a menos de 15 m de qualquer máquina (medido do ouvinte).
- R4: tensão (alguém suspeito/investigando/procurando ou drone alerta a até 50 m) e combate (alguém em combate), com 4 s de sobra.
- R5: chiado do rádio cresce com o sinal ruim; bipe no começo de cada fala da Lia. Sem filtro passa-banda de voz (não há voz gravada).
- R6: raycast do ouvinte até o som; atrás de parede = passa-baixa 900 Hz e −40% de volume. O terreno não abafa.
- Alertas sonoros: "alerta" quando uma máquina entra em combate, "suspeito" quando começa a desconfiar.

## Critérios de aceite
- [ ] De olhos fechados, dá para saber qual máquina está perto.
- [ ] O silêncio das cigarras avisa a chegada de uma patrulha antes de vê-la.
