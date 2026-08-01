using SmartInvest.Application.Services.Import;

namespace SmartInvest.Application.Interfaces;

public interface IExcelImportParser
{
    ParsedImportFile Parse(Stream fileStream);
}
