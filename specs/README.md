# Specs — Apagão: Ilha do Amparo

O jogo é desenvolvido por **Spec-Driven Development (SDD)**: nenhum sistema é implementado sem uma spec aprovada, e o código segue a spec. Quando a implementação revela que a spec estava errada, a spec é corrigida **antes** de mudar o código.

O GDD (visão de design, história e tom) fica no documento compartilhado. Estas specs traduzem o GDD em requisitos testáveis e em decisões técnicas.

## Estrutura

| Arquivo | Conteúdo |
| --- | --- |
| [constituicao.md](constituicao.md) | Regras que valem para todo o projeto: princípios, convenções de código, definição de pronto |
| [arquitetura.md](arquitetura.md) | Visão técnica: camadas, pastas, namespaces, interfaces centrais, fluxo de eventos |
| [roadmap.md](roadmap.md) | Marcos, tarefas e status |
| [sistemas/](sistemas/) | Uma spec por sistema |

## Fluxo de trabalho

1. **Spec:** escrever ou revisar a spec do sistema (requisitos + critérios de aceite). Status `Rascunho` → `Aprovada`.
2. **Plano:** preencher a seção *Design técnico* (classes, dados, parâmetros).
3. **Tarefas:** quebrar em tarefas no [roadmap.md](roadmap.md).
4. **Implementar:** uma tarefa por vez, sempre com commit.
5. **Verificar:** testar no Play cada critério de aceite e marcar `[x]`. Status `Implementada` → `Validada`.

## Status das specs

| # | Sistema | Marco | Status |
| --- | --- | --- | --- |
| 01 | [Movimento do jogador](sistemas/01-movimento-jogador.md) | 1, 7 | Implementada |
| 02 | [Câmera](sistemas/02-camera.md) | 1 | Implementada |
| 03 | [Núcleo de IA (FSM)](sistemas/03-nucleo-ia.md) | 2 | Implementada |
| 04 | [Percepção: visão](sistemas/04-visao.md) | 3 | Implementada |
| 05 | [Sentinela](sistemas/05-sentinela.md) | 2–8 | Implementada |
| 06 | [Ruído e audição](sistemas/06-ruido.md) | 4 | Implementada |
| 07 | [Rede da VIGÍLIA (mente coletiva)](sistemas/07-rede-vigilia.md) | 4 | Implementada (HUD provisório) |
| 08 | [Vida e dano](sistemas/08-vida-dano.md) | 5, 8 | Implementada (sem checkpoint) |
| 09 | [Facão e takedowns](sistemas/09-facao-takedown.md) | 5, 8 | Implementada |
| 10 | [Armas de fogo](sistemas/10-armas-fogo.md) | 7 | Implementada |
| 11 | [PEM](sistemas/11-pem.md) | 7, 10 | Implementada |
| 12 | [Recursos, inventário e crafting](sistemas/12-crafting.md) | 8 | Implementada (menu em lista) |
| 13 | [Escalada e telhados](sistemas/13-escalada.md) | 9 | Implementada |
| 14 | [Matilha](sistemas/14-matilha.md) | 6 | Implementada (rastro na água pendente) |
| 15 | [Drones: Olho, Vespa e Brasa](sistemas/15-drones.md) | 10 | Implementada (queima de mato no marco 11) |
| 16 | [Brutamontes](sistemas/16-brutamontes.md) | 11 | Rascunho |
| 17 | [Capataz (chefe final)](sistemas/17-capataz.md) | 12 | Rascunho |
| 18 | [Campanha, objetivos e checkpoints](sistemas/18-campanha.md) | 11 | Rascunho |
| 19 | [Rádio e narrativa](sistemas/19-radio-narrativa.md) | 11 | Rascunho |
| 20 | [Interface (HUD)](sistemas/20-hud.md) | 5–11 | Parcial (IMGUI provisório) |
| 21 | [Áudio](sistemas/21-audio.md) | 12 | Rascunho |
| 22 | [Mapa e level design](sistemas/22-mapa.md) | 11 | Rascunho |

`Implementada` significa que o código existe; só vira `Validada` depois do teste no Play.
