# Apagão: Ilha do Amparo — instruções para agentes de IA

Jogo de sobrevivência furtiva em Unity 6.6 (URP), desenvolvido por Spec-Driven Development.

## Antes de qualquer código
1. Leia `specs/constituicao.md` (regras do projeto) e `specs/arquitetura.md`.
2. Encontre a spec do sistema em `specs/sistemas/`. Sem spec aprovada, escreva ou atualize a spec primeiro e peça aprovação.
3. Implemente só o que a spec pede. Se a spec estiver errada, corrija a spec antes do código.

## Depois de implementar
- Atualize o status da spec em `specs/README.md` e as tarefas em `specs/roadmap.md` (`[~]` implementado, `[x]` só depois de testado no Play).
- Se a mudança afeta a cena de teste, atualize os builders em `Assets/_Apagao/Editor/`.
- Commits: `Marco N: <sistema>`.

## Lembretes técnicos
- Código do jogo só em `Assets/_Apagao/`.
- Novo Input System apenas. Fast Enter Play Mode ativo: resetar estáticos em `SubsystemRegistration`.
- Infraestrutura em inglês, conteúdo do jogo em português, comentários em português.
