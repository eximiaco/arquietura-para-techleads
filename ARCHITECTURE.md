# Arquitetura do Sistema - Seguro Auto

Este documento descreve em detalhes a arquitetura, estratégias e decisões técnicas do projeto de sistema legado para o domínio de Seguro para Automóveis, com foco em observabilidade e telemetria.

---

## Sumário

- [Visão Geral da Arquitetura](#visão-geral-da-arquitetura)
- [Estratégia de Criação dos Serviços WCF](#estratégia-de-criação-dos-serviços-wcf)
- [Arquitetura em Camadas](#arquitetura-em-camadas)
- [Estratégias de Uso do SQLite](#estratégias-de-uso-do-sqlite)
- [Service Discovery e Gateway](#service-discovery-e-gateway)
- [Fault Injection e Resiliência](#fault-injection-e-resiliência)
- [Orquestração com .NET Aspire](#orquestração-com-net-aspire)
- [Ciclo de Vida e Dependency Injection](#ciclo-de-vida-e-dependency-injection)
- [Observabilidade e Logging](#observabilidade-e-logging)
- [Telemetria de Banco de Dados](#telemetria-de-banco-de-dados)

---

## Visão Geral da Arquitetura

O projeto implementa uma arquitetura de sistema legado baseado em WCF/SOAP, com observabilidade completa através de OpenTelemetry e .NET Aspire. O foco é demonstrar como instrumentar e monitorar serviços legados com tracing distribuído, métricas e logs estruturados.

### Princípios Arquiteturais

1. **Redução de Fricção**: Zero dependências de Windows, IIS ou SQL Server
2. **Observabilidade Integrada**: Tracing distribuído com OpenTelemetry, logs e métricas através do Aspire Dashboard
3. **Dados Determinísticos**: Seeding idempotente com IDs âncora para testes consistentes
4. **Telemetria Profunda**: Spans customizados para operações SOAP, fault injection e banco de dados

### Stack Tecnológico

- **.NET 10**: Framework base para todos os projetos
- **CoreWCF**: Compatibilidade com WCF/SOAP em .NET moderno
- **ASP.NET Core**: Host para serviços CoreWCF
- **Entity Framework Core**: ORM para acesso a dados
- **SQLite**: Banco de dados leve e portável
- **.NET Aspire**: Orquestração e observabilidade
- **OpenTelemetry**: Tracing distribuído, métricas e exportação OTLP
- **YARP**: Proxy reverso para gateway

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
    [MessageBodyMember(Order = 1)]
    public int CustomerId { get; set; }

    [MessageBodyMember(Order = 2)]
    public string VehiclePlate { get; set; } = string.Empty;

    [MessageBodyMember(Order = 3)]
    public string VehicleModel { get; set; } = string.Empty;

    [MessageBodyMember(Order = 4)]
    public int VehicleYear { get; set; }
}
```

**Regras críticas:**
- Quando uma operação usa `[MessageContract]`, **todos** os parâmetros e o tipo de retorno devem ser `[MessageContract]`. Não é possível misturar com tipos primitivos.
- **IMPORTANTE**: Sempre especifique `Order` nos atributos `[MessageBodyMember]` para garantir a ordem correta de deserialização pelo CoreWCF.
- A ordem dos elementos no XML SOAP deve corresponder à ordem especificada no `Order`.

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
    public DbSet<DbOperationLog> DbOperationLogs { get; set; }
}
```

#### `DatabaseSeeder`

Responsável pelo seeding inicial do banco de dados:

- **Idempotente**: Só executa se o banco estiver vazio
- **Determinístico**: Usa seed fixo para gerar dados consistentes
- **IDs Âncora**: Sempre cria customer 999, policy 1234, etc.

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
- Formato SOAP: Usa prefixo `legacy:` com namespace definido no `soap:Envelope`
- Exemplo de formato correto:
  ```xml
  <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
                 xmlns:legacy="http://eximia.co/seguroauto/legacy">
      <soap:Body>
          <legacy:QuoteRequest>
              <legacy:CustomerId>999</legacy:CustomerId>
              <legacy:VehiclePlate>ABC-1234</legacy:VehiclePlate>
              <legacy:VehicleModel>Honda Civic</legacy:VehicleModel>
              <legacy:VehicleYear>2022</legacy:VehicleYear>
          </legacy:QuoteRequest>
      </soap:Body>
  </soap:Envelope>
  ```

#### Frontend MVC (SeguroAuto.Web)
- Aplicação ASP.NET Core MVC
- Consome serviços Legacy através do Gateway YARP
- Interface web para visualizar e criar cotações, apólices, sinistros e regras de precificação
- Localizado em `src/Frontend/SeguroAuto.Web`
- Acessível através do Gateway na porta 15100

#### Gateway (YARP)
- Proxy reverso para roteamento
- Service discovery automático
- **Legacy Gateway**: Porta fixa 15100, expõe serviços SOAP e Frontend MVC

### Camada de Infraestrutura Compartilhada

#### `SeguroAuto.ServiceDefaults`

Projeto compartilhado que configura OpenTelemetry para todos os serviços:

- **Tracing**: Instrumentação de ASP.NET Core, HttpClient e ActivitySources customizados
- **Métricas**: ASP.NET Core, HttpClient e runtime
- **Exportação OTLP**: Envia dados para o Aspire Dashboard automaticamente
- **ActivitySources registrados**:
  - `SeguroAuto.Web.SoapClient`: Spans para chamadas SOAP do frontend
  - `SeguroAuto.FaultInjection`: Spans para injeção de falhas
  - `SeguroAuto.Database`: Spans para operações de banco de dados

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

### Banco de Dados

O projeto utiliza um arquivo SQLite isolado:

| Arquivo | Seed | Profile |
|---------|------|---------|
| `legacy.db` | 1001 | `legacy` |

**Vantagens:**
- Reset fácil: deletar o arquivo `.db`
- Sem necessidade de servidor externo

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
- **Relacionamentos**: Customer -> Policies -> Claims (cascade delete)
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

O projeto usa `AddYarp()` nativo do Aspire para criar um gateway:

```csharp
var gateway = builder.AddYarp("gateway")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 15100; // Porta fixa para facilitar testes
    })
    .WithConfiguration(yarp =>
    {
        // Rotas para serviços SOAP
        yarp.AddRoute("/QuoteService.svc/{**catch-all}", quoteService);
        yarp.AddRoute("/PolicyService.svc/{**catch-all}", policyService);
        yarp.AddRoute("/ClaimsService.svc/{**catch-all}", claimsService);
        yarp.AddRoute("/PricingRulesService.svc/{**catch-all}", pricingRulesService);

        // Rota para o frontend MVC (catch-all)
        yarp.AddRoute("/{**catch-all}", frontend);
    });
```

**Vantagens:**
- Service discovery automático
- Sem problemas de HttpSys (usa container YARP)
- Configuração declarativa
- Funciona em todas as plataformas
- **Porta fixa (15100)**: Facilita testes e uso de arquivos .http

**Porta fixa do Gateway:**
- O gateway sempre responde na porta **15100**
- Isso permite usar URLs fixas em arquivos `.http` e scripts de teste
- Os demais serviços continuam com portas dinâmicas (gerenciadas pelo Aspire)

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

## Orquestração com .NET Aspire

### Legacy.AppHost

O `Legacy.AppHost` orquestra todos os serviços do sistema:

- 4 serviços Legacy (Quote, Policy, Claims, PricingRules)
- Gateway YARP (porta fixa 15100)
- Frontend MVC (SeguroAuto.Web)
- DbTelemetryWorker (worker de telemetria de banco)

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

### Dashboard

O Aspire Dashboard fica disponível em:

- **Dashboard**: `http://localhost:15000`
- **OTLP Endpoint**: `http://localhost:15001`

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

### OpenTelemetry e ServiceDefaults

O projeto `SeguroAuto.ServiceDefaults` configura OpenTelemetry para todos os serviços de forma centralizada:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("SeguroAuto.Web.SoapClient")
            .AddSource("SeguroAuto.FaultInjection")
            .AddSource("SeguroAuto.Database")
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .UseOtlpExporter();
```

O Aspire injeta automaticamente nos processos filhos as variáveis `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME` e `OTEL_RESOURCE_ATTRIBUTES`, garantindo que cada serviço exporte telemetria para o Dashboard sem configuracao manual.

### Spans Customizados

O sistema registra tres `ActivitySource` customizados para tracing profundo:

#### 1. `SeguroAuto.Web.SoapClient`
Spans criados pelo frontend ao fazer chamadas SOAP para os serviços Legacy. Permitem visualizar no trace distribuído o tempo gasto em cada chamada SOAP, incluindo a construção do envelope XML e o parsing da resposta.

#### 2. `SeguroAuto.FaultInjection`
Spans emitidos pelo middleware de fault injection. Registram quando uma falha é injetada (delay, error ou chaos), o tipo de falha e a duração do delay aplicado. Facilitam a correlação entre falhas injetadas e seus efeitos no trace.

#### 3. `SeguroAuto.Database`
Spans emitidos pelo `DbTelemetryWorker` a partir dos registros da tabela `db_operation_logs`. Representam operações de banco de dados (INSERT, UPDATE, SELECT) vinculadas ao trace original do serviço que executou a operação. Incluem tags semanticas OpenTelemetry como `db.system`, `db.operation`, `db.sql.table`.

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
- **Traces**: Rastreamento distribuído de requisições (incluindo spans SOAP, fault injection e banco)
- **Métricas**: Performance e saúde dos serviços
- **Service Map**: Visualização da topologia dos serviços

### Tratamento de Exceções

Todos os serviços implementam tratamento consistente:

1. **Try-Catch** em todas as operações públicas
2. **Logging** de erros com contexto
3. **FaultException** para erros de negócio (SOAP)

---

## Telemetria de Banco de Dados

### Visão Geral

O sistema implementa um padrão de telemetria assíncrona para operações de banco de dados, permitindo visualizar no Aspire Dashboard spans de operações SQL vinculados ao trace original do serviço.

### Tabela `db_operation_logs`

A tabela `db_operation_logs` armazena registros de operações de banco de dados com contexto de trace:

```csharp
modelBuilder.Entity<DbOperationLog>(entity =>
{
    entity.ToTable("db_operation_logs");
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.Exported);
});
```

Cada registro contém:
- **TraceId / SpanId**: Contexto do trace original (permite vincular o span como filho)
- **OperationName**: Nome da operação (ex: `INSERT Customer`)
- **OperationType**: Tipo da operação SQL (INSERT, UPDATE, SELECT, DELETE)
- **TableName**: Tabela afetada
- **StartedAt / EndedAt**: Timestamps da operação
- **Status**: Sucesso ou erro
- **ErrorMessage**: Mensagem de erro (quando aplicável)
- **Details**: Detalhes adicionais
- **Exported**: Flag indicando se o registro já foi processado pelo worker

### DbTelemetryWorker

O `Legacy.DbTelemetryWorker` é um `BackgroundService` que faz polling na tabela `db_operation_logs` a cada 2 segundos e converte registros pendentes em spans OpenTelemetry:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ProcessPendingLogsAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
    }
}
```

**Processo de conversão:**

1. Busca ate 50 registros com `Exported = false`
2. Para cada registro, reconstroi o `ActivityContext` original a partir de `TraceId` e `SpanId`
3. Cria um novo span usando `ActivitySource("SeguroAuto.Database")` com o parent context original
4. Adiciona tags semanticas OpenTelemetry: `db.system`, `db.operation`, `db.sql.table`, `db.operation.name`
5. Marca o registro como `Exported = true`

**Resultado:** Os spans de banco aparecem no Aspire Dashboard como filhos do trace do servico que executou a operacao, proporcionando visibilidade completa do fluxo desde a requisição HTTP ate a operação no SQLite.

---

## Componentes do Sistema

### Visão Geral

**Componentes:**
- 4 serviços CoreWCF (Quote, Policy, Claims, PricingRules)
- Gateway YARP nativo do Aspire (porta fixa 15100)
- Frontend MVC (ASP.NET Core) - consome serviços através do Gateway
- DbTelemetryWorker - worker de telemetria de banco de dados
- Banco `legacy.db`
- Fault injection habilitado (delay 300ms)

**Acesso:**
- Gateway: http://localhost:15100 (porta fixa)
- Frontend: http://localhost:15100 (através do gateway)
- Serviços SOAP: http://localhost:15100/QuoteService.svc (através do gateway)
- Dashboard Aspire: http://localhost:15000

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

### 3. SQLite como Banco de Dados

**Decisão**: Usar SQLite como banco de dados

**Razão**: Isolamento de dados, facilita reset, zero setup

**Impacto**: Dados em arquivo único, mas requer gerenciamento de arquivos

### 4. Seeding Idempotente

**Decisão**: Seeding só executa se o banco estiver vazio

**Razão**: Permite reiniciar serviços sem perder dados, mas facilita reset

**Impacto**: Reset requer deletar o arquivo `.db`

### 5. Gateway com AddYarp() Nativo

**Decisão**: Usar `AddYarp()` nativo do Aspire em vez de projeto Gateway customizado

**Razão**: Service discovery automático, sem problemas de HttpSys, mais simples

**Impacto**: Requer Aspire com suporte a YARP

### 6. Porta Fixa do Gateway

**Decisão**: Gateway sempre usa porta fixa 15100

**Razão**: Facilita testes e uso de arquivos `.http` sem precisar atualizar URLs a cada execução

**Impacto**:
- URLs fixas em arquivos de teste
- Facilita desenvolvimento e debugging
- Demais serviços continuam com portas dinâmicas

**Configuração:**
```csharp
var gateway = builder.AddYarp("gateway")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 15100; // Porta fixa
    })
    .WithConfiguration(yarp => { ... });
```

### 7. Formato SOAP com Prefixo legacy:

**Decisão**: Usar prefixo `legacy:` em todos os elementos SOAP com namespace definido no `soap:Envelope`

**Razão**: CoreWCF requer namespace explícito para deserialização correta dos MessageContracts

**Impacto**:
- XML SOAP mais verboso, mas compatível com CoreWCF
- Parsing das respostas deve suportar namespace com/sem prefixo (fallback)

**Formato correto:**
```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:legacy="http://eximia.co/seguroauto/legacy">
    <soap:Body>
        <legacy:QuoteRequest>
            <legacy:CustomerId>999</legacy:CustomerId>
            <legacy:VehiclePlate>ABC-1234</legacy:VehiclePlate>
        </legacy:QuoteRequest>
    </soap:Body>
</soap:Envelope>
```

### 8. Order nos MessageBodyMember

**Decisão**: Sempre especificar `Order` nos atributos `[MessageBodyMember]`

**Razão**: Garante ordem correta de deserialização pelo CoreWCF, especialmente importante quando há múltiplos campos

**Impacto**: Mais verbosidade nos contratos, mas maior confiabilidade na deserialização

**Exemplo:**
```csharp
[MessageContract]
public class QuoteRequest
{
    [MessageBodyMember(Order = 1)]
    public int CustomerId { get; set; }

    [MessageBodyMember(Order = 2)]
    public string VehiclePlate { get; set; } = string.Empty;
}
```

### 9. Frontend MVC Consumindo SOAP

**Decisão**: Criar frontend MVC que consome serviços SOAP através do Gateway

**Razão**: Demonstra integração de aplicações modernas com serviços legados SOAP

**Impacto**:
- Frontend usa `HttpClientFactory` para fazer requisições SOAP
- Parsing manual de XML SOAP nas respostas
- Suporte a namespace com/sem prefixo para robustez
- Service discovery do Aspire para obter URL do gateway

### 10. Telemetria Assíncrona de Banco via Worker

**Decisão**: Usar tabela `db_operation_logs` + `BackgroundService` em vez de instrumentação síncrona do EF Core

**Razão**: Desacopla a emissão de spans da operação de banco, evitando overhead no path crítico dos serviços

**Impacto**:
- Spans de banco aparecem com delay de ate 2 segundos no Dashboard
- Worker independente pode ser escalado separadamente
- Trace context preservado via TraceId/SpanId na tabela

---

## Considerações de Performance

### SQLite em Produção

**Aviso**: SQLite não é recomendado para produção com alta concorrência

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
2. Criar message contracts para parâmetros/retornos:
   - **IMPORTANTE**: Sempre especificar `Order` nos `[MessageBodyMember]`
   - Exemplo: `[MessageBodyMember(Order = 1)]`
3. Implementar `Service.cs` herdando da interface
4. Registrar no `Program.cs`:
   - `AddTransient<Service>()`
   - `AddServiceEndpoint<Service, IService>()`
5. Adicionar ao AppHost com `AddProject<Service>()`
6. Adicionar rota no Gateway YARP:
   ```csharp
   yarp.AddRoute("/ServiceName.svc/{**catch-all}", service);
   ```
7. Se criar cliente SOAP no frontend:
   - Usar prefixo `legacy:` em todos os elementos XML
   - Definir namespace no `soap:Envelope`
   - Implementar parsing com fallback para namespace com/sem prefixo

### Adicionar Nova Regra de Fault Injection

1. Adicionar novo `FaultErrorKind` no enum
2. Implementar lógica em `ReturnErrorAsync()`
3. Configurar via variável de ambiente `FAULT_ERROR_KIND`

---

## Clientes SOAP no Frontend

### Estrutura dos Clientes SOAP

O frontend MVC (`SeguroAuto.Web`) contém clientes SOAP para consumir os serviços Legacy:

- `QuoteServiceClient`: Consome QuoteService
- `PolicyServiceClient`: Consome PolicyService
- `ClaimsServiceClient`: Consome ClaimsService
- `PricingRulesServiceClient`: Consome PricingRulesService

### Formato SOAP Correto

Todos os clientes SOAP seguem o formato correto:

```csharp
private string BuildSoapEnvelope(string body)
{
    // Namespace legacy definido no Envelope
    return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
               xmlns:legacy=""{Namespace}"">
    <soap:Body>
        {body}
    </soap:Body>
</soap:Envelope>";
}

// Body com prefixo legacy: em todos os elementos
var soapBody = $@"<legacy:QuoteRequest>
    <legacy:CustomerId>{customerId}</legacy:CustomerId>
    <legacy:VehiclePlate>{EscapeXml(vehiclePlate)}</legacy:VehiclePlate>
    <legacy:VehicleModel>{EscapeXml(vehicleModel)}</legacy:VehicleModel>
    <legacy:VehicleYear>{vehicleYear}</legacy:VehicleYear>
</legacy:QuoteRequest>";
```

**Características importantes:**
- Namespace `xmlns:legacy` definido no `soap:Envelope`, não no body
- Prefixo `legacy:` usado em todos os elementos do contrato
- Ordem dos elementos corresponde ao `Order` especificado nos `[MessageBodyMember]`

### Parsing Robusto de Respostas

O parsing das respostas SOAP suporta namespace com/sem prefixo (fallback):

```csharp
// Tentar com namespace primeiro, depois sem namespace (fallback)
var quoteResponse = body?.Descendants(legacyNs + "QuoteResponse").FirstOrDefault()
                ?? body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "QuoteResponse");

// Para elementos filhos também
var quoteNumber = quoteResponse.Element(legacyNs + "QuoteNumber")?.Value
               ?? quoteResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "QuoteNumber")?.Value
               ?? string.Empty;
```

**Vantagens:**
- Funciona mesmo se o CoreWCF retornar XML sem prefixo
- Mais robusto e tolerante a variações no formato
- Facilita debugging quando há problemas de namespace

### Service Discovery do Gateway

O frontend obtém a URL do gateway através do service discovery do Aspire:

```csharp
_gatewayUrl = _configuration["services__gateway__http__0"]
           ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
           ?? "http://localhost:15100"; // Fallback para porta fixa
```

**Formato da variável:**
- `services__gateway__http__0`: URL do gateway injetada pelo Aspire
- Como o gateway usa porta fixa (15100), o fallback sempre funciona

### Registro no DI Container

Os clientes SOAP são registrados como `Scoped` no DI container:

```csharp
builder.Services.AddHttpClient(); // Registra IHttpClientFactory
builder.Services.AddScoped<IQuoteServiceClient, QuoteServiceClient>();
builder.Services.AddScoped<IPolicyServiceClient, PolicyServiceClient>();
// ...
```

**Razão:**
- `Scoped`: Uma instância por requisição HTTP
- Usa `IHttpClientFactory` para gerenciar `HttpClient` instances
- Permite injeção de dependências (IConfiguration, ILogger)

### Tratamento de Erros

Todos os clientes implementam tratamento robusto de erros:

```csharp
if (!response.IsSuccessStatusCode)
{
    var errorContent = await response.Content.ReadAsStringAsync();
    _logger.LogError("SOAP request failed with status {StatusCode}. Response: {Response}",
        response.StatusCode, errorContent);
    response.EnsureSuccessStatusCode();
}
```

**Características:**
- Logging detalhado de erros HTTP
- Extração de mensagens de erro do SOAP Fault quando possível
- Exceções informativas para facilitar debugging

---

## Referências e Documentação

- [CoreWCF Documentation](https://github.com/CoreWCF/CoreWCF)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)
- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)

---

## Conclusão

Esta arquitetura foi projetada para:

1. **Reduzir fricção**: Funciona em qualquer plataforma, sem dependências pesadas
2. **Facilitar aprendizado**: Estrutura clara, código comentado, exemplos práticos
3. **Demonstrar observabilidade**: Tracing distribuído completo com OpenTelemetry, desde a requisição HTTP ate a operação no banco de dados
4. **Suportar treinamento**: Dados consistentes, falhas controláveis, telemetria profunda

A arquitetura combina serviços Legacy com observabilidade moderna, demonstrando como instrumentar e monitorar sistemas legados de forma eficaz.
