using System.ComponentModel.DataAnnotations;

namespace SeguroAuto.Web.Models;

public class PolicyViewModel
{
    public string PolicyNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public decimal Premium { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PolicyCreateViewModel
{
    [Required(ErrorMessage = "O ID do cliente é obrigatório")]
    [Range(1, int.MaxValue, ErrorMessage = "O ID do cliente deve ser maior que zero")]
    public int CustomerId { get; set; } = 999;

    [Required(ErrorMessage = "A placa do veículo é obrigatória")]
    [StringLength(10, ErrorMessage = "A placa deve ter no máximo 10 caracteres")]
    public string VehiclePlate { get; set; } = string.Empty;

    [Required(ErrorMessage = "O modelo do veículo é obrigatório")]
    [StringLength(100, ErrorMessage = "O modelo deve ter no máximo 100 caracteres")]
    public string VehicleModel { get; set; } = string.Empty;

    [Required(ErrorMessage = "O ano do veículo é obrigatório")]
    [Range(1900, 2100, ErrorMessage = "O ano deve estar entre 1900 e 2100")]
    public int VehicleYear { get; set; } = DateTime.Now.Year;

    [Required(ErrorMessage = "O prêmio é obrigatório")]
    [Range(0.01, 999999.99, ErrorMessage = "O prêmio deve estar entre 0,01 e 999.999,99")]
    public decimal Premium { get; set; }
}

