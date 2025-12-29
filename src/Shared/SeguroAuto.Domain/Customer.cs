namespace SeguroAuto.Domain;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty; // CPF/CNPJ
    public DateTime CreatedAt { get; set; }
    public List<Policy> Policies { get; set; } = new();
}

