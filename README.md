# Modernização de Legado - Seguro Auto

Este repositório contém material didático e técnico para um treinamento de modernização de um sistema legado no domínio de Seguro para Automóveis.

Resumo rápido
- Produto: Seguro para Automóveis
- Objetivo: Treinamento de modernização de legado (WCF → arquitetura moderna)
- Premissas: baixo atrito, execução local, cenários controlados, dados consistentes

Tecnologias principais
- CoreWCF (compatibilidade com SOAP em .NET moderno)
- .NET Aspire (orquestração local dos demos)
- SQLite (persistência leve, zero-setup)

---

Sumário
- [Modernização de Legado - Seguro Auto](#modernização-de-legado---seguro-auto)
  - [Objetivo do Projeto](#objetivo-do-projeto)
  - [Racional da Tecnologia](#racional-da-tecnologia)
    - [Por que CoreWCF?](#por-que-corewcf)
    - [Redução de fricção (princípio central)](#redução-de-fricção-princípio-central)
  - [Orquestracao com .NET Aspire](#orquestracao-com-net-aspire)
  - [Estrutura de Demos](#estrutura-de-demos)
    - [Demo 1: Legacy](#demo-1-legacy)
    - [Demo 2: Modernization](#demo-2-modernization)
    - [Demo 3: Lab](#demo-3-lab)
  - [Estrategia de Banco de Dados (SQLite)](#estrategia-de-banco-de-dados-sqlite)
  - [Dataset mockado e seeding](#dataset-mockado-e-seeding)
  - [Variaveis de Ambiente](#variaveis-de-ambiente)
  - [Como Inicializar os Demos](#como-inicializar-os-demos)
    - [Pré-requisitos](#pré-requisitos)
    - [Demo 1: Legacy](#demo-1-legacy-1)
    - [Demo 2: Modernization](#demo-2-modernization-1)
    - [Demo 3: Lab](#demo-3-lab-1)
    - [Solução de Problemas](#solução-de-problemas)
    - [Notas Importantes](#notas-importantes)
  - [Injecao de Falhas, Delay e Caos](#injecao-de-falhas-delay-e-caos)

---

<a id="objetivo-do-projeto"></a>
## Objetivo do Projeto

Este projeto foi criado para simular, de forma controlada e didática, um ambiente típico de legado corporativo em uma seguradora, especificamente no domínio de Seguro para Automóveis. O objetivo é permitir:

- Executar serviços WCF (SOAP) em ambiente moderno
- Demonstrar problemas clássicos de sistemas legados
- Evoluir gradualmente para uma arquitetura moderna
- Orquestrar cenários com .NET Aspire
- Executar demos distintas e um laboratório incremental
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
- Ter um comando por demo (fluxo simples de execução)

---

<a id="orquestracao-com-net-aspire"></a>
## Orquestracao com .NET Aspire

O .NET Aspire é usado como plataforma de orquestração local. Responsabilidades principais:

- Subir múltiplos serviços simultaneamente
- Gerenciar dependências entre serviços
- Injetar variáveis de ambiente por cenário
- Fornecer observabilidade (logs, traces, métricas)
- Isolar cenários por demo (cada demo tem seu AppHost)

---

<a id="estrutura-de-demos"></a>
## Estrutura de Demos

O projeto possui três demos, cada uma com um propósito pedagógico distinto.

### Demo 1: Legacy

Objetivo: Mostrar o cenário legado puro, com todos os problemas explícitos.

Componentes:
- Serviços CoreWCF: QuoteService, PolicyService, ClaimsService, PricingRulesService
- Banco SQLite exclusivo: `legacy.db`
- Cliente de teste simples
- Falhas e delays habilitáveis via variáveis de ambiente

Uso no treinamento: demonstrar SOAP, contratos rígidos, latência, impacto operacional e observabilidade limitada.

### Demo 2: Modernization

Objetivo: Mostrar um estado evoluído, com convivência entre legado e moderno.

Componentes:
- Todos os serviços do Legacy
- `Modern.Api` (REST)
- Strangler Gateway (opcional)
- Banco SQLite exclusivo: `modernization.db`
- Falhas desligadas por padrão

Uso no treinamento: demonstrar Strangler Fig Pattern, extração incremental e coexistência legado + moderno.

### Demo 3: Lab

Objetivo: Hands-on para evolução incremental durante o treinamento.

Componentes:
- Todos os serviços (legado + moderno)
- Gateway com feature flags
- Banco SQLite exclusivo: `lab.db`
- Falhas, delays e caos controláveis em runtime

Uso no treinamento: permitir "virar a chave", introduzir falhas propositalmente e discutir resiliência e observabilidade.

---

<a id="estrategia-de-banco-de-dados-sqlite"></a>
## Estrategia de Banco de Dados (SQLite)

Por que SQLite?
- Zero setup
- Arquivo local, transportável
- Funciona em containers Linux
- Ideal para laboratório e treinamento

Um banco por demo (isolamento):

| Demo | Arquivo DB |
|------|------------|
| Legacy | `legacy.db` |
| Modernization | `modernization.db` |
| Lab | `lab.db` |

O caminho do arquivo de banco é sempre injetado via variável de ambiente (`DB_PATH`).

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

Estratégia de seed (fixa por demo):

| Demo | SEED |
|------|------|
| Legacy | 1001 |
| Modernization | 2002 |
| Lab | 3003 |

O seeding é executado no startup, é idempotente e só roda se o banco não existir.

---

<a id="variaveis-de-ambiente"></a>
## Variaveis de Ambiente

Variáveis comuns a todos os serviços:

| Variável | Descrição |
|----------|-----------|
| `DB_PROVIDER` | Sempre `sqlite` |
| `DB_PATH` | Caminho do arquivo `.db` (ex: `/data/legacy.db`) |
| `DATASET_SEED` | Seed do dataset (ex: `1001`) |
| `DATASET_PROFILE` | Perfil do dataset: `legacy`, `modern`, `lab` |

Variáveis para injeção de falhas (Demo Legacy e Lab):

| Variável | Valores / Descrição |
|----------|----------------------|
| `FAULT_MODE` | `off`, `delay`, `error`, `chaos` |
| `FAULT_DELAY_MS` | Delay artificial em ms (ex: `300`) |
| `FAULT_ERROR_RATE` | Taxa de erro (0.0 a 1.0) para modo `chaos` |
| `FAULT_ERROR_KIND` | `timeout`, `soapfault`, `http503` |

Exemplo rápido de configuração (bash / zsh):

```bash
export DATASET_PROFILE=legacy
export DATASET_SEED=1001
export DB_PROVIDER=sqlite
export DB_PATH=./data/legacy.db
export FAULT_MODE=delay
export FAULT_DELAY_MS=300
```

> Observação: os valores exatos e o script de inicialização dos AppHosts variam por demo - consulte a seção [Como Inicializar os Demos](#como-inicializar-os-demos) abaixo.

---

<a id="como-inicializar-os-demos"></a>
## Como Inicializar os Demos

Cada demo possui seu próprio AppHost do .NET Aspire que orquestra todos os serviços necessários. Os demos são independentes e podem ser executados separadamente.

### Pré-requisitos

1. **.NET 9 SDK instalado**
   - Verifique se está instalado: `dotnet --version` (deve mostrar 9.x.x)
   - Se não estiver instalado, baixe em: https://dotnet.microsoft.com/download/dotnet/9.0

2. **.NET Aspire workload instalado**
   - **IMPORTANTE**: A workload do Aspire deve ser instalada antes de executar os demos
   - Execute o comando:
     ```bash
     dotnet workload install aspire
     ```
   - Aguarde a instalação completar (pode levar alguns minutos)
   - Verifique se foi instalada corretamente:
     ```bash
     dotnet workload list
     ```
     Você deve ver `aspire` na lista de workloads instaladas

3. **Terminal/Command Prompt**
   - Use bash/zsh no macOS/Linux ou PowerShell/CMD no Windows

<a id="demo-1-legacy-inicializacao"></a>
### Demo 1: Legacy

O demo Legacy demonstra o cenário legado puro com serviços CoreWCF (SOAP).

**Como inicializar:**

```bash
# 1. Certifique-se de que a workload do Aspire está instalada
dotnet workload install aspire

# 2. Navegue até o diretório do AppHost
cd src/Legacy/Legacy.AppHost

# 3. Execute o AppHost
dotnet run
```

> **Nota**: Se você receber o erro `NETSDK1147` indicando que a workload aspire não está instalada, execute `dotnet workload install aspire` e tente novamente.

**O que será iniciado:**
- QuoteService (CoreWCF) - porta configurada pelo Aspire
- PolicyService (CoreWCF) - porta configurada pelo Aspire
- ClaimsService (CoreWCF) - porta configurada pelo Aspire
- PricingRulesService (CoreWCF) - porta configurada pelo Aspire
- Gateway YARP - **porta fixa 15100** (facilita testes e arquivos .http)
- Frontend MVC (ASP.NET Core) - porta configurada pelo Aspire
- Dashboard do .NET Aspire com observabilidade (porta 15000)

**Configuração padrão:**
- Banco de dados: `./data/legacy.db`
- Dataset seed: `1001`
- Dataset profile: `legacy`
- Fault mode: `delay` (300ms)

**Acessar serviços:**
- Dashboard Aspire: http://localhost:15000 (será exibido no terminal após iniciar)
- **Gateway: http://localhost:15100** (porta fixa - use nos arquivos .http)
- Frontend MVC: http://localhost:15100 (através do gateway)
- Endpoints SOAP: http://localhost:15100/QuoteService.svc (através do gateway)

**Testar serviços:**
```bash
# Usar arquivo .http (recomendado)
# O arquivo Legacy-Services.http já está configurado com a porta 15100
# Execute as requisições diretamente do VS Code ou Rider

# Ou usar curl diretamente
curl -X POST http://localhost:15100/QuoteService.svc \
  -H "Content-Type: text/xml; charset=utf-8" \
  -H "SOAPAction: http://eximia.co/seguroauto/legacy/IQuoteService/GetQuote" \
  -d @soap-request.xml

# Ou acessar o Frontend MVC
# Abra http://localhost:15100 no navegador
```

---

<a id="demo-2-modernization-inicializacao"></a>
### Demo 2: Modernization

O demo Modernization demonstra a coexistência entre serviços legados (SOAP) e modernos (REST).

**Como inicializar:**

```bash
# 1. Certifique-se de que a workload do Aspire está instalada
dotnet workload install aspire

# 2. Navegue até o diretório do AppHost
cd src/Modernization/Modernization.AppHost

# 3. Execute o AppHost
dotnet run
```

> **Nota**: Se você receber o erro `NETSDK1147` indicando que a workload aspire não está instalada, execute `dotnet workload install aspire` e tente novamente.

**O que será iniciado:**
- Modern.Api (REST) - porta configurada pelo Aspire
- Legacy.QuoteService (CoreWCF) - porta configurada pelo Aspire
- Legacy.PolicyService (CoreWCF) - porta configurada pelo Aspire
- Modern.Gateway (YARP) - porta configurada pelo Aspire
- Dashboard do .NET Aspire com observabilidade (porta 15010)

**Configuração padrão:**
- Banco de dados: `./data/modernization.db`
- Dataset seed: `2002`
- Dataset profile: `modern`
- Fault mode: `off` (sem falhas)

**Acessar serviços:**
- Dashboard Aspire: http://localhost:15010 (será exibido no terminal após iniciar)
- Gateway: roteia `/api/*` para Modern.Api e `/legacy/*` para serviços Legacy
- REST API: endpoints em `/api/quotes`, `/api/policies`
- SOAP Services: disponíveis através do gateway ou diretamente

**Testar:**
```bash
# REST API
curl http://localhost:<PORT>/api/quotes/customer/999

# Gateway (roteia para Modern.Api)
curl http://localhost:<GATEWAY_PORT>/api/quotes/customer/999
```

---

<a id="demo-3-lab-inicializacao"></a>
### Demo 3: Lab

O demo Lab é para hands-on durante o treinamento, com feature flags e controle de falhas em runtime.

**Como inicializar:**

```bash
# 1. Certifique-se de que a workload do Aspire está instalada
dotnet workload install aspire

# 2. Navegue até o diretório do AppHost
cd src/Lab/Lab.AppHost

# 3. Execute o AppHost
dotnet run
```

> **Nota**: Se você receber o erro `NETSDK1147` indicando que a workload aspire não está instalada, execute `dotnet workload install aspire` e tente novamente.

**O que será iniciado:**
- Modern.Api (REST) - porta configurada pelo Aspire
- Legacy.QuoteService (CoreWCF) - porta configurada pelo Aspire
- Legacy.PolicyService (CoreWCF) - porta configurada pelo Aspire
- Lab.Gateway (YARP com feature flags) - porta configurada pelo Aspire
- Dashboard do .NET Aspire com observabilidade (porta 15020)

**Configuração padrão:**
- Banco de dados: `./data/lab.db`
- Dataset seed: `3003`
- Dataset profile: `lab`
- Fault mode: `chaos` (10% de taxa de erro)
- Feature flag: `USE_MODERN_API=false` (usa Legacy por padrão)

**Acessar serviços:**
- Dashboard Aspire: http://localhost:15020 (será exibido no terminal após iniciar)
- Gateway: alterna entre Legacy e Modern via feature flag `USE_MODERN_API`
- REST API: disponível diretamente ou através do gateway
- SOAP Services: disponíveis diretamente ou através do gateway

**Controle de feature flags:**
O gateway verifica a variável de ambiente `USE_MODERN_API`:
- `USE_MODERN_API=true`: roteia para Modern.Api (REST)
- `USE_MODERN_API=false`: roteia para Legacy Services (SOAP)

**Testar com feature flag:**
```bash
# Alterar feature flag (requer reiniciar o AppHost)
export USE_MODERN_API=true
cd src/Lab/Lab.AppHost
dotnet run
```

**Controle de falhas:**
```bash
# Configurar modo de falha antes de iniciar
export FAULT_MODE=chaos
export FAULT_ERROR_RATE=0.2  # 20% de taxa de erro
cd src/Lab/Lab.AppHost
dotnet run
```

---

### Solução de Problemas

**Erro: NETSDK1147 - workload aspire não instalada**

Se você receber este erro:
```
error NETSDK1147: Para criar este projeto, as seguintes cargas de trabalho devem ser instaladas: aspire
```

Execute:
```bash
dotnet workload install aspire
```

Aguarde a instalação completar e tente novamente.

**Verificar se a workload está instalada:**
```bash
dotnet workload list
```

**Se a instalação falhar:**
- Certifique-se de que está usando .NET 9 SDK
- Tente atualizar a workload: `dotnet workload update`
- Em alguns casos, pode ser necessário executar: `dotnet workload restore`

### Notas Importantes

1. **Isolamento de bancos de dados**: Cada demo usa seu próprio arquivo SQLite isolado (`legacy.db`, `modernization.db`, `lab.db`)

2. **Portas dinâmicas e fixas**: 
   - **Gateway Legacy**: Porta fixa **15100** (facilita testes e arquivos .http)
   - **Demais serviços**: Portas dinâmicas atribuídas pelo Aspire
   - **Dashboard**: Portas fixas por demo:
     - Legacy: http://localhost:15000
     - Modernization: http://localhost:15010
     - Lab: http://localhost:15020

3. **Seeding automático**: Cada serviço executa seeding do banco na inicialização (idempotente - só cria se o banco não existir).

4. **IDs âncora**: Todos os demos incluem os mesmos IDs âncora para facilitar testes:
   - Customer ID: `999`
   - Policy ID: `1234`
   - Policy Number: `"AUTO-1234"`

5. **Executar múltiplos demos simultaneamente**: Agora é possível executar múltiplos demos simultaneamente, pois cada um usa portas diferentes para o dashboard. Apenas certifique-se de que não há outros processos usando as portas 15000, 15010 ou 15020.

---

<a id="injecao-de-falhas-delay-e-caos"></a>
## Injecao de Falhas, Delay e Caos

Objetivo: permitir que o instrutor demonstre problemas reais, controle o impacto e ative/desative falhas sem alterar código.

Como funciona:
- Middleware global em ASP.NET Core que executa antes do CoreWCF
- Lê as variáveis de ambiente listadas acima
- Decide se atrasa, falha ou deixa o pedido passar

Modos disponíveis:
- `delay`: simula lentidão de rede/processamento
- `error`: retorna erro consistente
- `chaos`: aplica erro aleatório conforme `FAULT_ERROR_RATE`

Uso didático:
- Legacy: delays e falhas ativáveis
- Modernization: comportamento estável por padrão
- Lab: ativar falha durante o treino para exercitar resiliência
