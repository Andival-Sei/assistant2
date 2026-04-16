using System.Threading.Tasks;

namespace Assistant.WinUI.Application.Abstractions;

public interface IMediaCaptureService
{
    Task<string?> CapturePhotoAsync();
}
