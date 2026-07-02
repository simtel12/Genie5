using Genie.Core.Mapper;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public #122 — travel over-weighted swims/climbs for skilled characters, so it
/// chose a ferry when a 957-Athletics character should have swum the Faldesu.
/// The swim/climb effort penalty now decays with Athletics rank
/// (<see cref="AutoMapperEngine.EffortPenalty"/>).
/// </summary>
public class EffortWeightingTests
{
    [Theory]
    // Swim (base 8): full at low/unknown skill, decaying to 0 by rank 600.
    [InlineData("swim west", 0, 8)]
    [InlineData("swim west", 300, 4)]
    [InlineData("swim west", 600, 0)]
    [InlineData("swim west", 957, 0)]      // the #122 character — swim is now "free"
    // Natural climb (base 6): same decay.
    [InlineData("climb cliff", 0, 6)]
    [InlineData("climb cliff", 300, 3)]
    [InlineData("climb cliff", 957, 0)]
    // Built structures: never penalised, regardless of skill.
    [InlineData("climb stairs", 0, 0)]
    [InlineData("climb ladder", 0, 0)]
    [InlineData("climb steep steps", 0, 0)]
    // Flat moderate group: NOT Athletics-scaled.
    [InlineData("ford stream", 999, 4)]
    [InlineData("dive pool", 999, 4)]
    [InlineData("wade across", 999, 4)]
    // Ordinary moves: no penalty.
    [InlineData("go north", 500, 0)]
    [InlineData("north", 0, 0)]
    [InlineData("", 0, 0)]
    public void EffortPenalty_ScalesSwimAndClimbByAthletics(string move, int athletics, int expected)
    {
        Assert.Equal(expected, AutoMapperEngine.EffortPenalty(move, athletics));
    }

    [Fact]
    public void EffortPenalty_HighAthleticsSwim_BeatsShortFerryWait()
    {
        // The crux of #122: at high Athletics a swim should cost less than idling
        // on a ferry. Swim edge ≈ 1 (baseline, penalty 0); a ferry's scheduled
        // wait adds cost on top of its own baseline, so the swim wins.
        var swimPenalty = AutoMapperEngine.EffortPenalty("swim west", 957);
        Assert.Equal(0, swimPenalty);
    }
}
