using System;
using System.IO;

using CAO.Core.Optimizations.Storage;
using Xunit;

namespace CAO.Core.Tests;

public sealed class ProfileSubDirsCacheTests
{
    /// <summary>
    /// CAO-BUG-2026-10-06: cuatro subclases de la familia declaran
    /// <c>Targets =&gt; BuildTargets()</c>, de modo que la lista de objetivos se
    /// reconstruye en cada acceso a la propiedad. Un solo <c>Detect()</c> la lee
    /// dos veces (el guardia de "sin objetivos" y <c>PendingFiles()</c>) y cada
    /// lectura ejecuta <c>Directory.GetDirectories("C:\Users")</c> una vez por
    /// parte de la ruta. Para <c>cleanup-windows-temp</c> son cinco
    /// enumeraciones de la carpeta de usuarios por lectura, repetidas en cada
    /// apply, verify y analisis: trabajo de disco puro, sin resultado util
    /// nuevo, que ademas crece con el numero de perfiles de la maquina.
    /// </summary>
    [Fact]
    public void ProfileSubDirs_DoesNotRescanTheUsersFolder_ForEveryCall()
    {
        var scans = 0;
        var previousScanner = TempFileCleanupOptimization.ProfileRootScanner;
        var previousTtl = TempFileCleanupOptimization.ProfileCacheTtl;

        TempFileCleanupOptimization.ProfileRootScanner = root =>
        {
            scans++;
            return Directory.GetDirectories(root);
        };
        // TTL largo: dentro de una misma operacion el resultado debe usarse tal cual.
        TempFileCleanupOptimization.ProfileCacheTtl = TimeSpan.FromMinutes(5);
        TempFileCleanupOptimization.ResetProfileCache();

        try
        {
            var baseline = scans;

            _ = TempFileCleanupOptimization.ProfileSubDirs("AppData", "Local", "Temp");
            var warm = scans;

            for (var i = 0; i < 10; i++)
            {
                _ = TempFileCleanupOptimization.ProfileSubDirs("AppData", "Local", "Temp");
                _ = TempFileCleanupOptimization.ProfileSubDirs("AppData", "LocalLow", "Temp");
            }

            Assert.True(scans - baseline <= 1,
                "Como mucho una enumeracion de la carpeta de usuarios por vida de la cache; se han hecho " + scans);

            Assert.Equal(warm, scans);
        }
        finally
        {
            TempFileCleanupOptimization.ProfileRootScanner = previousScanner;
            TempFileCleanupOptimization.ProfileCacheTtl = previousTtl;
            TempFileCleanupOptimization.ResetProfileCache();
        }
    }
}