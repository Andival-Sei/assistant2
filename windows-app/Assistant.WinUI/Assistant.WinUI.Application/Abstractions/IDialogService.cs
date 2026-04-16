using System.Threading.Tasks;

namespace Assistant.WinUI.Application.Abstractions;

public interface IDialogService
{
    Task ShowMessageAsync(string title, string body);
}
