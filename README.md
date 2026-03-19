# Seguro Auto - Workshop de Arquitetura

Este repositório contém material didático e técnico para um workshop de arquitetura, utilizando um sistema legado no domínio de Seguro para Automóveis.

Resumo rápido
- Produto: Seguro para Automóveis
- Objetivo: Workshop de arquitetura com foco em observabilidade e sistemas legados
- Premissas: baixo atrito, execução local, cenários controlados, dados consistentes

Tecnologias principais
- CoreWCF (compatibilidade com SOAP em .NET moderno)
- .NET Aspire (orquestração local e observabilidade)
- OpenTelemetry (tracing distribuído)
- SQLite (persistência leve, zero-setup)

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
- [Estratégia de Banco de Dados (SQLite)](#estrategia-de-banco-de-dados-sqlite)
- [Dataset mockado e seeding](#dataset-mockado-e-seeding)
- [Variáveis de Ambiente](#variaveis-de-ambiente)
- [Como Inicializar](#como-inicializar)
- [Injeção de Falhas, Delay e Caos](#injecao-de-falhas-delay-e-caos)

---

<a id="objetivo-do-projeto"></a>
## Objetivo do Projeto

Este projeto foi criado para simular, de forma controlada e didática, um ambiente típico de legado corporativo em uma seguradora, especificamente no domínio de Seguro para Automóveis. O objetivo é permitir:

- Executar serviços WCF (SOAP) em ambiente moderno
- Demonstrar problemas clássicos de sistemas legados
- Demonstrar observabilidade distribuída com OpenTelemetry e Aspire
- Demonstrar telemetria de banco de dados com correlation_id
- Orquestrar cenários com .NET Aspire
- Trabalhar com dados persistidos, relacionados e previsíveis
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

Racional pedagógico: CoreWCF não é o WCF Framework original, mas é suficientemente semelhante para ensinar acoplamento, contratos rígidos e limitações operacionais.

### Redução de fricção (princípio central)

O projeto foi projetado para reduzir barreiras ao laboratório:

- Não depender de Windows
- Não exigir IIS
- Não exigir SQL Server
- Rodar igual em macOS / Linux / Windows
- Ter um comando para executar (fluxo simples de execução)

---

<a id="orquestracao-com-net-aspire"></a>
## Orquestração com .NET Aspire

O .NET Aspire é usado como plataforma de orquestração local. Responsabilidades principais:

- Subir múltiplos serviços simultaneamente
- Gerenciar dependências entre serviços
- Injetar variáveis de ambiente por cenário
- Fornecer observabilidade (logs, traces, métricas) via dashboard
- Injetar automaticamente `OTEL_EXPORTER_OTLP_ENDPOINT` nos serviços

---

<a id="estrutura-do-projeto"></a>
## Estrutura do Projeto

```
src/
  Shared/
    SeguroAuto.Domain/          # Entidades de domínio
    SeguroAuto.Data/            # DbContext, seeding, DbOperationLog
    SeguroAuto.Common/          # Utilitários compartilhados
    SeguroAuto.FaultInjection/  # Middleware de injeção de falhas
    SeguroAuto.ServiceDefaults/ # OpenTelemetry + health checks
  Legacy/
    Legacy.QuoteService/        # Serviço SOAP de cotações
    Legacy.PolicyService/       # Serviço SOAP de apólices
    Legacy.ClaimsService/       # Serviço SOAP de sinistros
    Legacy.PricingRulesService/ # Serviço SOAP de regras de precificação
    Legacy.Gateway/             # YARP reverse proxy
    Legacy.DbTelemetryWorker/   # Worker que exporta telemetria do banco
    Legacy.AppHost/             # Aspire AppHost (orquestração)
    Legacy.TestClient/          # Cliente de teste
  Frontend/
    SeguroAuto.Web/             # Frontend MVC
```

---

<a id="observabilidade"></a>
## Observabilidade

O projeto implementa tracing distribuído end-to-end usando OpenTelemetry com exportação OTLP para o Aspire Dashboard.

### Fluxo do trace

```
Browser → Frontend MVC → Gateway YARP → Serviço SOAP → Banco de Dados
```

Cada componente gera spans que são correlacionados automaticamente via W3C traceparent:

- **Frontend**: spans de requisição ASP.NET Core + spans customizados nos SOAP clients (`SeguroAuto.Web.SoapClient`)
- **Gateway**: spans do YARP reverse proxy
- **Serviços SOAP**: spans de requisição ASP.NET Core + spans do FaultInjection middleware (`SeguroAuto.FaultInjection`)
- **Banco de dados**: spans reconstruídos pelo `DbTelemetryWorker` a partir da tabela `db_operation_logs` (`SeguroAuto.Database`)

### Telemetria de banco de dados

O QuoteService usa raw SQL "procedures" que passam o `correlation_id` (trace_id + span_id):

1. A "procedure" executa a operação de negócio (INSERT/UPDATE)
2. Loga a operação na tabela `db_operation_logs` com o trace context
3. O `DbTelemetryWorker` lê os logs pendentes a cada 2s
4. Reconstrói spans com o parent context original e exporta via OTLP

No Dashboard, o span `sp_create_quote` aparece **dentro** do trace do QuoteService, demonstrando correlação entre aplicação e banco.

### Dashboard

- URL: http://localhost:15000
- OTLP endpoint: http://localhost:15001
- Aba **Traces**: visualizar fluxo distribuído completo
- Aba **Structured Logs**: logs correlacionados com traces

---

<a id="estrategia-de-banco-de-dados-sqlite"></a>
## Estratégia de Banco de Dados (SQLite)

Por que SQLite?
- Zero setup
- Arquivo local, transportável
- Funciona em containers Linux
- Ideal para laboratório e treinamento

Arquivo de banco: `data/legacy.db`

O caminho do arquivo de banco é injetado via variável de ambiente (`DB_PATH`).

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
| `DB_PATH` | Caminho do arquivo `.db` (ex: `/data/legacy.db`) |
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

1. **.NET 10 SDK instalado**
   - Verifique: `dotnet --version` (deve mostrar 10.x.x)

2. **.NET Aspire workload instalado**
   ```bash
   dotnet workload install aspire
   ```

### Executar

```bash
# Navegue até o diretório do AppHost
cd src/Legacy/Legacy.AppHost

# Execute
dotnet run
```

**O que será iniciado:**
- QuoteService (CoreWCF) - porta dinâmica
- PolicyService (CoreWCF) - porta dinâmica
- ClaimsService (CoreWCF) - porta dinâmica
- PricingRulesService (CoreWCF) - porta dinâmica
- Gateway YARP - **porta fixa 15100**
- Frontend MVC - porta dinâmica
- DbTelemetryWorker - background service
- Dashboard Aspire - porta 15000

**Acessar serviços:**
- Dashboard Aspire: http://localhost:15000
- Gateway: http://localhost:15100
- Frontend MVC: http://localhost:15100 (via gateway)
- Endpoints SOAP: http://localhost:15100/QuoteService.svc (via gateway)

**Testar serviços:**
```bash
# Acessar o Frontend MVC no navegador
open http://localhost:15100

# Ou usar curl para SOAP
curl -X POST http://localhost:15100/QuoteService.svc \
  -H "Content-Type: text/xml; charset=utf-8" \
  -H "SOAPAction: http://eximia.co/seguroauto/legacy/IQuoteService/GetQuote" \
  -d @soap-request.xml
```

### Solução de Problemas

**Erro: NETSDK1147 - workload aspire não instalada**
```bash
dotnet workload install aspire
```

**Verificar workload:**
```bash
dotnet workload list
```

### Notas Importantes

1. **Porta fixa do Gateway**: 15100 (facilita testes e arquivos .http)
2. **Demais serviços**: portas dinâmicas atribuídas pelo Aspire
3. **Dashboard**: http://localhost:15000
4. **Seeding automático**: idempotente, só popula se o banco estiver vazio
5. **IDs âncora**: Customer 999, Policy 1234, Policy Number "AUTO-1234"

---

<a id="injecao-de-falhas-delay-e-caos"></a>
## Injeção de Falhas, Delay e Caos

Objetivo: permitir que o instrutor demonstre problemas reais, controle o impacto e ative/desative falhas sem alterar código.

Como funciona:
- Middleware global em ASP.NET Core que executa antes do CoreWCF
- Lê as variáveis de ambiente listadas acima
- Decide se atrasa, falha ou deixa o pedido passar
- Gera spans no tracing com tags `fault.type`, `fault.delay_ms`, `fault.error_kind`

Modos disponíveis:
- `delay`: simula lentidão de rede/processamento
- `error`: retorna erro consistente
- `chaos`: aplica erro aleatório conforme `FAULT_ERROR_RATE`
