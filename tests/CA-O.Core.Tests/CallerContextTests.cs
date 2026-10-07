using CAO.Core.Abstractions;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// CAO-BUG-2026-10-06 (F1). La suplantacion de hilo de WindowsIdentity.RunImpersonated
/// se retira cuando el delegado DEVELUELVE su Task, no cuando la Task completa. En un
/// metodo asincrono el delegado devuelve la Task en el primer await, de modo que todas
/// las continuaciones posteriores se ejecutan ya como SYSTEM. Por eso el SID del
/// llamante viaja en un AsyncLocal, que si se propaga con las continuaciones y ademas
/// se aisla entre despachos concurrentes (el gate del servicio admite cuatro).
/// </summary>
public sealed class CallerContextTests
{
    [Fact]
    public async Task CurrentUserSid_SurvivesAnAwait()
    {
        var before = CallerContext.CurrentUserSid;
        try
        {
            CallerContext.CurrentUserSid = "S-1-5-21-1-2-3-1001";

            await Task.Yield();
            await Task.Delay(10);

            Assert.Equal("S-1-5-21-1-2-3-1001", CallerContext.CurrentUserSid);
        }
        finally
        {
            CallerContext.CurrentUserSid = before;
        }
    }

    [Fact]
    public async Task CurrentUserSid_IsIsolatedBetweenConcurrentBranches()
    {
        var before = CallerContext.CurrentUserSid;
        try
        {
            CallerContext.CurrentUserSid = "S-1-5-21-1-2-3-1001";

            var reader = Task.Run(async () =>
            {
                await Task.Yield();
                return CallerContext.CurrentUserSid;
            });
            var writer = Task.Run(async () =>
            {
                await Task.Yield();
                CallerContext.CurrentUserSid = "S-1-5-21-9-9-9-500";
                await Task.Yield();
                return CallerContext.CurrentUserSid;
            });

            await Task.WhenAll(reader, writer);

            // La rama que solo lee no debe ver lo que escribio la otra: si el
            // almacen fuera un estatico plano, el servicio despachando cuatro
            // peticiones a la vez resolveria el hive del usuario equivocado.
            Assert.Equal("S-1-5-21-1-2-3-1001", await reader);

            // Y el valor de este hilo sigue siendo el suyo.
            Assert.Equal("S-1-5-21-1-2-3-1001", CallerContext.CurrentUserSid);
            Assert.Equal("S-1-5-21-9-9-9-500", await writer);
        }
        finally
        {
            CallerContext.CurrentUserSid = before;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CurrentUserSid_TreatsBlankAsAbsent(string? value)
    {
        var before = CallerContext.CurrentUserSid;
        try
        {
            CallerContext.CurrentUserSid = value;

            Assert.Null(CallerContext.CurrentUserSid);
        }
        finally
        {
            CallerContext.CurrentUserSid = before;
        }
    }

    [Fact]
    public void CurrentUserSid_TrimsAndNormalizes()
    {
        var before = CallerContext.CurrentUserSid;
        try
        {
            CallerContext.CurrentUserSid = "  S-1-5-21-7-7-7-1001  ";
            Assert.Equal("S-1-5-21-7-7-7-1001", CallerContext.CurrentUserSid);

            CallerContext.CurrentUserSid = null;
            Assert.Null(CallerContext.CurrentUserSid);
        }
        finally
        {
            CallerContext.CurrentUserSid = before;
        }
    }
}