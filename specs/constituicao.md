# Constituição do projeto

Regras que valem para todo o código e para toda spec. Mudar uma regra daqui exige registrar o motivo no fim deste arquivo.

## Princípios de design (do GDD)

1. **Você é a presa.** Nenhum sistema pode tornar o Caio mais forte que as máquinas em combate aberto. Lutar é o último recurso.
2. **Improviso brasileiro.** Toda arma e ferramenta é gambiarra de sucata. Nada de tecnologia militar pronta.
3. **Nenhum lugar é seguro.** Cada inimigo novo precisa quebrar pelo menos uma estratégia do jogador.
4. **Sistemas antes de conteúdo.** Poucos inimigos e um mapa pequeno, com IA rica. Um sistema só ganha arte final depois de ser divertido com cubos.
5. **Toda punição tem contra-jogada.** Qualquer mecânica que pune o jogador (ex.: a rede da VIGÍLIA) precisa de uma forma clara de ser contornada.

## Restrições técnicas

- **Engine:** Unity 6.6 (6000.6), URP, migrar para 6.7 LTS quando sair.
- **Plataforma:** PC Linux e Windows, teclado e mouse primeiro; controle depois.
- **Input:** somente o novo Input System (`activeInputHandler: 1`).
- **Fast Enter Play Mode está ativo:** campos `static` não são resetados entre Plays. Todo estado estático precisa ser limpo em um método `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- **Pacotes:** cada pacote novo exige uma decisão registrada na spec que o usa. Hoje o NavMesh é gerado em runtime (`NavMeshBaker`), sem o pacote AI Navigation.
- **Cenas geradas por código:** a cena de teste é recriada por `Apagão > Criar Ilha de Teste`. Ajustes feitos à mão nessa cena se perdem; mudanças permanentes vão para os builders em `Assets/_Apagao/Editor`.

## Convenções de código

- **Pasta raiz:** `Assets/_Apagao/`. Nada do jogo fora dela, além dos pacotes.
- **Namespaces** espelham as pastas: `Apagao.Player`, `Apagao.AI`, `Apagao.AI.Sentinela`, `Apagao.Systems`, `Apagao.Combat`, `Apagao.Crafting`, `Apagao.EditorTools`.
- **Idioma:** infraestrutura em inglês (`StateMachine`, `VisionSensor`, `NoiseSystem`); conteúdo do jogo em português (`SentinelaAI`, `PatrulhaState`, `Facao`, `Matilha`). Comentários e tooltips em português.
- **Uma classe por estado** de IA, em `States/`, herdando `State<TOwner>`.
- **Campos configuráveis** são `[SerializeField] private` com `[Header]` e `[Tooltip]`. Nada de campo público mutável.
- **Dados de balanceamento** (armas, receitas, parâmetros de inimigo) vão para `ScriptableObject` em `Assets/_Apagao/Data/` assim que o sistema estiver estável.
- **Comunicação entre sistemas** por interfaces (`IDamageable`, `IStunnable`, `IInteractable`, `INoiseListener`) e eventos C#. Evitar `FindObjectOfType` fora de `Awake`/`Start`.
- **Unidades:** metros, segundos, graus. Velocidades em m/s.
- **Sem `Update` vazio** e sem alocação por frame em código de IA (`NonAlloc`, buffers reutilizados).

## Definição de pronto

Um sistema está **pronto** quando:

- [ ] Compila sem erros e sem warnings novos.
- [ ] Todos os critérios de aceite da spec foram testados no Play e marcados.
- [ ] Os parâmetros principais são ajustáveis no Inspector.
- [ ] Existe feedback visual de debug (gizmo, label ou indicador) para o que não é óbvio.
- [ ] O builder da cena de teste cria o sistema sozinho.
- [ ] Commit com mensagem `Marco N: <sistema>` e status atualizado no `README.md` das specs.

## Registro de mudanças nesta constituição

- 2026-10-12 — Versão inicial.
