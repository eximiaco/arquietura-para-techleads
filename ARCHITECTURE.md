# Arquitetura do Sistema - Seguro Auto

Este documento descreve em detalhes a arquitetura, estratégias e decisões técnicas do projeto de modernização de legado para o domínio de Seguro para Automóveis.

---

## Sumário

- [Visão Geral da Arquitetura](#visão-geral-da-arquitetura)
- [Estratégia de Criação dos Serviços WCF](#estratégia-de-criação-dos-serviços-wcf)
- [Arquitetura em Camadas](#arquitetura-em-camadas)
- [Estratégias de Uso do SQLite](#estratégias-de-uso-do-sqlite)
- [Service Discovery e Gateway](#service-discovery-e-gateway)
- [Fault Injection e Resiliência](#fault-injection-e-resiliência)
- [Padrões de Modernização](#padrões-de-modernização)
- [Orquestração com .NET Aspire](#orquestração-com-net-aspire)
- [Ciclo de Vida e Dependency Injection](#ciclo-de-vida-e-dependency-injection)
- [Observabilidade e Logging](#observabilidade-e-logging)

---

## Visão Geral da Arquitetura

O projeto implementa uma arquitetura de modernização incremental, demonstrando a evolução de um sistema legado baseado em WCF/SOAP para uma arquitetura moderna baseada em REST/HTTP.

### Princípios Arquiteturais

1. **Redução de Fricção**: Zero dependências de Windows, IIS ou SQL Server
2. **Isolamento por Demo**: Cada demo possui seu próprio banco de dados e configuração
3. **Coexistência Gradual**: Legado e moderno convivem durante a transição
4. **Observabilidade Integrada**: Logs, traces e métricas através do Aspire Dashboard
5. **Dados Determinísticos**: Seeding idempotente com IDs âncora para testes consistentes

### Stack Tecnológico

- **.NET 9**: Framework base para todos os projetos
- **CoreWCF**: Compatibilidade com WCF/SOAP em .NET moderno
- **ASP.NET Core**: Host para serviços REST e CoreWCF
- **Entity Framework Core**: ORM para acesso a dados
- **SQLite**: Banco de dados leve e portável
- **.NET Aspire**: Orquestração e observabilidade
- **YARP**: Proxy reverso para gateways

---

## Estratégia de Criação dos Serviços WCF

### Por que CoreWCF?

O WCF clássico não é suportado em .NET moderno nem em Linux. O **CoreWCF** é um port do modelo de programação WCF para ASP.NET Core, mantendo:

- Atributos `[ServiceContract]` e `[OperationContract]`
- Suporte a SOAP e bindings como `BasicHttpBinding`
- Compatibilidade com contratos WCF existentes
- Execução em Linux, containers e .NET moderno

### Estrutura de um Serviço WCF

Cada serviço Legacy segue uma estrutura padronizada:

#### 1. Interface do Serviço (`IService.cs`)

Define o contrato SOAP usando atributos WCF:

```csharp
[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IQuoteService
{
    [OperationContract]
    QuoteResponse GetQuote(QuoteRequest request);
    
    [OperationContract]
    GetQuotesByCustomerResponse GetQuotesByCustomer(GetQuotesByCustomerRequest request);
}
```

**Características importantes:**
- **Namespace SOAP**: Define o namespace XML para o contrato
- **Message Contracts**: Todos os parâmetros e retornos devem ser `[MessageContract]`
- **Não misturar tipos**: Não é possível misturar `[MessageContract]` com tipos primitivos na mesma operação

#### 2. Message Contracts

Cada operação usa classes marcadas com `[MessageContract]`:

```csharp
[MessageContract]
public class QuoteRequest
{
    [MessageBodyMember]
    public int CustomerId { get; set; }
    
    [MessageBodyMember]
    public string VehiclePlate { get; set; } = string.Empty;
}
```

**Regra crítica**: Quando uma operação usa `[MessageContract]`, **todos** os parâmetros e o tipo de retorno devem ser `[MessageContract]`. Não é possível misturar com tipos primitivos.

#### 3. Implementação do Serviço (`Service.cs`)

A implementação herda da interface e é registrada no DI container:

```csharp
public class QuoteService : IQuoteService
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<QuoteService> _logger;

    public QuoteService(SeguroAutoDbContext context, ILogger<QuoteService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public QuoteResponse GetQuote(QuoteRequest request)
    {
        // Implementação...
    }
}
```

#### 4. Configuração no `Program.cs`

```csharp
// 1. Registrar o serviço no DI como Transient
builder.Services.AddTransient<QuoteService>();

// 2. Configurar CoreWCF
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

// 3. Habilitar detalhes de exceção em SOAP faults (desenvolvimento)
builder.Services.AddSingleton<IServiceBehavior>(sp => new ServiceDebugBehavior
{
    IncludeExceptionDetailInFaults = true,
    HttpHelpPageEnabled = true
});

// 4. Configurar endpoint SOAP
app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder
        .AddService<QuoteService>()
        .AddServiceEndpoint<QuoteService, IQuoteService>(
            new BasicHttpBinding(),
            "/QuoteService.svc");
});
```

### Serviços Implementados

O projeto contém 4 serviços Legacy:

1. **QuoteService**: Criação e consulta de cotações de seguro
2. **PolicyService**: Gerenciamento de apólices de seguro
3. **ClaimsService**: Gerenciamento de sinistros
4. **PricingRulesService**: Regras de precificação

### Tratamento de Exceções

Todos os serviços implementam tratamento de exceções consistente:

```csharp
try
{
    // Lógica do serviço
    _logger.LogInformation("Operação executada com sucesso");
    return response;
}
catch (FaultException)
{
    throw; // Re-throw FaultException as-is (já é um SOAP fault)
}
catch (Exception ex)
{
    _logger.LogError(ex, "Erro na operação");
    throw new FaultException($"Erro: {ex.Message}");
}
```

---

## Arquitetura em Camadas

O projeto segue uma arquitetura em camadas com separação clara de responsabilidades:

### Camada de Domínio (`SeguroAuto.Domain`)

Contém as entidades de negócio puras, sem dependências de infraestrutura:

- `Customer`: Cliente da seguradora
- `Policy`: Apólice de seguro
- `Claim`: Sinistro
- `Quote`: Cotação de seguro
- `PricingRule`: Regra de precificação

**Características:**
- Classes POCO (Plain Old CLR Objects)
- Sem atributos de ORM
- Sem lógica de persistência
- Representam o modelo de domínio

### Camada de Dados (`SeguroAuto.Data`)

Responsável pela persistência e acesso a dados:

#### `SeguroAutoDbContext`

DbContext do Entity Framework Core que:
- Define os `DbSet` para cada entidade
- Configura relacionamentos e índices
- Mapeia entidades para tabelas SQLite

```csharp
public class SeguroAutoDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Policy> Policies { get; set; }
    public DbSet<Claim> Claims { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<PricingRule> PricingRules { get; set; }
}
```

#### `DatabaseSeeder`

Responsável pelo seeding inicial do banco de dados:

- **Idempotente**: Só executa se o banco estiver vazio
- **Determinístico**: Usa seed fixo para gerar dados consistentes
- **IDs Âncora**: Sempre cria customer 999, policy 1234, etc.
- **Perfis**: Diferentes perfis de dados por demo (`legacy`, `modern`, `lab`)

#### `ServiceCollectionExtensions`

Métodos de extensão para configuração:

- `AddSeguroAutoData()`: Configura o DbContext com SQLite
- `SeedDatabaseAsync()`: Executa o seeding do banco

### Camada de Aplicação (Serviços Legacy)

Cada serviço Legacy (`Legacy.*Service`) implementa:

- **Lógica de negócio**: Regras de cálculo, validações
- **Acesso a dados**: Através do `SeguroAutoDbContext`
- **Contratos SOAP**: Implementação dos contratos WCF
- **Logging**: Registro de operações e erros

### Camada de Apresentação

#### Serviços Legacy (CoreWCF)
- Endpoints SOAP em `/ServiceName.svc`
- Respostas em formato XML/SOAP

#### Modern.Api (REST)
- Controllers ASP.NET Core
- Endpoints REST em `/api/*`
- Respostas em JSON

#### Gateways (YARP)
- Proxy reverso para roteamento
- Service discovery automático
- Feature flags (Lab.Gateway)

### Camada de Infraestrutura Compartilhada

#### `SeguroAuto.FaultInjection`

Middleware para injeção de falhas controladas:

- **Delay**: Simula latência de rede
- **Error**: Retorna erros consistentes
- **Chaos**: Aplica erros aleatórios conforme taxa configurada

#### `SeguroAuto.Common`

Utilitários compartilhados (estrutura preparada para expansão)

---

## Estratégias de Uso do SQLite

### Por que SQLite?

1. **Zero Setup**: Não requer instalação de servidor de banco
2. **Portabilidade**: Arquivo único, fácil de transportar
3. **Cross-Platform**: Funciona em Windows, macOS e Linux
4. **Ideal para Demos**: Rápido, leve, suficiente para cenários de treinamento

### Isolamento por Demo

Cada demo possui seu próprio arquivo SQLite isolado:

| Demo | Arquivo | Seed | Profile |
|------|---------|------|---------|
| Legacy | `legacy.db` | 1001 | `legacy` |
| Modernization | `modernization.db` | 2002 | `modern` |
| Lab | `lab.db` | 3003 | `lab` |

**Vantagens:**
- Dados não se misturam entre demos
- Pode executar múltiplos demos simultaneamente
- Reset fácil: deletar o arquivo `.db`

### Configuração de Caminho

O caminho do banco é configurado via variável de ambiente `DB_PATH`:

```csharp
var dbPath = configuration["DB_PATH"] ?? "./data/legacy.db";

// Converte para caminho absoluto
if (!Path.IsPathRooted(dbPath))
{
    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    dbPath = Path.GetFullPath(Path.Combine(baseDirectory, dbPath));
}

// Garante que o diretório existe
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}
```

**Características:**
- Resolve caminhos relativos para absolutos
- Cria o diretório se não existir
- Suporta caminhos customizados por serviço

### Seeding Idempotente

O seeding é executado na inicialização de cada serviço:

```csharp
public async Task SeedAsync()
{
    // Verifica se já existe dados (seeding idempotente)
    if (await _context.Customers.AnyAsync())
    {
        return; // Já foi populado, não executa novamente
    }
    
    // Popula o banco...
}
```

**Características:**
- **Idempotente**: Pode ser executado múltiplas vezes sem duplicar dados
- **Determinístico**: Mesmo seed gera os mesmos dados
- **IDs Âncora**: Sempre cria customer 999, policy 1234 para testes consistentes

### Relacionamentos e Constraints

O `SeguroAutoDbContext` configura:

- **Chaves primárias**: Para todas as entidades
- **Índices únicos**: Document, PolicyNumber, ClaimNumber, QuoteNumber
- **Relacionamentos**: Customer → Policies → Claims (cascade delete)
- **Foreign Keys**: Garantem integridade referencial

---

## Service Discovery e Gateway

### Service Discovery no Aspire

O .NET Aspire fornece service discovery automático através de variáveis de ambiente injetadas:

**Formato das variáveis:**
```
services__{service-name}__{endpoint-name}__{index}
```

**Exemplo:**
```
services__quote-service__http__0=http://localhost:59219
```

### Gateway Legacy com AddYarp()

O demo Legacy usa `AddYarp()` nativo do Aspire para criar um gateway:

```csharp
var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/QuoteService.svc/{**catch-all}", quoteService);
        yarp.AddRoute("/PolicyService.svc/{**catch-all}", policyService);
        yarp.AddRoute("/ClaimsService.svc/{**catch-all}", claimsService);
        yarp.AddRoute("/PricingRulesService.svc/{**catch-all}", pricingRulesService);
    });
```

**Vantagens:**
- Service discovery automático
- Sem problemas de HttpSys (usa container YARP)
- Configuração declarativa
- Funciona em todas as plataformas

### Gateway Modernization (Strangler Fig)

O `Modern.Gateway` implementa o padrão **Strangler Fig**:

- Roteia `/api/*` para `Modern.Api` (REST)
- Roteia `/legacy/*` para serviços Legacy (SOAP)
- Permite migração incremental endpoint por endpoint

### Gateway Lab (Feature Flags)

O `Lab.Gateway` implementa feature flags para alternar entre Legacy e Modern:

```csharp
var useModern = Environment.GetEnvironmentVariable("USE_MODERN_API")?.ToLower() == "true";

if (useModern && context.Request.Path.StartsWithSegments("/api"))
{
    // Roteia para Modern API
}
else if (!useModern && context.Request.Path.StartsWithSegments("/api"))
{
    // Roteia para Legacy (converte REST para SOAP)
    context.Request.Path = "/legacy" + context.Request.Path;
}
```

**Uso didático:**
- Permite "virar a chave" entre Legacy e Modern
- Demonstra feature flags em ação
- Facilita testes A/B durante o treinamento

---

## Fault Injection e Resiliência

### Objetivo

Permitir que o instrutor demonstre problemas reais, controle o impacto e ative/desative falhas sem alterar código.

### Implementação

Middleware ASP.NET Core que executa antes do CoreWCF ou controllers:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (_options.Mode == FaultMode.Off)
    {
        await _next(context);
        return;
    }

    switch (_options.Mode)
    {
        case FaultMode.Delay:
            await HandleDelayAsync(context);
            break;
        case FaultMode.Error:
            await HandleErrorAsync(context);
            return; // Não continua o pipeline
        case FaultMode.Chaos:
            if (ShouldApplyChaos())
            {
                await HandleChaosAsync(context);
                return;
            }
            await HandleDelayAsync(context);
            break;
    }

    await _next(context);
}
```

### Modos de Falha

#### 1. Delay

Simula lentidão de rede ou processamento:

```csharp
private async Task HandleDelayAsync(HttpContext context)
{
    if (_options.DelayMs > 0)
    {
        _logger.LogWarning("Fault Injection: Applying delay of {DelayMs}ms", _options.DelayMs);
        await Task.Delay(_options.DelayMs);
    }
}
```

**Uso:**
- Demonstrar impacto de latência
- Testar timeouts
- Simular degradação de performance

#### 2. Error

Retorna erro consistente:

```csharp
private async Task HandleErrorAsync(HttpContext context)
{
    _logger.LogWarning("Fault Injection: Returning error {ErrorKind}", _options.ErrorKind);
    await ReturnErrorAsync(context, _options.ErrorKind);
}
```

**Tipos de erro:**
- `Http503`: Service Unavailable
- `Timeout`: Request Timeout
- `SoapFault`: SOAP fault XML

#### 3. Chaos

Aplica erro aleatório conforme taxa configurada:

```csharp
private bool ShouldApplyChaos()
{
    if (_options.ErrorRate <= 0)
        return false;

    var random = new Random();
    return random.NextDouble() < _options.ErrorRate;
}
```

**Uso:**
- Testar resiliência
- Demonstrar circuit breakers
- Simular falhas intermitentes

### Configuração

Via variáveis de ambiente:

```bash
FAULT_MODE=chaos          # off, delay, error, chaos
FAULT_DELAY_MS=300        # Delay em milissegundos
FAULT_ERROR_RATE=0.1      # Taxa de erro (0.0 a 1.0)
FAULT_ERROR_KIND=http503  # timeout, soapfault, http503
```

### Ordem no Pipeline

O middleware de fault injection deve estar **antes** do CoreWCF:

```csharp
app.UseRouting();
app.UseFaultInjection();  // Antes do CoreWCF
app.UseServiceModel(...); // CoreWCF
```

---

## Padrões de Modernização

### 1. Strangler Fig Pattern

O demo **Modernization** implementa o padrão Strangler Fig:

**Conceito:**
- Gradualmente "estrangula" o sistema legado
- Extrai funcionalidades para o novo sistema
- Mantém legado funcionando durante a transição

**Implementação:**
- `Modern.Gateway` roteia requisições
- Novos endpoints em `Modern.Api` (REST)
- Endpoints legados continuam funcionando (SOAP)
- Migração endpoint por endpoint

**Exemplo:**
```
GET /api/quotes/customer/999  → Modern.Api (REST)
POST /legacy/QuoteService.svc → Legacy.QuoteService (SOAP)
```

### 2. Feature Flags

O demo **Lab** implementa feature flags:

**Conceito:**
- Alternar entre implementações sem deploy
- Testar nova implementação com tráfego limitado
- Rollback rápido em caso de problemas

**Implementação:**
```csharp
var useModern = Environment.GetEnvironmentVariable("USE_MODERN_API")?.ToLower() == "true";

if (useModern)
{
    // Roteia para Modern.Api
}
else
{
    // Roteia para Legacy Services
}
```

**Uso:**
- `USE_MODERN_API=true`: Usa Modern.Api
- `USE_MODERN_API=false`: Usa Legacy Services

### 3. Coexistência Legado + Moderno

Durante a modernização, ambos os sistemas convivem:

- **Legado**: Continua funcionando, recebe manutenção mínima
- **Moderno**: Recebe novas funcionalidades e melhorias
- **Gateway**: Orquestra o roteamento entre ambos

---

## Orquestração com .NET Aspire

### AppHost por Demo

Cada demo possui seu próprio `AppHost` que orquestra todos os serviços:

- **Legacy.AppHost**: Orquestra serviços Legacy + Gateway
- **Modernization.AppHost**: Orquestra Legacy + Modern.Api + Gateway
- **Lab.AppHost**: Orquestra Legacy + Modern.Api + Gateway com feature flags

### Configuração de Serviços

Cada serviço é adicionado ao AppHost:

```csharp
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("quote-service")
    .WithHttpEndpoint()  // Porta dinâmica
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", "1001")
    .WithEnvironment("DATASET_PROFILE", "legacy")
    .WithEnvironment("FAULT_MODE", "delay")
    .WithEnvironment("FAULT_DELAY_MS", "300");
```

### Service Discovery

O Aspire injeta variáveis de ambiente com as URLs dos serviços:

```csharp
var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        // Service discovery automático - não precisa conhecer portas
        yarp.AddRoute("/QuoteService.svc/{**catch-all}", quoteService);
    });
```

### Portas do Dashboard

Cada demo usa portas diferentes para o dashboard:

- **Legacy**: `http://localhost:15000`
- **Modernization**: `http://localhost:15010`
- **Lab**: `http://localhost:15020`

Isso permite executar múltiplos demos simultaneamente.

---

## Ciclo de Vida e Dependency Injection

### Ciclo de Vida dos Serviços WCF

Os serviços CoreWCF são registrados como **Transient**:

```csharp
builder.Services.AddTransient<QuoteService>();
```

**Razão:**
- Cada requisição SOAP recebe uma nova instância
- Equivalente ao `InstanceContextMode.PerCall` do WCF clássico
- Evita problemas de concorrência e estado compartilhado

### Ciclo de Vida do DbContext

O `SeguroAutoDbContext` é registrado como **Scoped** (padrão do EF Core):

```csharp
services.AddDbContext<SeguroAutoDbContext>(options =>
    options.UseSqlite(connectionString));
```

**Características:**
- Uma instância por requisição HTTP
- Compartilhada entre serviços na mesma requisição
- Dispose automático ao final da requisição

### Ciclo de Vida dos Controllers REST

Os controllers do `Modern.Api` usam o ciclo de vida padrão do ASP.NET Core:

- **Scoped**: Uma instância por requisição HTTP
- Gerenciado automaticamente pelo framework

### Injeção de Dependências

Todos os serviços recebem dependências via construtor:

```csharp
public QuoteService(
    SeguroAutoDbContext context, 
    ILogger<QuoteService> logger)
{
    _context = context;
    _logger = logger;
}
```

**Vantagens:**
- Dependências explícitas
- Fácil de testar (mock de dependências)
- Respeita o princípio de inversão de dependência

---

## Observabilidade e Logging

### Logging Estruturado

Todos os serviços usam `ILogger<T>` para logging estruturado:

```csharp
_logger.LogInformation("GetQuote called for CustomerId: {CustomerId}, Vehicle: {VehicleModel}", 
    request.CustomerId, request.VehicleModel);

_logger.LogError(ex, "Error in GetQuote for CustomerId: {CustomerId}", request.CustomerId);
```

**Vantagens:**
- Logs estruturados (facilita busca e análise)
- Níveis apropriados (Information, Warning, Error)
- Contexto rico (parâmetros, exceções)

### Aspire Dashboard

O .NET Aspire fornece observabilidade integrada:

- **Logs**: Visualização de logs de todos os serviços
- **Traces**: Rastreamento distribuído de requisições
- **Métricas**: Performance e saúde dos serviços
- **Service Map**: Visualização da topologia dos serviços

### Tratamento de Exceções

Todos os serviços implementam tratamento consistente:

1. **Try-Catch** em todas as operações públicas
2. **Logging** de erros com contexto
3. **FaultException** para erros de negócio (SOAP)
4. **HTTP Status Codes** apropriados (REST)

---

## Estrutura de Demos

### Demo 1: Legacy

**Objetivo**: Mostrar o cenário legado puro

**Componentes:**
- 4 serviços CoreWCF (Quote, Policy, Claims, PricingRules)
- Gateway YARP nativo do Aspire
- Banco `legacy.db`
- Fault injection habilitado (delay 300ms)

**Uso**: Demonstrar problemas de legado, latência, contratos rígidos

### Demo 2: Modernization

**Objetivo**: Mostrar coexistência legado + moderno

**Componentes:**
- Serviços Legacy (SOAP)
- Modern.Api (REST)
- Modern.Gateway (Strangler Fig)
- Banco `modernization.db`
- Fault injection desabilitado

**Uso**: Demonstrar Strangler Fig Pattern, migração incremental

### Demo 3: Lab

**Objetivo**: Hands-on durante treinamento

**Componentes:**
- Serviços Legacy + Modern.Api
- Lab.Gateway (Feature Flags)
- Banco `lab.db`
- Fault injection configurável (chaos mode)

**Uso**: Exercícios práticos, feature flags, resiliência

---

## Decisões de Design Importantes

### 1. Message Contracts Obrigatórios

**Decisão**: Todos os parâmetros e retornos de operações SOAP devem ser `[MessageContract]`

**Razão**: CoreWCF não permite misturar `[MessageContract]` com tipos primitivos

**Impacto**: Mais verbosidade, mas maior compatibilidade com WCF clássico

### 2. Transient para Serviços WCF

**Decisão**: Serviços CoreWCF registrados como `Transient`

**Razão**: Cada requisição SOAP deve receber uma nova instância (equivalente a `PerCall`)

**Impacto**: Melhor isolamento, sem problemas de concorrência

### 3. SQLite por Demo

**Decisão**: Um arquivo SQLite isolado por demo

**Razão**: Isolamento de dados, facilita reset, permite execução simultânea

**Impacto**: Dados não se misturam, mas requer gerenciamento de arquivos

### 4. Seeding Idempotente

**Decisão**: Seeding só executa se o banco estiver vazio

**Razão**: Permite reiniciar serviços sem perder dados, mas facilita reset

**Impacto**: Reset requer deletar o arquivo `.db`

### 5. Gateway com AddYarp() Nativo

**Decisão**: Usar `AddYarp()` nativo do Aspire em vez de projeto Gateway customizado

**Razão**: Service discovery automático, sem problemas de HttpSys, mais simples

**Impacto**: Requer Aspire 9.3+ (ou versão com suporte a YARP)

---

## Considerações de Performance

### SQLite em Produção

**⚠️ Aviso**: SQLite não é recomendado para produção com alta concorrência

**Limitações:**
- Escrita sequencial (lock de arquivo)
- Não suporta múltiplos escritores simultâneos eficientemente
- Sem suporte a replicação nativa

**Uso no Projeto:**
- Apenas para demos e treinamento
- Dados mockados, baixa carga
- Ideal para cenários didáticos

### CoreWCF Performance

CoreWCF é mais leve que WCF clássico:
- Menos overhead de serialização
- Melhor integração com ASP.NET Core
- Suporta async/await nativamente

---

## Extensibilidade

### Adicionar Novo Serviço Legacy

1. Criar interface `IService.cs` com `[ServiceContract]`
2. Criar message contracts para parâmetros/retornos
3. Implementar `Service.cs` herdando da interface
4. Registrar no `Program.cs`:
   - `AddTransient<Service>()`
   - `AddServiceEndpoint<Service, IService>()`
5. Adicionar ao AppHost com `AddProject<Service>()`

### Adicionar Novo Endpoint REST

1. Criar controller em `Modern.Api/Controllers/`
2. Implementar ações HTTP (GET, POST, etc.)
3. Usar `SeguroAutoDbContext` para acesso a dados
4. Gateway roteia automaticamente `/api/*` para Modern.Api

### Adicionar Nova Regra de Fault Injection

1. Adicionar novo `FaultErrorKind` no enum
2. Implementar lógica em `ReturnErrorAsync()`
3. Configurar via variável de ambiente `FAULT_ERROR_KIND`

---

## Referências e Documentação

- [CoreWCF Documentation](https://github.com/CoreWCF/CoreWCF)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)
- [SQLite Documentation](https://www.sqlite.org/docs.html)

---

## Conclusão

Esta arquitetura foi projetada para:

1. **Reduzir fricção**: Funciona em qualquer plataforma, sem dependências pesadas
2. **Facilitar aprendizado**: Estrutura clara, código comentado, exemplos práticos
3. **Demonstrar modernização**: Mostra evolução de legado para moderno
4. **Suportar treinamento**: Múltiplos demos, dados consistentes, falhas controláveis

A arquitetura evolui gradualmente através dos três demos, demonstrando padrões e técnicas de modernização de sistemas legados.

