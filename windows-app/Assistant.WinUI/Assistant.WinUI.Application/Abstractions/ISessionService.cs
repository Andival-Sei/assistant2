using System.Threading.Tasks;

namespace Assistant.WinUI.Application.Abstractions;

public interface ISessionService
{
    bool HasSession { get; }

    Task<bool> RestoreAsync();
}
