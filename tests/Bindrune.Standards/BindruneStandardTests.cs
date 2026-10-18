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
    /// A bind's id is opaque. <c>BindIds</c> builds every one (<c>cfg:</c>, <c>jotunn:</c>,
    /// <c>vanilla:</c> and the rest) and nothing takes one apart again, so the shared list, your
    /// keys and the mutes, which all store ids, can never disagree about how one is spelled. A
    /// prefix written anywhere else is an id made by hand.
    /// </summary>
    public class BindIdStandardTests
    {
        private static readonly string[] Prefixes = { "cfg:", "jotunn:", "vanilla:", "game:", "gamepad:" };

        [Fact]
        public void OnlyBindIdsSpellsABindId()
        {
            var found = new List<string>();
            foreach (var tree in BindruneSource.Compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                if (tree.FilePath.EndsWith("BindIds.cs", StringComparison.Ordinal)) continue;
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    var text = node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText
                        : node is InterpolatedStringTextSyntax part ? part.TextToken.ValueText : null;
                    if (text == null) continue;
                    if (Prefixes.Any(p => text.StartsWith(p, StringComparison.Ordinal))) found.Add($"{BindruneSource.Where(node)} \"{text}\"");
                }
            }
            Violations.None("spell a bind id outside BindIds", found);
        }

        [Fact]
        public void BindIdsUsesThePrefixesThisTestKnows()
        {
            // Each id starts with a literal, or with the first text of an interpolated string.
            var ids = BindruneSource.Compilation.SyntaxTrees.Single(t => t.FilePath.EndsWith("BindIds.cs", StringComparison.Ordinal));
            var starts = ids.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression)).Select(l => l.Token.ValueText)
                .Concat(ids.GetRoot().DescendantNodes().OfType<InterpolatedStringExpressionSyntax>()
                    .Select(s => s.Contents.FirstOrDefault()).OfType<InterpolatedStringTextSyntax>().Select(t => t.TextToken.ValueText))
                .Where(t => t.Length > 1 && t.EndsWith(":", StringComparison.Ordinal))
                .ToList();
            Assert.Equal(Prefixes.OrderBy(p => p, StringComparer.Ordinal), starts.Distinct().OrderBy(p => p, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Where Bindrune changes the game, each patch finds its own target (<c>AccessTools</c>) and
    /// turns itself off with a warning when a game update moved it. Patching by attribute, or
    /// all at once with <c>PatchAll</c>, throws for a missing target and would take the whole
    /// plugin down over one patch.
    /// </summary>
    public class PatchStandardTests
    {
        [Fact]
        public void EveryPatchIsAppliedByHandAndCanFailAlone()
        {
            var found = new List<string>();
            foreach (var (attribute, model) in BindruneSource.All<AttributeSyntax>())
            {
                var type = model.GetTypeInfo(attribute).Type?.ToDisplayString() ?? "";
                if (type.StartsWith("HarmonyLib.Harmony", StringComparison.Ordinal) && type != "HarmonyLib.HarmonyPriority")
                    found.Add($"{BindruneSource.Where(attribute)} [{attribute.Name}]");
            }
            foreach (var (call, model) in BindruneSource.All<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method && method.Name == "PatchAll" && method.ContainingType?.Name == "Harmony")
                    found.Add($"{BindruneSource.Where(call)} PatchAll");
            }
            Violations.None("patch by attribute or all at once", found);
        }
    }

    /// <summary>
    /// Bindrune writes its files one way, <c>TextStore.Write</c>, which makes the folder and,
    /// for what is yours, writes aside and swaps the file in, so a crash mid-write never leaves
    /// half a file where your keys were. The few other writers each hold a file that is not
    /// Bindrune's own text: Keepsake's list, which Bindrune changes only as Keepsake writes it;
    /// the spare copy outside the profile; and an old file removed once its replacement is saved.
    /// </summary>
    public class FileWriteStandardTests
    {
        private static readonly Dictionary<string, string> Writers = new Dictionary<string, string>
        {
            ["TextStore.cs"] = "the one writer of Bindrune's text files",
            ["KeepsakeHandover.cs"] = "takes your keybinds out of keepsake.pins, aside and swapped in, as Keepsake writes it",
            ["SpareCopy.cs"] = "copies bindrune.keys and bindrune.spare to the spare copy outside the profile and back",
            ["SpareFolder.cs"] = "removes the spare copy outside the profile, and the emptied folder that held it, when you turn it off",
            ["PersonalStore.cs"] = "removes a file from before 0.2.1 once what it held is in bindrune.keys",
        };

        private static readonly HashSet<string> Writes = new HashSet<string>
        {
            "WriteAllText", "WriteAllLines", "WriteAllBytes", "AppendAllText", "AppendAllLines", "AppendText", "CreateText",
            "Create", "Copy", "Move", "Replace", "Delete", "Open", "OpenWrite",
        };

        [Fact]
        public void FilesAreWrittenByTheirOneWriter()
        {
            var found = new List<string>();
            foreach (var (call, model) in BindruneSource.In<InvocationExpressionSyntax>(new[] { BindruneSource.Compilation }))
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString();
                if ((owner != "System.IO.File" && owner != "System.IO.Directory") || !Writes.Contains(method.Name)) continue;
                if (owner == "System.IO.Directory" && method.Name != "Delete" && method.Name != "Move") continue;
                if (Writers.ContainsKey(System.IO.Path.GetFileName(call.SyntaxTree.FilePath))) continue;
                found.Add($"{BindruneSource.Where(call)} {owner}.{method.Name} in {BindruneSource.TopType(call)}.{BindruneSource.Member(call)}");
            }
            foreach (var (made, model) in BindruneSource.In<BaseObjectCreationExpressionSyntax>(new[] { BindruneSource.Compilation }))
            {
                var type = model.GetTypeInfo(made).Type?.ToDisplayString();
                if (type != "System.IO.StreamWriter" && type != "System.IO.FileStream") continue;
                if (Writers.ContainsKey(System.IO.Path.GetFileName(made.SyntaxTree.FilePath))) continue;
                found.Add($"{BindruneSource.Where(made)} new {type} in {BindruneSource.TopType(made)}.{BindruneSource.Member(made)}");
            }
            Violations.None("write a file outside its one writer", found);
        }
    }
}
