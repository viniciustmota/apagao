# 22 — Mapa e level design

**Status:** Greybox implementado (falta testar) · **Marco:** 11

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

## Ilha do Amparo (greybox, marco 11)
- Gerada por `Apagão > Criar Ilha do Amparo` (`IlhaAmparoBuilder`): terreno de 1000 × 1000 m, ilha com ~400 m de raio, mar fundo em volta.
- Zonas planas (`Pad`) sobre o relevo: Vila (0, −250), Sítio (−230, −50), Manguezal (245, −60, água de 1,1 m), Almoxarifado (130, 110), Rua principal (z = 200), Estação (0, 320) num morro.
- Mata: ~260 árvores (tronco com colisão, copa sem) e ~45 manchas de mato alto entre a vila e o resto.
- 3 rotas: vila (rua / telhados / mato ao norte), sítio (curral / caixa d'água / mata), almoxarifado (pátio / anexo → telhado / portas dos fundos), rua (chão / telhados com vãos de 1,2 m / quintais), estação (portão / muro de 2,5 m escalável).
- Uma rede e uma antena por zona com inimigos (R4). NavMesh gerado por zona quando o ato liga (a ilha inteira de uma vez travaria o início).
- R5: água rasa deixa o Caio a ×0,6 e apaga o rastro; agachado no manguezal, a cabeça fica abaixo d'água e a térmica não vê.
- R6: `VegetationPatch` reduz a visibilidade para as máquinas terrestres a ×0,4 (os drones ignoram). Mato ainda não queima.
- R7: luz fixa por ato no `ActManager` (noite, amanhecer, dia, pôr do sol, noite). Postes acesos na vila, na rua e na estação.
- R8: `SeaCurrent` empurra o Caio de volta quando a água passa de 1,3 m.

## Questões abertas
- Arte final: substituir o greybox por modelos à mão (ProBuilder/assets), mantendo as posições do builder.
