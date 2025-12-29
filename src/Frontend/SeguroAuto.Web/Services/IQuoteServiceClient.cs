namespace SeguroAuto.Web.Services;

public interface IQuoteServiceClient
{
    Task<QuoteResponse> GetQuoteAsync(int customerId, string vehiclePlate, string vehicleModel, int vehicleYear);
    Task<List<QuoteResponse>> GetQuotesByCustomerAsync(int customerId);
    Task<bool> ApproveQuoteAsync(string quoteNumber);
}

public class QuoteResponse
{
    public string QuoteNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public decimal Premium { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Status { get; set; } = string.Empty;
}

