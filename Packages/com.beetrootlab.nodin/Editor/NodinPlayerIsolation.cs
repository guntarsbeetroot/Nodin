using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Nodin.Editor
{
    internal sealed class NodinPlayerIsolation : IFilterBuildAssemblies, IPostBuildPlayerScriptDLLs, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            // The C# compiler needs the definitions to bind conditional attributes.
            return assemblies.Where(assembly => !IsAttributeAssembly(assembly)).ToArray();
        }

        public void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            var paths = report.GetFiles().Select(file => file.path)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !IsAttributeAssembly(path));
            ValidateCompiledPlayerAssemblies(paths);
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null && IsPackageAsset(scene.path))
                throw new BuildFailedException($"Nodin package scenes are for Editor use: {scene.path}");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            var includedAssembly = report.GetFiles().Select(file => file.path)
                .FirstOrDefault(path => Path.GetFileName(path) == "Nodin.dll");
            if (includedAssembly != null)
                throw new BuildFailedException($"Nodin assembly in player output: {includedAssembly}");

            var includedAsset = report.packedAssets.SelectMany(pack => pack.contents)
                .Select(asset => asset.sourceAssetPath).FirstOrDefault(IsPackageAsset);
            if (includedAsset != null)
                throw new BuildFailedException($"The player contains a Nodin asset: {includedAsset}");
        }

        internal static int ValidateCompiledPlayerAssemblies(IEnumerable<string> paths)
        {
            var count = 0;
            foreach (var path in paths)
            {
                using var assembly = AssemblyDefinition.ReadAssembly(path);
                if (IsPackageAssembly(assembly.Name.Name))
                    throw new BuildFailedException($"Nodin assembly in player output: {path}");
                foreach (var module in assembly.Modules)
                {
                    if (module.AssemblyReferences.Any(reference => IsPackageAssembly(reference.Name)))
                        throw new BuildFailedException(
                            $"Player assembly '{assembly.Name.Name}' references Nodin at runtime. " +
                            "Move uses of Nodin types outside attributes into Editor-only code.");
                }
                count++;
            }
            return count;
        }

        private static bool IsAttributeAssembly(string path) => Path.GetFileName(path) is "Nodin" or "Nodin.dll";

        private static bool IsPackageAssembly(string name) =>
            name is "Nodin" or "Nodin.Editor" or "Nodin.Editor.Tests" or "Nodin.Tests.Fixtures";

        private static bool IsPackageAsset(string path) =>
            path?.StartsWith("Packages/com.beetrootlab.nodin/", StringComparison.Ordinal) == true;
    }
}
