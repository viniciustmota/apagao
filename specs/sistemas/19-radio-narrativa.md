# 19 — Rádio e narrativa

**Status:** Parcial (legendas e sinal implementados, falta testar; áudio e alto-falantes no marco 12) · **Marco:** 11

## Objetivo
Dar o peso emocional no estilo The Last of Us sem companheira controlada por IA: a voz da Lia no rádio, cada vez mais fraca, e a voz do prefeito usada pelo Capataz.

## Requisitos
- **R1** Falas como dados (`DialogueLine`: personagem, texto, áudio opcional, duração).
- **R2** Legendas sempre ativas no MVP (sem dublagem completa).
- **R3** Rádio da Lia: uma conversa curta por ato, disparada por gatilho; o Caio responde segurando V.
- **R4** Degradação do sinal por ato: chiado e cortes aumentam (filtro de áudio + legenda com trechos "…").
- **R5** Alto-falantes da vila: o Capataz fala com a voz do prefeito em momentos-chave (ato 2 em diante) e reage a eventos (antena sabotada, robô destruído).
- **R6** Momentos roteirizados do GDD: a Matilha arrastando algo pela rua (prólogo), a igreja em chamas (ato 3), a resposta da VIGÍLIA com a voz da Lia (final).
- **R7** Falar no rádio faz ruído baixo (2 m).

## Implementação atual (marco 11)
- `DialogueLine` (personagem, texto, duração) e `RadioSystem` com fila de falas e legenda no `CampaignHUD`.
- Sinal por objetivo (0 a 1): na fala da Lia, palavras viram "…" com chance `(1 − sinal) × 0,6` (R4).
- Fala do Caio emite ruído de 2 m (R7). Responder segurando V e áudio ficam para depois.
- Final da demo: a "Lia" responde com sinal perfeito, e a legenda revela que não é ela (R6, versão em texto).

## Critérios de aceite
- [ ] Cada ato tem pelo menos uma conversa com a Lia.
- [ ] A resposta final no rádio usa a voz da Lia sintetizada e fecha o jogo.

## Questões abertas
- Roteiro das falas: escrever na próxima revisão do GDD.
