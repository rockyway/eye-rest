using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    public interface IReportingService
    {
        Task<string> GenerateHealthReportAsync(int days = 30);
        Task<string> GenerateComplianceReportAsync(int days = 30);
        Task<string> GenerateUsageReportAsync(int days = 30);
        Task<string> ExportReportAsync(string reportType, ExportFormat format, int days = 30);
    }
}