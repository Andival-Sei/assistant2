using System.Threading.Tasks;

namespace Assistant.WinUI.Application.Abstractions;

public interface IFilePickerService
{
    Task<string?> PickFileAsync(bool photoOnly);
}
