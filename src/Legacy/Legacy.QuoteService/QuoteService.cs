using System.Diagnostics;
using System.ServiceModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.Domain;

namespace Legacy.QuoteService;

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
        try
        {
            // Log detalhado do request recebido
            _logger.LogInformation("GetQuote called - CustomerId: {CustomerId}, VehicleModel: '{VehicleModel}' (IsNull: {IsNull}, Length: {Length}), VehiclePlate: '{VehiclePlate}' (IsNull: {IsNull2}, Length: {Length2}), VehicleYear: {VehicleYear}", 
                request.CustomerId, 
                request.VehicleModel ?? "<NULL>", 
                request.VehicleModel == null, 
                request.VehicleModel?.Length ?? -1,
                request.VehiclePlate ?? "<NULL>",
                request.VehiclePlate == null,
                request.VehiclePlate?.Length ?? -1,
                request.VehicleYear);

            // Validação dos campos obrigatórios
            if (string.IsNullOrWhiteSpace(request.VehicleModel))
            {
                _logger.LogError("VehicleModel is null or empty for CustomerId: {CustomerId}. Request details - Plate: '{Plate}', Year: {Year}", 
                    request.CustomerId, request.VehiclePlate ?? "<NULL>", request.VehicleYear);
                throw new FaultException("VehicleModel is required");
            }
            
            if (string.IsNullOrWhiteSpace(request.VehiclePlate))
            {
                _logger.LogError("VehiclePlate is null or empty for CustomerId: {CustomerId}. Request details - Model: '{Model}', Year: {Year}", 
                    request.CustomerId, request.VehicleModel ?? "<NULL>", request.VehicleYear);
                throw new FaultException("VehiclePlate is required");
            }

            var customer = _context.Customers.Find(request.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
                throw new FaultException("Customer not found");
            }

            // Calcula prêmio base
            var basePremium = CalculateBasePremium(request.VehicleYear, request.VehicleModel);
            
            // Aplica regras de pricing
            var finalPremium = ApplyPricingRules(basePremium, request, customer);

            // Executa "stored procedure" simulada via raw SQL, passando correlation_id
            var quoteNumber = ExecuteCreateQuoteProcedure(
                request.CustomerId,
                request.VehiclePlate ?? string.Empty,
                request.VehicleModel ?? string.Empty,
                request.VehicleYear,
                finalPremium);

            var validUntil = DateTime.UtcNow.AddDays(30);

            _logger.LogInformation("Quote created: {QuoteNumber} for CustomerId: {CustomerId} with Premium: {Premium}",
                quoteNumber, request.CustomerId, finalPremium);

            return new QuoteResponse
            {
                QuoteNumber = quoteNumber,
                CustomerId = request.CustomerId,
                Premium = finalPremium,
                ValidUntil = validUntil,
                Status = QuoteStatus.Pending.ToString()
            };
        }
        catch (FaultException)
        {
            throw; // Re-throw FaultException as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetQuote for CustomerId: {CustomerId}", request.CustomerId);
            throw new FaultException($"Error creating quote: {ex.Message}");
        }
    }

    public GetQuotesByCustomerResponse GetQuotesByCustomer(GetQuotesByCustomerRequest request)
    {
        try
        {
            _logger.LogInformation("GetQuotesByCustomer called for CustomerId: {CustomerId}", request.CustomerId);

            var quotes = _context.Quotes
                .Where(q => q.CustomerId == request.CustomerId)
                .OrderByDescending(q => q.CreatedAt)
                .Take(10)
                .ToList();

            _logger.LogInformation("Found {Count} quotes for CustomerId: {CustomerId}", quotes.Count, request.CustomerId);

            return new GetQuotesByCustomerResponse
            {
                Quotes = quotes.Select(q => new QuoteResponse
                {
                    QuoteNumber = q.QuoteNumber,
                    CustomerId = q.CustomerId,
                    Premium = q.Premium,
                    ValidUntil = q.ValidUntil,
                    Status = q.Status.ToString()
                }).ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetQuotesByCustomer for CustomerId: {CustomerId}", request.CustomerId);
            throw new FaultException($"Error retrieving quotes: {ex.Message}");
        }
    }

    public ApproveQuoteResponse ApproveQuote(ApproveQuoteRequest request)
    {
        try
        {
            _logger.LogInformation("ApproveQuote called for QuoteNumber: {QuoteNumber}", request.QuoteNumber);

            var success = ExecuteApproveQuoteProcedure(request.QuoteNumber);

            if (success)
                _logger.LogInformation("Quote approved: {QuoteNumber}", request.QuoteNumber);
            else
                _logger.LogWarning("Quote not found: {QuoteNumber}", request.QuoteNumber);

            return new ApproveQuoteResponse { Success = success };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ApproveQuote for QuoteNumber: {QuoteNumber}", request.QuoteNumber);
            throw new FaultException($"Error approving quote: {ex.Message}");
        }
    }

    private decimal CalculateBasePremium(int vehicleYear, string vehicleModel)
    {
        var age = DateTime.Now.Year - vehicleYear;
        var basePremium = 1000m;

        if (age < 3)
            basePremium = 1200m;
        else if (age < 10)
            basePremium = 1000m;
        else
            basePremium = 1500m;

        return basePremium;
    }

    private decimal ApplyPricingRules(decimal basePremium, QuoteRequest request, Customer customer)
    {
        var rules = _context.PricingRules.Where(r => r.IsActive).ToList();
        var finalPremium = basePremium;

        foreach (var rule in rules)
        {
            if (ShouldApplyRule(rule, request, customer))
            {
                finalPremium *= rule.Multiplier;
            }
        }

        return Math.Round(finalPremium, 2);
    }

    private bool ShouldApplyRule(PricingRule rule, QuoteRequest request, Customer customer)
    {
        // Implementação simplificada - em produção seria mais complexa
        if (rule.Condition.Contains("VehicleYear <"))
        {
            var age = DateTime.Now.Year - request.VehicleYear;
            return age > 10;
        }

        if (rule.Condition.Contains("VehicleYear >="))
        {
            var age = DateTime.Now.Year - request.VehicleYear;
            return age <= 2;
        }

        if (rule.Condition.Contains("Customer.Policies.Count"))
        {
            var policyCount = _context.Policies.Count(p => p.CustomerId == customer.Id);
            return policyCount > 2;
        }

        return false;
    }

    /// <summary>
    /// Captura informações da sessão PostgreSQL para enriquecer a telemetria.
    /// Simula o que uma stored procedure real faria internamente.
    /// </summary>
    private record PgSessionInfo(int Pid, string TransactionId, string SessionUser,
        string ServerIp, string ServerPort, string DbName, string ApplicationName);

    private PgSessionInfo GetPgSessionInfo()
    {
        using var cmd = _context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = @"
            SELECT pg_backend_pid(),
                   txid_current()::text,
                   session_user,
                   COALESCE(inet_server_addr()::text, 'localhost'),
                   COALESCE(inet_server_port()::text, '5432'),
                   current_database(),
                   current_setting('application_name')";

        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            cmd.Connection.Open();

        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new PgSessionInfo(
            Pid: reader.GetInt32(0),
            TransactionId: reader.GetString(1),
            SessionUser: reader.GetString(2),
            ServerIp: reader.GetString(3),
            ServerPort: reader.GetString(4),
            DbName: reader.GetString(5),
            ApplicationName: reader.GetString(6));
    }

    /// <summary>
    /// Loga operação na tabela db_operation_logs com trace context e informações da sessão PostgreSQL.
    /// </summary>
    private void LogDbOperation(string traceId, string spanId, string operationName,
        string operationType, string tableName, string details, DateTime startedAt, DateTime endedAt,
        string status, string? errorMessage, PgSessionInfo? session)
    {
        _context.Database.ExecuteSqlRaw(@"
            INSERT INTO db_operation_logs
                (""TraceId"", ""SpanId"", ""OperationName"", ""OperationType"", ""TableName"",
                 ""Details"", ""StartedAt"", ""EndedAt"", ""Status"", ""ErrorMessage"", ""Exported"",
                 ""DbPid"", ""DbTransactionId"", ""DbSessionUser"", ""DbServerIp"",
                 ""DbServerPort"", ""DbName"", ""DbApplicationName"")
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, false,
                    {10}, {11}, {12}, {13}, {14}, {15}, {16})",
            traceId, spanId, operationName, operationType, tableName,
            details, startedAt, endedAt, status, errorMessage ?? (object)DBNull.Value,
            session?.Pid ?? (object)DBNull.Value,
            session?.TransactionId ?? (object)DBNull.Value,
            session?.SessionUser ?? (object)DBNull.Value,
            session?.ServerIp ?? (object)DBNull.Value,
            session?.ServerPort ?? (object)DBNull.Value,
            session?.DbName ?? (object)DBNull.Value,
            session?.ApplicationName ?? (object)DBNull.Value);
    }

    /// <summary>
    /// Simula uma stored procedure que recebe o correlation_id (trace context),
    /// executa a operação de negócio e loga na tabela db_operation_logs
    /// com informações da sessão PostgreSQL (PID, transaction ID, server IP, etc.).
    /// </summary>
    private string ExecuteCreateQuoteProcedure(
        int customerId, string vehiclePlate, string vehicleModel,
        int vehicleYear, decimal premium)
    {
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? "";
        var spanId = activity?.SpanId.ToString() ?? "";

        var quoteNumber = $"QUOTE-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var now = DateTime.UtcNow;
        var startedAt = now;

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            // Captura informações da sessão PostgreSQL dentro da transaction
            var session = GetPgSessionInfo();

            // Passo 1: operação de negócio - INSERT do quote
            _context.Database.ExecuteSqlRaw(@"
                INSERT INTO ""Quotes"" (""QuoteNumber"", ""CustomerId"", ""VehiclePlate"", ""VehicleModel"",
                                    ""VehicleYear"", ""Premium"", ""Status"", ""ValidUntil"", ""CreatedAt"")
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})",
                quoteNumber, customerId, vehiclePlate, vehicleModel,
                vehicleYear, premium, (int)QuoteStatus.Pending,
                now.AddDays(30), now);

            var endedAt = DateTime.UtcNow;

            // Passo 2: loga a operação com correlation_id + sessão PostgreSQL
            var details = JsonSerializer.Serialize(new
            {
                quote_number = quoteNumber,
                customer_id = customerId,
                vehicle_model = vehicleModel,
                premium
            });

            LogDbOperation(traceId, spanId, "sp_create_quote", "INSERT", "Quotes",
                details, startedAt, endedAt, "OK", null, session);

            transaction.Commit();
            return quoteNumber;
        }
        catch (Exception ex)
        {
            var endedAt = DateTime.UtcNow;
            transaction.Rollback();

            try
            {
                LogDbOperation(traceId, spanId, "sp_create_quote", "INSERT", "Quotes",
                    "{}", startedAt, endedAt, "ERROR", ex.Message, null);
            }
            catch { /* telemetria não deve quebrar o fluxo de negócio */ }

            throw;
        }
    }

    /// <summary>
    /// Simula stored procedure de aprovação com correlation_id + sessão PostgreSQL.
    /// </summary>
    private bool ExecuteApproveQuoteProcedure(string quoteNumber)
    {
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? "";
        var spanId = activity?.SpanId.ToString() ?? "";

        var startedAt = DateTime.UtcNow;

        var quote = _context.Quotes.FirstOrDefault(q => q.QuoteNumber == quoteNumber);
        if (quote == null)
            return false;

        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var session = GetPgSessionInfo();

            _context.Database.ExecuteSqlRaw(@"
                UPDATE ""Quotes"" SET ""Status"" = {0} WHERE ""QuoteNumber"" = {1}",
                (int)QuoteStatus.Approved, quoteNumber);

            var endedAt = DateTime.UtcNow;

            var details = JsonSerializer.Serialize(new
            {
                quote_number = quoteNumber,
                previous_status = quote.Status.ToString(),
                new_status = QuoteStatus.Approved.ToString()
            });

            LogDbOperation(traceId, spanId, "sp_approve_quote", "UPDATE", "Quotes",
                details, startedAt, endedAt, "OK", null, session);

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            var endedAt = DateTime.UtcNow;
            transaction.Rollback();

            try
            {
                LogDbOperation(traceId, spanId, "sp_approve_quote", "UPDATE", "Quotes",
                    "{}", startedAt, endedAt, "ERROR", ex.Message, null);
            }
            catch { /* telemetria não deve quebrar o fluxo de negócio */ }

            throw;
        }
    }
}

