using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bindrune.Standards
{
    /// <summary>
    /// A release packages Bindrune.dll alone, so a player installing Bindrune gets nothing of the
    /// self-test. The self-test is a plugin of its own, <c>Bindrune.SelfTest.dll</c>, which only a
    /// development build installs; it brings its command, its runner and its files, and reaches
    /// Bindrune's insides through <c>InternalsVisibleTo</c>. Installing it is what turns the
    /// self-test on, so Bindrune itself names no self-test type, setting, command or file.
    /// </summary>
    public class SelfTestContainmentTests
    {
        [Fact]
        public void BindruneAloneHoldsNothingOfTheSelfTest()
        {
            var found = Declared(BindruneSource.Compilation)
                .Where(t => t.Name.IndexOf("SelfTest", StringComparison.Ordinal) >= 0)
                .Select(t => $"{t.Where} {t.Name}");
            Violations.None("hold a part of the self-test in Bindrune", found);
        }

        [Fact]
        public void BindruneAloneNamesNoSelfTestSettingCommandOrFile()
        {
            var found = Literals(BindruneSource.Compilation)
                .Where(l => l.Text.IndexOf("selftest", StringComparison.OrdinalIgnoreCase) >= 0 || l.Text.IndexOf("self-test", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(l => $"{l.Where} \"{l.Text}\"");
            Violations.None("name a self-test setting, command or file in Bindrune", found);
        }

        [Fact]
        public void TheSelfTestPluginBringsItsCommand()
        {
            var types = Declared(BindruneSource.SelfTest).Select(t => t.Name).ToList();
            Assert.Contains("SelfTestPlugin", types);
            Assert.Contains(Literals(BindruneSource.SelfTest), l => l.Text.Equals("selftest", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void AReleaseTakesBindruneDllAlone()
        {
            // The plugin's own project leaves the self-test's folder out, and the release packs
            // the one DLL the plugin's build makes.
            var root = BindruneSource.Root();
            var project = File.ReadAllText(Path.Combine(root, "Bindrune.csproj"));
            Assert.Contains("<Compile Remove=\"selftest/**\" />", project, StringComparison.Ordinal);

            var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
            Assert.Contains("cp bin/Release/Bindrune.dll dist/package/", release, StringComparison.Ordinal);
            Assert.DoesNotContain("SelfTest", release, StringComparison.Ordinal);
        }

        /// <summary>Every type a compilation declares, nested ones too, with where.</summary>
        private static IEnumerable<(string Name, string Where)> Declared(CSharpCompilation compilation)
        {
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                foreach (var type in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()) yield return (type.Identifier.Text, BindruneSource.Where(type));
            }
        }

        /// <summary>Every string a compilation writes, plain or as the text of an interpolated one, with where.</summary>
        private static IEnumerable<(string Text, string Where)> Literals(CSharpCompilation compilation)
        {
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) yield return (literal.Token.ValueText, BindruneSource.Where(node));
                    else if (node is InterpolatedStringTextSyntax text) yield return (text.TextToken.ValueText, BindruneSource.Where(node));
                }
            }
        }
    }
}
