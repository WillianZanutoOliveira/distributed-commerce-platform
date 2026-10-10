[🇺🇸 English](0015-ai-first-request-bridge.en.md)

# ADR-0015 — Ponte de solicitação do AI-First via GitHub

## Estado
Proposta de governança — exige revisão e merge humano, validação de CI/Security e smoke end-to-end antes da ativação.

## Problema
O workflow `ai-evolution.yml` suporta `workflow_dispatch`, mas a integração GitHub disponibilizada ao ChatGPT expõe operações de branch/arquivo/PR sem a ação nativa de despacho. Alterar permissões do GitHub App não adiciona automaticamente ferramentas à conexão. Não é aceitável expor credenciais GitHub ou `OPENAI_API_KEY` no chat.

## Escolha
Adicionar **um workflow independente** (`.github/workflows/ai-evolution-request.yml`) como ponte de governança de escopo mínimo:
1. Disparar por `push` em `ai-requests/**` e apenas quando o commit alterar `.ai-requests/task.md`.
2. Verificar que o **commit filho direto** do histórico da `main` adicionou somente `.ai-requests/task.md`; rejeitar mudanças adicionais, reuso do arquivo, symlinks, codificação inválida ou tarefa maior que 4 KiB.
3. Passar somente o texto do arquivo como entrada JSON à API de `workflow_dispatch`, fixando `ref=main`, usando `contents:read` e `actions:write` restritos a esse job. Não executar o texto como shell nem registrar o conteúdo da tarefa em logs.
4. Preservar o workflow original: agente sem credenciais Git persistidas, validador isolado, guard confiável, patch verificado, PR para revisão humana e sem merge automático.

## Riscos e consequências
- Usuários com acesso de escrita a branches de solicitação conseguem pedir execuções e gerar custo de Codex/CI. Gerencie acessos, orçamento, políticas e observe os logs.
- O evento de `push` vindo da conexão GitHub App **deve ser comprovado em teste real**; commits feitos com `GITHUB_TOKEN` por outro workflow normalmente não iniciam novos eventos de Actions.
- Ainda depende de `OPENAI_API_KEY` e das permissões do job `publish` no harness. Falhas devem ser auditadas; a ponte não corrige automaticamente secrets nem controles do GitHub.
- O endpoint administrativo de despacho poderia ser exposto nativamente por outro conector no futuro; nesse caso, esta ponte pode ser desativada após migração revisada.

## Critério de aceitação
Revisão humana da mudança protegida; checks verdes; criação de uma branch `ai-requests/<id>` via ChatGPT; confirmação de execução do bridge e do `AI Evolution Harness` em `main` com mesmo texto; `engineer`, `validate`, `publish` e checks independentes aprovados, sem merge automático.
