[🇺🇸 English](ai-first-operations.en.md)

# Operação segura dos agentes AI-First

### Correção da sintaxe do preflight — 11/10/2026

O [harness #38153935585](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38153935585), solicitado pela [issue #33](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/33), passou pela instalação de `bubblewrap 0.9.0` e pelo carregamento do perfil AppArmor, mas falhou no preflight devido à sintaxe: `codex sandbox linux --` tentou executar um binário chamado `linux` e retornou `Failed to execvp linux`. A CLI `0.160.0` utiliza `codex sandbox -- COMANDO` neste modo. A correção substitui apenas a chamada de teste por `codex sandbox -- /bin/sh ...`. Não altera isolamento, permissões, segredo ou parâmetros de inferência. Precisa de um teste real em `main` após revisão.

### Atualização do runner Ubuntu — 11/10/2026

A [issue #31](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/31) acionou corretamente o [AI Evolution Harness #38153271880](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38153271880), mas o preflight Landlock do Codex falhou com `filesystem-restricted execution requires bubblewrap to isolate app-server sockets`. O job `engineer` terminou sem executar o agente. Esse resultado invalida a alternativa Landlock isolada testada no PR #30 neste runner.

A próxima correção **retoma o sandbox Bubblewrap padrão** e instala `bubblewrap`, `apparmor-profiles` e `apparmor-utils` dos repositórios Ubuntu, carrega o perfil AppArmor específico `bwrap-userns-restrict` conforme documentação oficial e executa um comando de leitura no sandbox antes da inferência. **Não** altera a configuração global `kernel.apparmor_restrict_unprivileged_userns`, não desabilita o sandbox, não dá acesso irrestrito a shell e não amplia o `GITHUB_TOKEN`. O workflow falha fechado se não houver perfil ou se o sandbox não iniciar. É necessário um smoke real na `main` com a mudança aprovada para validar a compatibilidade do runner.

### Teste de sandbox Linux — 11/10/2026

Após o ajuste de cota da API, o mantenedor autorizou a nova execução da [run #38134885086](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38134885086). O Codex autenticou, recebeu a tarefa e retornou sem alteração de arquivos porque comandos sandboxed falharam com `bwrap: loopback: Failed RTM_NEWADDR: Operation not permitted` no runner Ubuntu. O job `engineer` falhou em `Export a patch` (patch vazio), e os jobs `validate` e `publish` foram ignorados.

A correção proposta habilita explicitamente a implementação **Landlock** do sandbox Linux do Codex (`--enable use_legacy_landlock`) e adiciona um *preflight* de execução de shell **dentro do sandbox** antes de iniciar a inferência. **Não** muda o modo `workspace-write` para `danger-full-access`, não amplia permissões de GitHub e não permite merge automático. A compatibilidade do backend alternativo precisa ser comprovada com teste real no runner antes de declarar o ciclo completo.

### Registro adicional de autenticação — 11/10/2026

Após o merge do [PR #26](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/26), a [issue #27](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/27) iniciou o [harness #38134291188](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38134291188). A CLI aceitou os argumentos e iniciou a sessão em `workspace-write`, mas recebeu **HTTP 401 / `Missing bearer or basic authentication in header`**. Não houve patch gerado: `validate` e `publish` foram ignorados. A causa observada é que definir `OPENAI_API_KEY` apenas como variável de ambiente não estabeleceu login no Codex CLI fixado. Esta correção usa `codex login --with-api-key` com o segredo via **stdin** em diretório temporário, sem publicar o valor em logs ou na árvore Git; somente o job `engineer` recebe o segredo. O diretório de autenticação é excluído no final do job. Ainda é necessário um smoke end-to-end após a revisão e o merge desta mudança de governança.

## Atualização operacional — 11/10/2026

O [PR #24](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/24) integrou a ponte por issue na `main` (commit `fcd8479cc0d2047d08b35e58d5676bc7a579cae0`). A [issue #25](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/25), criada pelo conector GitHub do ChatGPT, iniciou com sucesso a [ponte #38133025576](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38133025576), que disparou [AI Evolution Harness #38133032767](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38133032767).

A tentativa original falhou porque `OPENAI_API_KEY` estava ausente. **Depois que o mantenedor configurou o secret**, a segunda tentativa do mesmo run passou em `Require OpenAI API credential` e instalou o Codex, mas o `engineer` falhou ao executar `codex exec --full-auto`: a CLI fixada em `@openai/codex@0.160.0` retornou `unexpected argument '--full-auto' found`. Assim, `validate` e `publish` não executaram. O PR desta correção coloca as opções de aprovação em posição global (`codex --ask-for-approval never exec --sandbox workspace-write`) e introduz uma checagem de compatibilidade da CLI antes do agente. Mantém o sandbox de escrita no workspace, sem credenciais Git persistidas e sem permitir auto-merge.

**Estado:** disparo e secret validados; a implementação de ponta a ponta ainda exige nova execução na `main` com a correção aprovada e mergeada, seguida de `engineer → validate → publish`, criação de PR e CI/Security/DAST verdes. Não registrar este ciclo como concluído apenas pelo sucesso da ponte.

## Histórico da implantação — 10/10/2026

**Workflow instalado na `main`; execução ponta a ponta ainda não comprovada.** O mantenedor integrou os [PRs #17](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/17), [#18](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/18) e [#19](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/pull/19). O workflow ativo é [.github/workflows/ai-evolution.yml](../.github/workflows/ai-evolution.yml), presente na `main` desde o commit `d5f76b272d3b02826bd7b16eb4d032412bc5a015`.

O PR #19 passou em [CI](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38084752616) e [Security](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/runs/38084752611). Isso valida a alteração de governança nos checks de PR, **não comprova que Codex executou, que os secrets estão configurados ou que a publicação automática de PR funciona**. Na última consulta de 10/10/2026, não havia execução `AI Evolution Harness` confirmada. A [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15) permanece aberta até um teste end-to-end passar.

## Arquitetura do workflow

```mermaid
flowchart LR
    A[workflow_dispatch em main] --> E[Engineer: Codex CLI]
    E -->|patch binário, limite 10 MiB| V[Validate: runner isolado, somente leitura]
    V -->|SHA-256 do patch aprovado| P[Publish: token com escrita restrita]
    P --> PR[Branch ai/evolution-* e um PR]
    PR --> CHECK[CI + Security + DAST por workflow_dispatch]
    CHECK --> HUMAN[Revisão e merge humanos]
```

| Job | Permissões e responsabilidades |
| --- | --- |
| `engineer` | `contents: read`, sem credencial Git persistida; usa apenas `OPENAI_API_KEY` para Codex e exporta um patch, sem criar PR |
| `validate` | `contents: read`; importa o patch em runner independente, aplica um guard confiável copiado **antes** do patch e executa Python, restore, build Release, testes, formatação, segurança estática e Compose |
| `publish` | `contents: write`, `pull-requests: write` e `actions: write`; verifica o SHA-256 do mesmo patch e o guard, cria uma branch e um PR e solicita CI, Security e DAST; **não faz merge** |

Actions de terceiros são referenciadas por SHA fixo. O harness aceita disparo manual e também poderá ser solicitado pela ponte GitHub descrita abaixo, após aprovação da governança; a execução continua serializada por repositório e limitada a tarefas pequenas. O artefato patch fica retido por três dias; o fluxo não possui deploy em produção.

## Preparação no GitHub

1. Em **Settings → Secrets and variables → Actions**, confirme que `OPENAI_API_KEY` existe como **repository secret**. Não cole a chave no chat, nas issues, em arquivos ou logs; use credencial dedicada e limites de consumo adequados.
2. Em **Settings → Actions → General**, confira permissões do `GITHUB_TOKEN` e se GitHub Actions pode criar pull requests. O job `publish` requer escrita em conteúdo/PRs e permissão para disparar workflows; evite aumentar os privilégios globais do repositório.
3. Confirme que a branch escolhida é `main` e que os checks de CI e Security da versão atual foram concluídos. A conexão usada para documentar este processo **não expõe o estado dos secrets nem permite garantir que as permissões estejam configuradas**.

## Disparo pelo conector GitHub do ChatGPT (ponte com revisão humana)

A conexão GitHub disponível no ChatGPT já cria **issues** em nome da conta autenticada, mas não fornece uma operação nativa de `workflow_dispatch`. A proposta [`.github/workflows/ai-evolution-request.yml`](../.github/workflows/ai-evolution-request.yml) usa o evento **`issues: opened`**, executado pelo GitHub a partir do código confiável na **`main`**, para iniciar o harness existente sem enviar tokens ao chat. **A ponte só funciona depois da revisão e do merge humano do PR de governança.**

### Protocolo para iniciar pelo chat

1. Peça ao ChatGPT: **"Inicie o AI-First para [tarefa pequena e específica]"**. O assistente deverá criar **uma issue nova** no repositório, com título iniciado exatamente por `[AI-FIRST] `.
2. O corpo da issue deve iniciar exatamente com a linha `<!-- ai-first-request:v1 -->`, seguida de uma linha em branco e da tarefa em texto UTF-8 (entre 10 e 4096 bytes). **Não inclua segredos ou dados privados**, pois a issue ficará visível no GitHub. Exemplo:

   ```markdown
   <!-- ai-first-request:v1 -->

   Crie somente dois novos arquivos de documentação de teste,
   docs/ai-first-github-bridge-smoke.md e
   docs/ai-first-github-bridge-smoke.en.md.
   Não altere outros arquivos e não faça merge.
   ```

3. O workflow roda **somente para issues abertas pela conta proprietária** `WillianZanutoOliveira` com o título permitido. O validador [`scripts/ai-first-request-dispatch.py`](../scripts/ai-first-request-dispatch.py) verifica autor, formato, limites de tamanho e caracteres de controle, e prepara JSON sem executar o texto como shell.
4. O job limita seu `GITHUB_TOKEN` a `contents: read` e `actions: write`, chama `workflow_dispatch` do `ai-evolution.yml` com `ref=main`, e preserva `engineer → validate → publish` e revisão humana antes do merge.

**A issue é um pedido auditável, não uma autorização de merge.** Somente o evento `opened` aciona o bridge: editar, comentar ou reabrir a mesma issue não inicia uma segunda execução. O fluxo não modifica o código de produção, não dá acesso a `OPENAI_API_KEY` ao bridge e não altera os guardrails do harness.

**Limitações e controles:** autenticação do conector precisa realmente registrar a issue com o login do proprietário; caso contrário, o job será ignorado. A conexão deve ter permissão para criar issues; `actions: write` deve estar permitido para o job no GitHub. Após o merge, **teste com uma issue inofensiva**, confirme o evento, o job bridge, a execução do harness e os checks independentes. Configure limites de custos de Codex e Actions. O GitHub documenta que eventos `issues` usam o workflow da branch padrão e que `workflow_dispatch` é uma exceção permitida para chamadas com `GITHUB_TOKEN`.

A opção manual **Run workflow**, descrita a seguir, continua disponível se a configuração do repositório impedir o disparo automático.

## Primeira execução controlada

1. Abra [Actions → AI Evolution Harness](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/actions/workflows/ai-evolution.yml), selecione **Run workflow**, escolha `main` e informe a tarefa:

   ```text
   Crie somente docs/ai-first-github-bridge-smoke.md e docs/ai-first-github-bridge-smoke.en.md,
   explicando em português e inglês que este é um teste inofensivo do fluxo
   AI-First. Não altere outros arquivos, não inclua dados privados e não faça merge.
   ```

2. Acompanhe os jobs `engineer`, `validate` e `publish`. O sucesso exige uma nova branch `ai/evolution-<run>-<attempt>` e exatamente **um pull request revisável**, sem atualização automática da `main`.
3. No PR gerado, confirme **resultados concluídos e aprovados** de CI, Security e DAST, incluindo disparos explícitos por `workflow_dispatch`; agendar os checks não equivale a aprová-los.
4. Revise o diff humano e verifique que apenas os dois documentos foram criados. Não faça merge automático. Registre o link da execução e o PR na [issue #15](https://github.com/WillianZanutoOliveira/distributed-commerce-platform/issues/15).

**Critério de conclusão:** a instalação do YAML não basta. Somente considere o harness operacional após o smoke end-to-end bem-sucedido com segredo válido, publicação e gates independentes.

## Diagnóstico e correção

| Sintoma | O que verificar |
| --- | --- |
| `Require OpenAI API credential` falha | Secret `OPENAI_API_KEY` ausente ou vazio em Actions |
| `engineer` falha | Logs do Codex e versão instalada (`@openai/codex@0.160.0`); não exponha o token |
| `validate` reprova patch | Caminhos protegidos, guard, restore/build/test/format, Compose ou SHA-base |
| `publish` falha | Digest SHA-256, permissões do token, capacidade de criar branch/PR e disparar workflows |
| PR aparece mas checks faltam | Verifique execuções CI/Security/DAST na branch criada e autorizações manuais exigidas |

Não contorne guard, políticas de branch ou checks para fazer o fluxo passar. Falhas de Actions, versões do Codex ou permissões exigem correção revisada por humano quando afetam o workflow protegido.

## Verificação local do guard

Requer Git e Python 3, sem pacotes Python adicionais:

```bash
python3 -m unittest discover -s tests/ai_harness -p 'test_*.py' -v
BASE_SHA="$(git rev-parse HEAD)" # capturar ANTES de iniciar o agente
# Depois da execução, use o guard confiável salvo fora do workspace alterável:
python3 /trusted/path/ai-change-guard.py --repo . --base-sha "$BASE_SHA"
```

| Código | Significado |
| --- | --- |
| `0` | Há alterações e nenhum caminho protegido foi afetado |
| `1` | Arquivo de governança protegido alterado, criado, removido ou renomeado |
| `2` | Nenhuma alteração elegível ou erro de Git; falha fechada |

O guard detecta mudanças **já commitadas desde o SHA-base, staged, unstaged e untracked**; utiliza nomes terminados por NUL e `--no-renames` para não ocultar exclusões. O SHA-base deve ser um commit imutável completo, não `origin/main`. Além de arquivos individuais como `AGENTS.md`, todos os caminhos de `.github/workflows/`, `.ai/`, `docs/governance/` e `tests/ai_harness/` são protegidos. **Nunca execute uma cópia do guard que o próprio agente possa ter alterado.** O workflow mantém essa cópia em um runner separado e repete a checagem antes de publicar.

Consulte [ADR-0005](adr/0005-ai-engineering-harness.md), [AGENTS.md](../AGENTS.md) e a [constituição de engenharia](../.ai/engineering-constitution.md).
