using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bindrune.Standards
{
    /// <summary>
    /// Bindrune touches the player's computer only through its own text files: it opens nothing
    /// outside the game and starts no program. A browser or folder the game opened would also
    /// keep Steam showing Valheim as running after it quits, as Steam counts every process the
    /// game starts as part of it.
    /// </summary>
    public class ShellStandardTests
    {
        [Fact]
        public void NothingOpensOutsideTheGame()
        {
            var found = new List<string>();
            foreach (var (call, model) in BindruneSource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString();
                if ((owner == "UnityEngine.Application" && method.Name == "OpenURL") || (owner == "System.Diagnostics.Process" && method.Name == "Start"))
                    found.Add($"{BindruneSource.Where(call)} {method.Name} in {BindruneSource.TopType(call)}.{BindruneSource.Member(call)}");
            }
            foreach (var (made, model) in BindruneSource.All<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(made).Type?.ToDisplayString() != "System.Diagnostics.ProcessStartInfo") continue;
                found.Add($"{BindruneSource.Where(made)} ProcessStartInfo in {BindruneSource.TopType(made)}.{BindruneSource.Member(made)}");
            }
            Violations.None("open something outside the game or start a program", found);
        }
    }

    /// <summary>
    /// Bindrune's timers (waiting for the game to load its controls, sampling the situation for
    /// the hints, a notice's delay) keep real time, which runs on while the game is paused: a
    /// single-player game stops its own clock in the menu, where the panel is used most, and a
    /// timer on it would never fire there. Nothing of Bindrune's moves with the world.
    /// </summary>
    public class ClockStandardTests
    {
        [Fact]
        public void TimersKeepRealTime()
        {
            var found = new List<string>();
            foreach (var (access, model) in BindruneSource.All<MemberAccessExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(access).Symbol is IPropertySymbol property) || property.ContainingType?.ToDisplayString() != "UnityEngine.Time") continue;
                var name = property.Name;
                if (name != "time" && name != "deltaTime" && name != "timeAsDouble" && name != "fixedTime" && name != "smoothDeltaTime") continue;
                found.Add($"{BindruneSource.Where(access)} Time.{name} in {BindruneSource.TopType(access)}.{BindruneSource.Member(access)}");
            }
            Violations.None("keep the game's time, which stops while it is paused", found);
        }
    }
}
