[🇺🇸 English](0015-ai-first-request-bridge.en.md)

# ADR-0015 — Disparo seguro do AI-First por issues GitHub

## Status
Proposta de governança. Requer revisão humana, merge manual e teste end-to-end para comprovar o disparo.

## Contexto
O `AI Evolution Harness` da `main` recebe `workflow_dispatch` com `inputs.task`, mas a conexão GitHub deste ChatGPT não disponibiliza essa ação. Ela disponibiliza `create_issue`. A exposição de tokens GitHub/OpenAI no chat é proibida.

## Decisão
Adicionar um workflow de ponte com gatilho `issues: opened`. O GitHub carrega esse evento a partir da **branch padrão `main`**, evitando que código de uma branch de solicitação modifique o próprio validador. O job só aceita issues cujo título comece com `[AI-FIRST] ` e cujo autor autenticado seja `WillianZanutoOliveira`.

O corpo precisa começar com `<!-- ai-first-request:v1 -->` e conter uma tarefa não vazia entre **10 e 4096 bytes UTF-8**. O helper `scripts/ai-first-request-dispatch.py` também recusa caracteres de controle e verifica o autor novamente, com testes de regressão em `tests/ai_request_bridge/`. Nenhuma instrução da issue é interpolada como shell.

O token é **restrito ao job**, com `contents:read` e `actions:write`; envia POST ao endpoint `/actions/workflows/ai-evolution.yml/dispatches` passando `ref: main` e `inputs.task` como JSON. O fluxo `engineer → validate → publish` permanece inalterado e sem auto-merge. A issue permanece no histórico como auditoria.

## Consequências e riscos
- Apenas a conta proprietária pode pedir a execução por issues. Se o GitHub App do ChatGPT criar issues atribuídas a outro usuário, o job será ignorado; é preciso comprovar a autoria em smoke real.
- O campo de tarefa é público na issue: não armazenar segredos, PII ou dados sensíveis. O bridge não recebe `OPENAI_API_KEY`.
- O custo de Codex/CI exige limites e monitoramento. O evento `opened` não é reexecutado por edição da issue.
- `GITHUB_TOKEN` pode emitir `workflow_dispatch`; se as políticas bloquearem `actions:write`, o processo falhará sem contornar controles.
- Workflow de governança e permissões precisam de revisão humana específica antes de entrar na `main`.

## Critérios de aceitação
CI/Security aprovados, revisão humana, merge manual, nova issue inofensiva do proprietário com marcador válido, execução efetiva de ponte e harness na `main`, jobs `engineer`, `validate`, `publish`, novos checks e PR review-only, sem merge automático.
