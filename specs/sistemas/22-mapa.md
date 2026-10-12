# 22 — Mapa e level design

**Status:** Rascunho · **Marco:** 11 (ilha de teste já existe)

## Objetivo
Uma ilha pequena e densa, com várias rotas em toda área.

## Requisitos
- **R1** Ilha de cerca de 1 km² jogável, vila ao sul e estação ao norte, caminho dando a volta pela mata.
- **R2** Zonas do GDD: Vila dos pescadores, Mata e coqueiral, Sítio do pai, Manguezal, Galpão da estação, Estação de cabos.
- **R3** **Regra das 3 rotas:** toda área tem pelo menos uma rota pelo chão, uma vertical (telhado/passarela) e uma escondida (mato/água).
- **R4** Cada área com rede (spec 07) tem uma antena visível de longe e alcançável por pelo menos duas rotas.
- **R5** Água rasa (até 1,2 m) caminhável e lenta (×0,6); esconde da térmica com o Caio agachado nela.
- **R6** Mato alto (layer Vegetation): reduz a visibilidade a ×0,4, não bloqueia movimento, pode queimar (spec 15).
- **R7** Ciclo dia/noite por ato (fixo, não em tempo real).
- **R8** Limites da ilha: mar fundo empurra o Caio de volta (corrente).

## Ilha de teste (existente)
- Gerada por `Apagão > Criar Ilha de Teste`: terreno de 400 × 400 m, vila com rua leste-oeste, 10 casas, caixotes, igreja, 2 Sentinelas.
- Continua sendo o laboratório de mecânicas; a ilha final é outra cena.

## Questões abertas
- Construir a ilha final à mão (Terrain + ProBuilder) ou expandir o builder? Proposta: à mão, usando os prefabs que os builders geram.
