namespace BeneditaUI;

/// <summary>
/// Helper para resolver serviços do DI container fora do contexto de injeção.
/// Usado por Shell para obter páginas e VMs do DI.
/// </summary>
public static class ServiceHelper
{
    public static IServiceProvider Services =>
        IPlatformApplication.Current!.Services;

    public static T GetService<T>() where T : notnull =>
        Services.GetRequiredService<T>();
}
