using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Player;

namespace Nodin.Editor.Tests
{
    public sealed class PlayerIsolationTests
    {
        [Test]
        public void PlayerBuildFiltersTheAttributeAssembly()
        {
            var assemblies = new[] { "Assembly-CSharp.dll", "Nodin.dll", "Nodin.Game.dll" };
            var filtered = new NodinPlayerIsolation().OnFilterAssemblies(BuildOptions.None, assemblies);
            Assert.That(filtered, Is.EqualTo(new[] { "Assembly-CSharp.dll", "Nodin.Game.dll" }));
        }

        [Test]
        [Explicit("Compiles scripts for the active build target and reads the resulting managed DLLs.")]
        public void CompiledPlayerScriptsExcludeNodin()
        {
            var directory = Path.Combine(Path.GetTempPath(), "Nodin-player-isolation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var target = EditorUserBuildSettings.activeBuildTarget;
                var settings = new ScriptCompilationSettings
                {
                    target = target,
                    group = BuildPipeline.GetBuildTargetGroup(target),
                };
                var result = PlayerBuildInterface.CompilePlayerScripts(settings, directory);
                var paths = new NodinPlayerIsolation().OnFilterAssemblies(BuildOptions.None, result.assemblies.ToArray())
                    .Select(assembly => Path.Combine(directory, assembly));
                Assert.That(NodinPlayerIsolation.ValidateCompiledPlayerAssemblies(paths), Is.Positive);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
