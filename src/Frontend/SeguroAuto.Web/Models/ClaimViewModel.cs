using System.ComponentModel.DataAnnotations;

namespace SeguroAuto.Web.Models;

public class ClaimViewModel
{
    public string ClaimNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
}

public class ClaimCreateViewModel
{
    [Required(ErrorMessage = "O número da apólice é obrigatório")]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "A descrição é obrigatória")]
    [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "O valor é obrigatório")]
    [Range(0.01, 999999.99, ErrorMessage = "O valor deve estar entre 0,01 e 999.999,99")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "A data do incidente é obrigatória")]
    [DataType(DataType.DateTime)]
    public DateTime IncidentDate { get; set; } = DateTime.Now;
}

