using System;
using System.Threading.Tasks;

namespace Assistant.WinUI.Application.Auth;

public interface IAuthWorkflowService
{
    Task HandleProtocolActivationAsync(Uri uri);
}
