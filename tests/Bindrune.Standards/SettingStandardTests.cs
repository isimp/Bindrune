using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bindrune.Standards
{
    /// <summary>
    /// Bindrune binds every setting it has in <c>Plugin.Awake</c>, before <c>Upkeep.Run</c>. Upkeep
    /// takes out of the settings file whatever is not bound by then, as a setting an earlier
    /// version had; a setting bound anywhere later would be taken out at every start, your value
    /// with it.
    /// </summary>
    public class SettingStandardTests
    {
        [Fact]
        public void EverySettingIsBoundInAwakeBeforeUpkeepRuns()
        {
            var found = new List<string>();
            var binds = 0;
            foreach (var (call, model) in BindruneSource.In<InvocationExpressionSyntax>(new[] { BindruneSource.Compilation }))
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method) || method.Name != "Bind" || method.ContainingType?.ToDisplayString() != "BepInEx.Configuration.ConfigFile") continue;
                binds++;

                var awake = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (awake == null || awake.Identifier.Text != "Awake" || BindruneSource.TopType(call) != "Plugin")
                {
                    found.Add($"{BindruneSource.Where(call)} bound in {BindruneSource.TopType(call)}.{BindruneSource.Member(call)}");
                    continue;
                }

                var upkeep = awake.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(i => model.GetSymbolInfo(i).Symbol is IMethodSymbol m && m.Name == "Run" && m.ContainingType?.Name == "Upkeep");
                if (upkeep == null || upkeep.SpanStart < call.SpanStart) found.Add($"{BindruneSource.Where(call)} bound after Upkeep.Run");
            }

            Assert.True(binds > 5, "Bindrune's settings were not found in its source");
            Violations.None("bind a setting where Upkeep would take it out", found);
        }
    }
}
