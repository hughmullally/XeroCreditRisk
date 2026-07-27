using System.Net.Http.Json;
using System.Text.Json.Serialization;
using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public class CompaniesHouseService(HttpClient httpClient, ILogger<CompaniesHouseService> logger) : ICompaniesHouseService
{
    public async Task<CompanyProfile?> GetCompanyProfileAsync(string companyNumber)
    {
        if (string.IsNullOrWhiteSpace(companyNumber))
            return null;

        try
        {
            var response = await httpClient.GetAsync($"company/{companyNumber.Trim()}");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Companies House lookup for {CompanyNumber} returned {StatusCode}", companyNumber, response.StatusCode);
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<CompanyProfileDto>();
            if (dto is null)
                return null;

            return new CompanyProfile
            {
                CompanyNumber = dto.CompanyNumber ?? companyNumber,
                CompanyName = dto.CompanyName ?? string.Empty,
                Status = dto.CompanyStatus ?? string.Empty,
                IncorporationDate = dto.DateOfCreation,
                AccountsOverdue = dto.Accounts?.Overdue ?? false,
                ConfirmationStatementOverdue = dto.ConfirmationStatement?.Overdue ?? false
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Companies House lookup failed for {CompanyNumber}", companyNumber);
            return null;
        }
    }

    private class CompanyProfileDto
    {
        [JsonPropertyName("company_number")] public string? CompanyNumber { get; set; }
        [JsonPropertyName("company_name")] public string? CompanyName { get; set; }
        [JsonPropertyName("company_status")] public string? CompanyStatus { get; set; }
        [JsonPropertyName("date_of_creation")] public DateTime? DateOfCreation { get; set; }
        [JsonPropertyName("accounts")] public AccountsDto? Accounts { get; set; }
        [JsonPropertyName("confirmation_statement")] public ConfirmationStatementDto? ConfirmationStatement { get; set; }
    }

    private class AccountsDto
    {
        [JsonPropertyName("overdue")] public bool Overdue { get; set; }
    }

    private class ConfirmationStatementDto
    {
        [JsonPropertyName("overdue")] public bool Overdue { get; set; }
    }
}
