namespace Genie.Core.Skills;

/// <summary>
/// The canonical DragonRealms skill names, exactly as DR emits them in
/// <c>&lt;component id='exp &lt;Skill&gt;'/&gt;</c> experience updates — 53 skills,
/// captured from a live session (see
/// <c>test_results/naper_session_findings.md</c>).
///
/// <para>
/// Populates the Edit Exit dialog's skill dropdown so map authors pick a real
/// skill name instead of free-typing one. The pathfinder's
/// <see cref="SkillStore"/> match is case-insensitive but exact on the name, so
/// a typo ("Athetics") would silently never gate — the dropdown removes that
/// whole failure mode and makes community data entry faster and consistent.
/// </para>
/// </summary>
public static class DrSkills
{
    /// <summary>All 53 skill names, alphabetical.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Alchemy", "Appraisal", "Arcana", "Athletics", "Attunement",
        "Augmentation", "Bow", "Brawling", "Brigandine", "Chain Armor",
        "Crossbow", "Debilitation", "Defending", "Empathy", "Enchanting",
        "Engineering", "Evasion", "First Aid", "Forging", "Heavy Thrown",
        "Large Blunt", "Large Edged", "Life Magic", "Light Armor",
        "Light Thrown", "Locksmithing", "Mechanical Lore", "Melee Mastery",
        "Missile Mastery", "Offhand Weapon", "Outdoorsmanship", "Outfitting",
        "Parry Ability", "Perception", "Performance", "Plate Armor",
        "Polearms", "Scholarship", "Shield Usage", "Skinning", "Slings",
        "Small Blunt", "Small Edged", "Sorcery", "Staves", "Stealth",
        "Tactics", "Targeted Magic", "Thievery", "Twohanded Blunt",
        "Twohanded Edged", "Utility", "Warding",
    };
}
