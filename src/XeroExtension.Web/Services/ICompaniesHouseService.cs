using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public interface ICompaniesHouseService
{
    /// <summary>
    /// Looks up a company by its Companies House number. Returns null if no company number was
    /// given or the lookup fails — enrichment is best-effort and must not break the rest of the
    /// risk pipeline if Companies House is unreachable or the number doesn't match.
    /// </summary>
    Task<CompanyProfile?> GetCompanyProfileAsync(string companyNumber);
}
