using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Genie.Core.Tests;

/// <summary>
/// Genie.Core is a self-contained Exe referenced as a bare DLL (HintPath), so
/// the runtime can't probe for it or its dependencies. Register an assembly
/// resolver that loads them from Core's build output (passed in via the
/// <c>CoreOutDir</c> assembly-metadata the csproj stamps).
///
/// This MUST run before any test method that touches a Genie.Core type is
/// JIT-compiled. A [ModuleInitializer] is guaranteed to run before any other
/// code in this module executes, and it references no Genie.Core types itself —
/// so the handler is in place by the time the first test resolves Genie.Core.
/// </summary>
internal static class ModuleInit
{
    [ModuleInitializer]
    public static void Init()
    {
        var coreDir = typeof(ModuleInit).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "CoreOutDir")?.Value;
        if (string.IsNullOrEmpty(coreDir)) return;

        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            var path = Path.Combine(coreDir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
    }
}
