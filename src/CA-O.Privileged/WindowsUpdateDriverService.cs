using System.Runtime.InteropServices;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;

namespace CAO.Privileged;

/// <summary>
/// Drivers vía Windows Update (WUApi COM), el equivalente honesto y firmado
/// por Microsoft a "descargar drivers": busca ofertas Type='Driver',
/// descarga e instala solo las que el usuario elige. Corre en el servicio
/// (SYSTEM); nunca lanza (salvo cancelación).
/// </summary>
internal sealed class WindowsUpdateDriverService
{
    public const int MaxUpdates = 50;

    public sealed record SearchOutcome(bool Success, string MessageEs, IReadOnlyList<DriverUpdateInfo> Updates);

    public sealed record InstallOutcome(
        bool Success, string MessageEs, int Installed, int Failed, bool RebootRequired);

    public async Task<SearchOutcome> SearchAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            object? session = null;
            try
            {
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("Agente de Windows Update no disponible en este equipo.");
                session = Activator.CreateInstance(sessionType)
                    ?? throw new InvalidOperationException("No se pudo iniciar el agente de Windows Update.");
                dynamic dynSession = session;
                dynSession.ClientApplicationID = "CA-O Driver Update";
                dynamic searcher = dynSession.CreateUpdateSearcher();
                searcher.Online = true; // resultados reales, no caché (tarda minutos)
                ct.ThrowIfCancellationRequested();
                dynamic result = searcher.Search("IsInstalled=0 and Type='Driver'");
                var updates = new List<DriverUpdateInfo>();
                int count = (int)result.Updates.Count;
                for (int i = 0; i < count && updates.Count < MaxUpdates; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        dynamic update = result.Updates[i];
                        updates.Add(WindowsUpdateDrivers.MapUpdateInfo(update));
                    }
                    catch { /* una oferta rota no tumba la lista */ }
                }
                return updates.Count == 0
                    ? new SearchOutcome(true, "Windows Update no ofrece drivers para este equipo. Todo al día.", updates)
                    : new SearchOutcome(true, $"{updates.Count} actualizaciones de driver disponibles.", updates);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new SearchOutcome(false, WindowsUpdateDrivers.DescribeFailure(ex), Array.Empty<DriverUpdateInfo>());
            }
            finally
            {
                ReleaseCom(session);
            }
        }, ct);
    }

    public async Task<InstallOutcome> InstallAsync(IReadOnlyList<string> updateIds, CancellationToken ct = default)
    {
        var wanted = (updateIds ?? Array.Empty<string>())
            .Where(id => CAO.Shared.Security.CommandPolicy.IsValidWindowsUpdateId(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxUpdates)
            .ToList();
        if (wanted.Count == 0)
            return new InstallOutcome(false, "Sin IDs válidos para instalar.", 0, 0, false);

        return await Task.Run(() =>
        {
            object? session = null;
            try
            {
                // Punto de restauración best-effort (Windows limita la frecuencia).
                string rpNote;
                try
                {
                    var (created, reason) = new WmiRestorePointService()
                        .CreateAsync("CA-O: actualizar drivers (Windows Update)", ct).GetAwaiter().GetResult();
                    rpNote = created ? "Punto de restauración creado. " : $"Sin punto de restauración ({reason}). ";
                }
                catch { rpNote = "Sin punto de restauración (no disponible). "; }

                ct.ThrowIfCancellationRequested();
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("Agente de Windows Update no disponible.");
                session = Activator.CreateInstance(sessionType)
                    ?? throw new InvalidOperationException("No se pudo iniciar el agente de Windows Update.");
                dynamic dynSession = session;
                dynSession.ClientApplicationID = "CA-O Driver Update";

                // Re-buscar y cruzar: solo se instala lo que WU siga ofertando.
                dynamic searcher = dynSession.CreateUpdateSearcher();
                searcher.Online = true;
                dynamic searchResult = searcher.Search("IsInstalled=0 and Type='Driver'");
                var offered = new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);
                int total = (int)searchResult.Updates.Count;
                for (int i = 0; i < total; i++)
                {
                    try
                    {
                        dynamic update = searchResult.Updates[i];
                        string id = (string)update.Identity.UpdateID;
                        if (wanted.Contains(id, StringComparer.OrdinalIgnoreCase) && !offered.ContainsKey(id))
                            offered[id] = update;
                    }
                    catch { }
                }
                if (offered.Count == 0)
                    return new InstallOutcome(false, rpNote + "Los drivers elegidos ya no están ofertados (quizá se instalaron). Re-busca.", 0, 0, false);

                ct.ThrowIfCancellationRequested();
                dynamic collection = dynSession.CreateUpdateCollection();
                foreach (var update in offered.Values) collection.Add(update);

                dynamic downloader = dynSession.CreateUpdateDownloader();
                downloader.Updates = collection;
                downloader.Download();
                ct.ThrowIfCancellationRequested();

                dynamic installer = dynSession.CreateUpdateInstaller();
                installer.Updates = collection;
                installer.AllowSourcePrompts = false;
                dynamic installResult = installer.Install();

                int installed = 0, failed = 0;
                bool reboot = false;
                try { reboot = (bool)installResult.RebootRequired; } catch { }
                for (int i = 0; i < collection.Count; i++)
                {
                    try
                    {
                        int code = (int)installResult.GetUpdateResult(i).ResultCode;
                        if (code is 2 or 3) installed++;
                        else failed++;
                    }
                    catch { failed++; }
                }
                try { reboot = reboot || WindowsUpdateDrivers.AnyRebootRequired(collection); } catch { }

                var msg = rpNote + $"Instalados {installed} de {offered.Count}" +
                    (failed > 0 ? $", {failed} fallaron" : string.Empty) + "." +
                    (reboot ? " REINICIA el equipo para completar." : string.Empty);
                return failed > 0 && installed == 0
                    ? new InstallOutcome(false, msg, installed, failed, reboot)
                    : new InstallOutcome(true, msg, installed, failed, reboot);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new InstallOutcome(false, WindowsUpdateDrivers.DescribeFailure(ex), 0, 0, false);
            }
            finally
            {
                ReleaseCom(session);
            }
        }, ct);
    }

    private static void ReleaseCom(object? com)
    {
        if (com is null) return;
        try
        {
            if (Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com);
        }
        catch { }
    }
}
