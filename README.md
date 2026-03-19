# Seguro Auto - Workshop de Arquitetura

Este repositório contém material didático e técnico para um workshop de arquitetura, utilizando um sistema legado no domínio de Seguro para Automóveis.

Resumo rápido
- Produto: Seguro para Automóveis
- Objetivo: Workshop de arquitetura com foco em observabilidade e sistemas legados
- Premissas: baixo atrito, execução local, cenários controlados, dados consistentes

Tecnologias principais
- CoreWCF (compatibilidade com SOAP em .NET moderno)
- .NET Aspire (orquestração local e observabilidade)
- PostgreSQL (banco de dados via container Aspire)
- OpenTelemetry (tracing distribuído end-to-end)
- YARP (reverse proxy / gateway)

> ! Sobre o Rider
>
> O Rider ainda não está com suporte completo ao Aspire.NET e existem bugs.
> Considere seguir as instruções desse arquivo para executar as demos via terminal.

---

Sumário
- [Objetivo do Projeto](#objetivo-do-projeto)
- [Racional da Tecnologia](#racional-da-tecnologia)
- [Orquestração com .NET Aspire](#orquestracao-com-net-aspire)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Observabilidade](#observabilidade)
- [Telemetria do Browser](#telemetria-do-browser)
- [Telemetria de Banco de Dados](#telemetria-de-banco-de-dados)
- [Resource Detectors (Container/Host)](#resource-detectors)
- [Casos de Uso do Workshop](#casos-de-uso-do-workshop)
- [Banco de Dados (PostgreSQL)](#banco-de-dados-postgresql)
- [Dataset mockado e seeding](#dataset-mockado-e-seeding)
- [Variáveis de Ambiente](#variaveis-de-ambiente)
- [Como Inicializar](#como-inicializar)
- [Injeção de Falhas, Delay e Caos](#injecao-de-falhas-delay-e-caos)
- [Erros Simulados via Interface](#erros-simulados-via-interface)
- [Troubleshooting com CorrelationId](#troubleshooting-com-correlationid)

---

<a id="objetivo-do-projeto"></a>
## Objetivo do Projeto

Este projeto foi criado para simular, de forma controlada e didática, um ambiente típico de legado corporativo em uma seguradora, especificamente no domínio de Seguro para Automóveis. O objetivo é permitir:

- Executar serviços WCF (SOAP) em ambiente moderno
- Demonstrar problemas clássicos de sistemas legados
- Demonstrar observabilidade distribuída com OpenTelemetry e Aspire
- Demonstrar telemetria de banco de dados com correlation_id
- Demonstrar telemetria do browser (client-side) conectada ao trace do servidor
- Simular erros controlados na integração WCF e na procedure do banco
- Exercitar troubleshooting usando CorrelationId e o Aspire Dashboard
- Introduzir falhas, delays e caos de forma controlada

---

<a id="racional-da-tecnologia"></a>
## Racional da Tecnologia

### Por que CoreWCF?

O WCF clássico (server-side) não é suportado diretamente em .NET moderno nem em Linux. Para reduzir fricção e ainda manter fidelidade conceitual ao legado, utilizamos CoreWCF:

- Port do modelo de programação WCF para ASP.NET Core
- Mantém atributos como `[ServiceContract]` e `[OperationContract]` e suporte a SOAP
- Suporta bindings como `BasicHttpBinding`
- Roda em Linux, containers e .NET moderno

### Redução de fricção (princípio central)

- Não depender de Windows ou IIS
- Rodar igual em macOS / Linux / Windows
- Um comando para executar tudo (`dotnet run`)
- PostgreSQL sobe automaticamente via container Aspire

---

<a id="orquestracao-com-net-aspire"></a>
## Orquestração com .NET Aspire

O .NET Aspire é usado como plataforma de orquestração local:

- Sobe PostgreSQL como container automaticamente
- Gerencia todos os serviços com portas dinâmicas
- Injeta connection strings e variáveis de ambiente
- Fornece observabilidade (logs, traces, métricas) via Dashboard
- Injeta automaticamente `OTEL_EXPORTER_OTLP_ENDPOINT` nos serviços

---

<a id="estrutura-do-projeto"></a>
## Estrutura do Projeto

```
src/
  Shared/
    SeguroAuto.Domain/          # Entidades de domínio
    SeguroAuto.Data/            # DbContext, seeding, DbOperationLog
    SeguroAuto.Common/          # Utilitários compartilhados
    SeguroAuto.FaultInjection/  # Middleware de injeção de falhas com tracing
    SeguroAuto.ServiceDefaults/ # OpenTelemetry + Resource Detectors + health checks
  Legacy/
    Legacy.QuoteService/        # Serviço SOAP de cotações (com procedures simuladas)
    Legacy.PolicyService/       # Serviço SOAP de apólices
    Legacy.ClaimsService/       # Serviço SOAP de sinistros
    Legacy.PricingRulesService/ # Serviço SOAP de regras de precificação
    Legacy.Gateway/             # YARP reverse proxy com tracing
    Legacy.DbTelemetryWorker/   # Worker que exporta telemetria do banco via OTLP
    Legacy.AppHost/             # Aspire AppHost (orquestração)
    Legacy.TestClient/          # Cliente de teste
  Frontend/
    SeguroAuto.Web/             # Frontend MVC com telemetria do browser
```

---

<a id="observabilidade"></a>
## Observabilidade

O projeto implementa tracing distribuído end-to-end usando OpenTelemetry com exportação OTLP para o Aspire Dashboard.

### Fluxo completo do trace

```
Browser (click/page load)
  └── Frontend MVC (ASP.NET Core)
        └── SOAP Client span (envelope XML capturado)
              └── Gateway YARP (path real no span)
                    └── Serviço SOAP (CoreWCF)
                          └── FaultInjection (delay/erro)
                                └── Procedure no banco (sp_create_quote)
                                      └── Informações da sessão PostgreSQL
```

### ActivitySources registradas

| ActivitySource | Onde aparece | O que captura |
|----------------|-------------|---------------|
| `SeguroAuto.Web.SoapClient` | frontend | Chamadas SOAP com envelope XML request/response |
| `SeguroAuto.FaultInjection` | serviços SOAP | Delay, erro e chaos injections |
| `SeguroAuto.Database` | db-telemetry-worker | Operações SQL da procedure com sessão PostgreSQL |
| `SeguroAuto.Browser` | frontend | Page load, clicks, form submits do browser |

### Dashboard

- URL: http://localhost:15000
- Aba **Traces**: fluxo distribuído completo (waterfall)
- Aba **Structured Logs**: logs correlacionados — buscar por TraceId
- Aba **Metrics**: métricas de runtime, HTTP e ASP.NET Core

---

<a id="telemetria-do-browser"></a>
## Telemetria do Browser

O arquivo `otel-browser.js` captura interações do usuário no browser e envia para o servidor via `POST /telemetry/spans`. O servidor reconstrói os spans como Activities vinculadas ao trace distribuído.

### O que é capturado

| Evento | Span name | Dados |
|--------|-----------|-------|
| Página carrega | `browser page_load` | URL, título, tempo de carga, DOM ready |
| Click em botão/link | `browser click` | Elemento, texto, href |
| Form submit | `browser form_submit` | Método, action, URL |

### Como funciona

1. O servidor injeta `<meta name="traceparent">` no HTML com o TraceId atual
2. O JS lê o traceparent e usa como parent context
3. Ao capturar um evento, envia via `sendBeacon` para `/telemetry/spans`
4. O `TelemetryController` reconstrói a Activity com o parent context original
5. O span aparece no Dashboard vinculado ao mesmo trace

---

<a id="telemetria-de-banco-de-dados"></a>
## Telemetria de Banco de Dados

O QuoteService usa raw SQL "procedures" que passam o `correlation_id` (trace_id + span_id) e capturam informações da sessão PostgreSQL.

### Informações capturadas pela procedure

| Tag OTel | Origem PostgreSQL | Exemplo |
|----------|-------------------|---------|
| `db.postgresql.pid` | `pg_backend_pid()` | 42 |
| `db.postgresql.transaction_id` | `txid_current()` | 12345 |
| `db.user` | `session_user` | postgres |
| `db.name` | `current_database()` | legacydb |
| `db.postgresql.application_name` | `current_setting('application_name')` | quote-service |
| `server.address` | `inet_server_addr()` | 172.17.0.2 |
| `server.port` | `inet_server_port()` | 5432 |

### Fluxo

1. A procedure executa INSERT/UPDATE + captura sessão PostgreSQL
2. Loga na tabela `db_operation_logs` com trace context e dados da sessão
3. O `DbTelemetryWorker` lê logs pendentes a cada 2s
4. Reconstrói spans com parent context original e exporta via OTLP
5. No Dashboard, `sp_create_quote` aparece dentro do trace do QuoteService

---

<a id="resource-detectors"></a>
## Resource Detectors (Container/Host)

Todos os serviços incluem resource attributes que aparecem em cada span:

| Atributo | Exemplo | Descrição |
|----------|---------|-----------|
| `host.name` | `3f8a2b1c9d4e` | Hostname do container |
| `os.description` | `Debian GNU/Linux 12` | Distro do OS |
| `os.type` | `linux` | Tipo do OS |
| `process.pid` | `1` | PID do processo |
| `process.runtime.name` | `.NET 10.0.0` | Runtime |
| `container.id` | `3f8a2b1c9d...` | ID do container Docker (Linux) |

---

<a id="casos-de-uso-do-workshop"></a>
## Casos de Uso do Workshop

### 1. Fluxo normal — Criar Cotação

1. Acesse http://localhost:15100 → Cotações → Nova Cotação
2. Preencha o formulário e clique **Criar Cotação**
3. No Dashboard → Traces: veja o fluxo completo browser → frontend → gateway → quote-service → banco

### 2. Fluxo normal — Aprovar Cotação

1. Na lista de cotações, clique **Aprovar** (botão verde)
2. No Dashboard → Traces: veja `sp_approve_quote` com dados da sessão PostgreSQL

### 3. Erro simulado na integração WCF — Aprovar com Erro

1. Na lista de cotações, clique **Aprovar com Erro** (botão vermelho)
2. O frontend exibe mensagem de erro com **CorrelationId**
3. No Dashboard → Traces: o span do `quote-service` aparece vermelho com:
   - `approve.simulate_error = true`
   - `error.simulated = true`
   - Status Error: "ERRO SIMULADO: Falha na integração WCF"
4. No Dashboard → Structured Logs: busque pelo CorrelationId para ver todos os logs correlacionados

### 4. Erro simulado na procedure do banco — Criar com Erro no Banco

1. Acesse Cotações → Nova Cotação
2. Preencha o formulário e clique **Criar com Erro no Banco** (botão vermelho)
3. A procedure executa o INSERT e depois provoca erro real do PostgreSQL
4. O INSERT é desfeito (rollback), mas o erro é logado na `db_operation_logs`
5. O `DbTelemetryWorker` captura e emite span com erro no tracing
6. No Dashboard → Traces: o span `sp_create_quote` aparece vermelho com:
   - `db.operation.details` contendo `simulated_error: true`
   - `ErrorMessage` com o erro real do PostgreSQL
   - Dados da sessão PostgreSQL (PID, transaction ID, server IP)
7. O frontend exibe **CorrelationId** na mensagem de erro

### 5. Troubleshooting com CorrelationId

1. Quando qualquer erro ocorre, o frontend exibe o **CorrelationId** (TraceId)
2. Copie o CorrelationId
3. No Dashboard → **Structured Logs** → filtre por TraceId
4. Veja todos os logs de todos os serviços correlacionados naquele trace
5. Clique no link do trace para ver o waterfall completo

### 6. Injeção de falhas via variáveis de ambiente

1. Pare o AppHost
2. Configure: `export FAULT_MODE=chaos` e `export FAULT_ERROR_RATE=0.3`
3. Reinicie: `dotnet run --project src/Legacy/Legacy.AppHost`
4. 30% das requisições aos serviços SOAP terão erros aleatórios
5. No Dashboard → Traces: spans vermelhos com `fault.type`, `fault.error_kind`

### 7. Observar telemetria do browser

1. Acesse qualquer página do frontend
2. No Dashboard → Traces: spans `browser page_load`, `browser click`, `browser form_submit`
3. Os spans do browser aparecem dentro do mesmo trace que o servidor

---

<a id="banco-de-dados-postgresql"></a>
## Banco de Dados (PostgreSQL)

O PostgreSQL é gerenciado pelo Aspire — sobe como container Docker automaticamente.

- **Senha fixa**: `workshop2026` (evita conflito com volume persistido)
- **Database**: `legacydb`
- **Volume**: dados persistidos entre execuções via `WithDataVolume()`
- **Schema**: criado automaticamente via `EnsureCreatedAsync()` no startup
- **Sequences**: resetadas após seeding para evitar conflito de PKs

Para resetar o banco:
```bash
docker volume rm $(docker volume ls -q | grep postgres)
```

---

<a id="dataset-mockado-e-seeding"></a>
## Dataset mockado e seeding

Princípios:
- Dados mockados, mas persistidos para comportamento previsível
- Dados relacionados e determinísticos
- IDs âncora sempre presentes para roteiros controlados

IDs âncora (sempre presentes):
- `customer_id = 999`
- `policy_id = 1234`
- `policy_number = "AUTO-1234"`

O seeding é executado no startup, é idempotente e só popula se o banco estiver vazio.

---

<a id="variaveis-de-ambiente"></a>
## Variáveis de Ambiente

Variáveis comuns a todos os serviços:

| Variável | Descrição |
|----------|-----------|
| `DATASET_SEED` | Seed do dataset (ex: `1001`) |
| `DATASET_PROFILE` | Perfil do dataset: `legacy` |

Variáveis para injeção de falhas:

| Variável | Valores / Descrição |
|----------|----------------------|
| `FAULT_MODE` | `off`, `delay`, `error`, `chaos` |
| `FAULT_DELAY_MS` | Delay artificial em ms (ex: `300`) |
| `FAULT_ERROR_RATE` | Taxa de erro (0.0 a 1.0) para modo `chaos` |
| `FAULT_ERROR_KIND` | `timeout`, `soapfault`, `http503` |

---

<a id="como-inicializar"></a>
## Como Inicializar

### Pré-requisitos

1. **.NET 10 SDK** — `dotnet --version` (10.x.x)
2. **.NET Aspire workload** — `dotnet workload install aspire`
3. **Docker** — necessário para o container PostgreSQL

### Executar

```bash
cd src/Legacy/Legacy.AppHost
dotnet run
```

**O que será iniciado:**
- PostgreSQL (container Docker) - porta dinâmica
- QuoteService (CoreWCF) - porta dinâmica
- PolicyService (CoreWCF) - porta dinâmica
- ClaimsService (CoreWCF) - porta dinâmica
- PricingRulesService (CoreWCF) - porta dinâmica
- Modern.Api (REST) - porta dinâmica
- Gateway YARP - **porta fixa 15100**
- Frontend MVC - porta dinâmica
- DbTelemetryWorker - background service
- Dashboard Aspire - porta 15000

**Acessar:**
- Dashboard Aspire: http://localhost:15000
- Frontend MVC: http://localhost:15100
- Endpoints SOAP: http://localhost:15100/QuoteService.svc
- API REST moderna: http://localhost:15100/api/quotes/customer/999

---

<a id="injecao-de-falhas-delay-e-caos"></a>
## Injeção de Falhas, Delay e Caos

Middleware global em ASP.NET Core que executa antes do CoreWCF. Gera spans no tracing com tags `fault.type`, `fault.delay_ms`, `fault.error_kind`.

Modos disponíveis:
- `delay`: simula lentidão de rede/processamento
- `error`: retorna erro consistente
- `chaos`: aplica erro aleatório conforme `FAULT_ERROR_RATE`

---

<a id="erros-simulados-via-interface"></a>
## Erros Simulados via Interface

O frontend oferece botões vermelhos para simular erros controlados, visíveis no tracing:

| Ação | Botão | Onde ocorre o erro | O que aparece no tracing |
|------|-------|--------------------|--------------------------|
| Aprovar cotação | **Aprovar com Erro** | Serviço WCF (FaultException) | Span vermelho no quote-service com `error.simulated=true` |
| Criar cotação | **Criar com Erro no Banco** | Procedure no PostgreSQL | Span vermelho `sp_create_quote` via worker com erro real do banco |

Ambos exibem o **CorrelationId** na mensagem de erro para troubleshooting.

---

<a id="troubleshooting-com-correlationid"></a>
## Troubleshooting com CorrelationId

Quando um erro ocorre, o frontend exibe o CorrelationId (TraceId do OpenTelemetry):

```
Erro simulado na aprovação da cotação QUOTE-ANCHOR-999.
CorrelationId: 4bf92f3577b34da6a3ce929d0e0e4736
Busque no Dashboard → Structured Logs → filtrar por TraceId
```

O CorrelationId permite:
1. **Buscar nos Structured Logs** — ver todos os logs de todos os serviços naquele trace
2. **Ver o Trace Detail** — waterfall com todos os spans do fluxo completo
3. **Identificar onde o erro ocorreu** — spans vermelhos indicam a origem
4. **Compartilhar com a equipe** — o ID identifica univocamente a operação que falhou

---

## Padrões de Estrangulamento de Legado

### 1. Strangler Fig Pattern

O padrão clássico de migração incremental. O `Modern.Api` (REST) coexiste com os serviços SOAP legados. O Gateway roteia gradualmente endpoints para o serviço moderno.

**Roteamento no Gateway:**

```
/api/quotes/*              → Modern.Api (REST)     ← NOVO
/QuoteService.svc/*        → Legacy QuoteService   ← LEGADO (ainda funciona)
/PolicyService.svc/*       → Legacy PolicyService
/ClaimsService.svc/*       → Legacy ClaimsService
/PricingRulesService.svc/* → Legacy PricingRulesService
/*                         → Frontend MVC
```

**Endpoints REST disponíveis:**

| Método | Endpoint | Descrição | Substitui |
|--------|----------|-----------|-----------|
| GET | `/api/quotes/customer/{id}` | Listar cotações por cliente | GetQuotesByCustomer (SOAP) |
| GET | `/api/quotes/{quoteNumber}` | Consultar cotação individual | Não existia no SOAP |

**Testar:**
```bash
# REST moderno (via gateway)
curl http://localhost:15100/api/quotes/customer/999

# SOAP legado (ainda funciona em paralelo)
curl -X POST http://localhost:15100/QuoteService.svc \
  -H "Content-Type: text/xml" \
  -H "SOAPAction: http://eximia.co/seguroauto/legacy/IQuoteService/GetQuotesByCustomer" \
  -d '...'
```

**No Dashboard:** os traces mostram claramente qual caminho foi usado — Modern.Api ou QuoteService SOAP — permitindo comparar performance e comportamento lado a lado.

### 2. CQRS (Command Query Responsibility Segregation)

Separação de responsabilidade entre leituras e escritas. As **queries** (leitura) são servidas pelo Modern.Api REST, enquanto os **commands** (escrita: criar cotação, aprovar, criar apólice, criar sinistro) continuam no legado SOAP.

**Separação no Gateway:**

```
QUERIES (leitura) → Modern.Api REST
  GET /api/quotes/customer/{id}
  GET /api/quotes/{quoteNumber}
  GET /api/policies/customer/{id}
  GET /api/policies/{policyNumber}
  GET /api/claims/policy/{policyNumber}

COMMANDS (escrita) → Legacy SOAP
  POST /QuoteService.svc    → Criar cotação, Aprovar cotação
  POST /PolicyService.svc   → Criar apólice
  POST /ClaimsService.svc   → Criar sinistro
```

**Benefícios demonstrados:**
- Leituras modernas (JSON, async, EF Core LINQ) sem tocar nas escritas
- Escritas legadas continuam estáveis (contratos SOAP inalterados)
- Mesmo banco de dados compartilhado (PostgreSQL)
- Evolução gradual sem risco de quebrar escritas críticas

**Testar queries REST:**
```bash
curl http://localhost:15100/api/quotes/customer/999
curl http://localhost:15100/api/policies/customer/999
curl http://localhost:15100/api/policies/AUTO-1234
curl http://localhost:15100/api/claims/policy/AUTO-1234
```
