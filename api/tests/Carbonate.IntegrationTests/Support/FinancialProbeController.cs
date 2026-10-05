using System.Text.Json.Serialization;
using Carbonate.Application.Masking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.IntegrationTests.Support;

/// <summary>
/// Returns financial fields without masking them in a service, to prove the global filter alone
/// keeps them out of the response.
/// </summary>
[ApiController]
[Route("api/test/finance")]
public class FinancialProbeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get() => Ok(new ProbeDto
    {
        Name = "Naidoo Wedding",
        TotalIncVat = 1150m,
        UnitCostToUs = 400m,
        MarginPercent = 22m,
        Crew = [new ProbeCrew { UserId = Guid.Empty, HourlyRate = 180m }],
    });

    public class ProbeDto
    {
        public string Name { get; set; } = "";

        [FinancialField(FinancialTier.Price)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? TotalIncVat { get; set; }

        [FinancialField(FinancialTier.Cost)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? UnitCostToUs { get; set; }

        [FinancialField(FinancialTier.Margin)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? MarginPercent { get; set; }

        public List<ProbeCrew> Crew { get; set; } = [];
    }

    public class ProbeCrew
    {
        public Guid UserId { get; set; }

        [FinancialField(FinancialTier.Staff)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? HourlyRate { get; set; }
    }
}
