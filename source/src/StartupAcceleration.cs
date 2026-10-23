using System;
using System.IO;
using System.Runtime;

namespace CommanderCZ
{
    internal static class StartupAcceleration
    {
        internal static void Begin(string directory = null)
        {
            try
            {
                directory = directory ?? System.IO.Path.Combine(AppPaths.DataDirectory, "jit");
                Directory.CreateDirectory(directory); ProfileOptimization.SetProfileRoot(directory);
                ProfileOptimization.StartProfile(typeof(StartupAcceleration).Assembly.ManifestModule.ModuleVersionId.ToString("N") + ".profile");
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { } catch (System.Security.SecurityException) { }
        }
    }
}

