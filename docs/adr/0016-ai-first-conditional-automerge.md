[🇺🇸 English](0016-ai-first-conditional-automerge.en.md)

# ADR-0016 — Revisão automática condicionada do AI-First

## Status
Proposta de governança. Requer CI, Security, revisão da configuração de permissões e validação E2E antes de habilitar merges sem intervenção humana.

## Contexto
O AI Evolution Harness comprovou o ciclo de issue do proprietário → Codex em Bubblewrap/AppArmor → patch imutável com SHA-256 → validador isolado → PR criado pelo GitHub Actions → CI/Security/DAST. Em 11/10/2026, o PR #38 foi aprovado nos três checks e mesclado. O usuário solicitou eliminar a etapa humana de revisão e merge.

## Decisão
Adicionar um job de **revisão por política independente** depois de publish. Ele executa código fixado no commit original confiável da main e recebe somente o PR e SHA publicados pelo harness, nunca executa arquivos ou instruções do PR. O processo verifica autoria por github-actions[bot], a única branch ai/evolution-<run>-<attempt>, base imutável, commit único, ausência de solicitações de revisão de terceiros ou equipes (a solicitação automática do proprietário prevista em CODEOWNERS é informativa apenas para docs elegíveis), bloqueios manuais, caminhos alterados e modos de arquivo normais. Faz fail-closed se a main se mover, se o PR mudar ou se não conseguir verificar qualquer campo.

**Primeira autorização limitada:** um a quatro **arquivos Markdown novos**, dentro de docs/ e subpastas de baixo risco, com no máximo 250 linhas novas e sem deleções, scripts, symlinks, alteração de código, configuração, docs/adr, docs/governance, docs/security ou workflows. Todos os demais PRs ficam abertos para revisão separada; nunca recebem auto-merge. Esta autorização não se estende a mudanças de produção, GitOps ou migrações.

Antes do merge, a mesma identidade do PR/commit deve ser revalidada e os resultados de três execuções independentes, originadas após o harness, do workflow_dispatch de ci.yml, security.yml e dast.yml devem estar completed/success para a **mesma branch e SHA**. O job usa GITHUB_TOKEN limitado a contents:write, pull-requests:write e actions:read, registra evidência e solicita merge pelo endpoint GitHub com SHA esperado, respeitando as proteções existentes.

Não há aprovação falsa de outro usuário nem bypass de regras do GitHub. A revisão automatizada consiste em política determinística mais CI/Security/DAST, não em promessa de que uma IA provou a correção funcional.

## Riscos e controles
- Branch protection/rulesets que exigem reviews humanos continuam prevalecendo; o merge falha sem contornar regras.
- Se o CI ou qualquer scan falhar, expirar ou divergir do SHA, nenhum merge acontece.
- Um runner de revisão **não** recebe OPENAI_API_KEY, não carrega nem executa código do PR, nem modifica políticas de governança.
- O limite inicial evita aprovar mudanças de aplicação/segurança com simples testes.
- O PR de habilitação é uma alteração de governança, portanto precisa de validação separada antes de merge.

## Aceite
CI/Security verdes no PR de governança; teste real com issue de documentação pequena, PR criado automaticamente, revisão por política concluída, três checks independentes verdes e merge na main sem ação humana. Garantir que uma tarefa em caminho proibido permanece aberta.
