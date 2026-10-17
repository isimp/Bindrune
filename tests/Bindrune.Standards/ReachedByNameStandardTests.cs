using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bindrune.Standards
{
    /// <summary>
    /// What Bindrune reaches in the game or in Jotunn by its name, out of the compiler's sight,
    /// the self-test looks for in the running game, so an update that renames one is told
    /// rather than found as a feature that quietly stopped. Every name handed to
    /// <c>AccessTools</c> in Bindrune's source is on the self-test's list, written
    /// "Type.member", or ".member" after the path that leads to it where the type is found
    /// at run time.
    /// </summary>
    public class ReachedByNameStandardTests
    {
        private static readonly HashSet<string> Lookups = new HashSet<string>
        {
            "Method", "DeclaredMethod", "Field", "DeclaredField", "Property", "DeclaredProperty", "PropertyGetter", "PropertySetter", "FieldRefAccess",
        };

        [Fact]
        public void EveryMemberReachedByNameIsLookedForInGame()
        {
            var listed = new HashSet<string>(StringLiterals(BindruneSource.SelfTest), StringComparer.Ordinal);
            var bindrune = BindruneSource.Compilation.Assembly;

            var found = new List<string>();
            var asked = 0;
            foreach (var (call, model) in BindruneSource.In<InvocationExpressionSyntax>(new[] { BindruneSource.Compilation }))
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method) || method.ContainingType?.ToDisplayString() != "HarmonyLib.AccessTools" || !Lookups.Contains(method.Name)) continue;
                var args = call.ArgumentList.Arguments;
                if (args.Count < 2 || !(model.GetConstantValue(args[1].Expression).Value is string name)) continue;

                if (args[0].Expression is TypeOfExpressionSyntax typeOf)
                {
                    if (SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(typeOf.Type).Type?.ContainingAssembly, bindrune)) continue;
                    asked++;
                    var expected = typeOf.Type + "." + name;
                    if (!listed.Contains(expected)) found.Add($"{BindruneSource.Where(call)} {expected}");
                }
                else
                {
                    asked++;
                    if (!listed.Any(l => l.EndsWith("." + name, StringComparison.Ordinal))) found.Add($"{BindruneSource.Where(call)} ...{name}");
                }
            }

            Assert.True(asked > 10, "the names Bindrune reaches were not found in its source");
            Violations.None("reach a member by name that the self-test does not look for", found);
        }

        private static IEnumerable<string> StringLiterals(CSharpCompilation compilation) =>
            compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath))
                .SelectMany(t => t.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>())
                .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
                .Select(l => l.Token.ValueText);
    }
}
