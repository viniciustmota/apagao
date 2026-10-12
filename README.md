# Apagão: Ilha do Amparo

Jogo de sobrevivência furtiva em terceira pessoa, feito em Unity. Em 2026, uma IA de defesa chamada **VIGÍLIA** toma o controle de drones e robôs e ocupa uma ilha de pescadores no litoral do Nordeste. Você é **Caio**, um eletricista que ficou para trás e precisa chegar ao rádio da estação de cabos submarinos para pedir resgate.

**Referências:** The Last of Us (escassez e tensão) + Ghost of Tsushima (telhados e takedowns) + Exterminador do Futuro (ambientação).

> Projeto pessoal de aprendizado, desenvolvido por Spec-Driven Development. Em desenvolvimento: MVP, marco 9 de 12.

## O que já existe

| Sistema | Estado |
| --- | --- |
| Movimento do Caio (andar, correr, agachar, pular) e câmera por cima do ombro | Implementado |
| Sentinela: patrulha, visão em cone, suspeita, investigação, combate com rifle | Implementado |
| Ruído: passos, quedas e golpes viram som que as máquinas ouvem | Implementado |
| Rede da VIGÍLIA: visão compartilhada, check-in de robôs e antena sabotável | Implementado |
| Vida e dano, facão (leve, pesado, durabilidade) e takedown furtivo | Implementado |
| Matilha: cães robô que farejam o rastro e caçam em grupo | Implementado |
| Revólver, espingarda de pesca, PEM (paralisa robôs) e esquiva | Implementado |
| Recursos, saque, crafting, bancada, bomba de ruído, molotov, isca elétrica e kit de cura | Implementado |
| Escalada de bordas, telhados, borda segura agachado e dano de queda | Implementado |
| Drones, Brutamontes, campanha | Planejado |

O status detalhado de cada sistema fica em [`specs/README.md`](specs/README.md) e o plano em [`specs/roadmap.md`](specs/roadmap.md).

## Requisitos

- **Unity 6.6** (6000.6.5f1) com **Universal Render Pipeline**
- Linux (desenvolvido no Pop!_OS) ou Windows
- Git e Git LFS
- IDE recomendada: JetBrains Rider

## Como rodar

```bash
git lfs install
git clone https://github.com/viniciustmota/apagao.git
```

1. Abra a pasta no **Unity Hub** com a versão 6000.6.
2. Espere a primeira importação e compilação.
3. No menu do topo, clique em **Apagão → Criar Ilha de Teste**.
4. Aperte **Play**.

A cena de teste é **gerada por código**. Mudanças feitas à mão nela se perdem ao recriar; mudanças permanentes vão nos builders em `Assets/_Apagao/Editor/`.

## Controles

| Tecla | Ação |
| --- | --- |
| WASD | Andar |
| Shift | Correr |
| C ou Ctrl | Agachar |
| Espaço | Pular · de frente para uma borda: escalar |
| Mouse | Câmera |
| Alt | Esquivar |
| 1 / 2 / 3 / 4 | Facão / Revólver / Espingarda / PEM |
| Botão direito | Mirar (armas e PEM) |
| Clique esquerdo | Facão: leve (segurar e soltar = pesado) · mirando: disparar |
| R | Recarregar |
| Segurar E | Coletar item |
| Segurar Tab + número | Fabricar (receitas elétricas só perto da bancada) |
| T / segurar G | Escolher / arremessar (bomba de ruído, molotov, isca) |
| H | Usar kit de primeiros socorros |
| E | Takedown (por trás) · segurar perto da antena = sabotar |
| Esc | Soltar o mouse (clique para prender de novo) |

## Estrutura

```
Assets/_Apagao/
  Scripts/
    Player/       Movimento, câmera, passos, rastro, morte
    AI/Core/      StateMachine e MachineAI (base de toda máquina terrestre)
    AI/Senses/    Visão e audição
    AI/Hive/      Rede da VIGÍLIA: rede, membros e antena
    AI/Sentinela/ Robô humanoide e seus estados
    AI/Matilha/   Cães robô, grupo e estados
    Combat/       Vida, dano, facão, takedown
    Systems/      Ruído e NavMesh em runtime
    UI/           HUD
    DebugTools/   Painel de teste da rede
  Editor/         Builders da cena de teste (menu Apagão)
specs/            SDD: constituição, arquitetura, roadmap e uma spec por sistema
```

## Como o projeto é desenvolvido

O jogo segue **Spec-Driven Development**:

1. Todo sistema tem uma spec em [`specs/sistemas/`](specs/sistemas/) com requisitos e critérios de aceite.
2. O código segue a spec. Se a implementação mostrar que a spec estava errada, a spec é corrigida primeiro.
3. Um sistema só é dado como validado depois de testado no Play.

As regras do projeto (convenções, arquitetura, definição de pronto) ficam em [`specs/constituicao.md`](specs/constituicao.md) e [`specs/arquitetura.md`](specs/arquitetura.md). Agentes de IA devem ler o [`AGENTS.md`](AGENTS.md) antes de mexer no código.

## Dicas de desenvolvimento

- **Pare o Play antes de atualizar scripts.** Com o Play rodando, a Unity adia a importação de arquivos novos.
- **Arquivos novos não aparecem?** Aperte **Ctrl+R** (Assets → Refresh) e espere compilar.
- **Mudou valores padrão de um componente?** Recrie a cena: a cena antiga guarda os valores antigos.
- **Debug da IA:** na aba Scene, selecione um robô para ver o cone de visão; o estado e a suspeita aparecem escritos acima dele. Os sons aparecem como círculos.
- **Fast Enter Play Mode está ativo:** campos `static` não são resetados entre Plays; limpe-os em `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.

## Autor

Vinícius Mota — [@viniciustmota](https://github.com/viniciustmota)
