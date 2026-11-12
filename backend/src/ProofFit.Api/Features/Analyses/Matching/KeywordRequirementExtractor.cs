using System.Text.RegularExpressions;
using ProofFit.Api.Domain;

namespace ProofFit.Api.Features.Analyses.Matching;

/// <summary>
/// Finds requirements without a language model, from the layout of a typical posting:
/// headed sections of bullet points. It is the baseline the model-based extractor is
/// compared with, and what runs when no model is configured.
/// </summary>
public partial class KeywordRequirementExtractor : IRequirementExtractor
{
    public const int MaxRequirements = 40;

    private enum Section { Unknown, MustHave, NiceToHave, NotRequirements }

    // Order matters: "nice to have" headings often also contain "requirements" or "skills".
    private static readonly (Section Section, string[] Keywords)[] Headings =
    [
        (Section.NiceToHave,
        [
            "nice to have", "nice-to-have", "good to have", "a plus", "bonus", "preferred", "optional",
            "буде плюсом", "плюсом буде", "бажано",
        ]),
        (Section.NotRequirements,
        [
            "responsibilit", "what you will do", "what you'll do", "your role", "duties", "we offer",
            "what we offer", "benefits", "perks", "about us", "about the company", "about the project",
            "обов'язки", "обов’язки", "ми пропонуємо", "про нас", "про компанію", "умови",
        ]),
        (Section.MustHave,
        [
            "requirement", "must have", "must-have", "qualification", "we expect", "you have",
            "what you need", "about you", "who you are", "skills", "tech stack",
            "вимоги", "необхідні", "ми очікуємо", "навички",
        ]),
    ];

    public Task<IReadOnlyList<ExtractedRequirement>> ExtractAsync(NumberedText vacancy, CancellationToken ct)
    {
        var requirements = new List<ExtractedRequirement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var section = Section.Unknown;

        for (var index = 0; index < vacancy.Lines.Count && requirements.Count < MaxRequirements; index++)
        {
            var line = vacancy.Lines[index].Trim();
            if (line.Length == 0) continue;

            var isBullet = BulletPrefix().IsMatch(line);
            var text = isBullet ? BulletPrefix().Replace(line, "").Trim() : line;
            var namesSkill = SkillCatalog.FindIn(text).Count > 0;

            if (!isBullet && HeadingSection(line, namesSkill) is { } heading)
            {
                section = heading;
                continue;
            }

            if (section == Section.NotRequirements) continue;

            // Inside a requirements section every bullet is a requirement, named skill or
            // not ("3+ years of experience"). Anywhere else, prose included, a line has to
            // name a known skill to count.
            var inRequirementsSection = section is Section.MustHave or Section.NiceToHave;
            if (!namesSkill && !(isBullet && inRequirementsSection)) continue;

            if (text.Length == 0 || !seen.Add(text)) continue;

            var importance = section == Section.NiceToHave || NiceToHaveHint().IsMatch(text)
                ? Importance.NiceToHave
                : Importance.MustHave;
            requirements.Add(new ExtractedRequirement(text, importance, index + 1));
        }

        return Task.FromResult<IReadOnlyList<ExtractedRequirement>>(requirements);
    }

    // A heading is a short line that announces a section. A short line that names a skill
    // ("Stack: React, Node.js") is content unless it ends with a colon.
    private static Section? HeadingSection(string line, bool namesSkill)
    {
        if (line.Length > 60) return null;
        if (namesSkill && !line.EndsWith(':')) return null;

        foreach (var (section, keywords) in Headings)
        {
            if (keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return section;
        }
        return null;
    }

    [GeneratedRegex(@"^(?:[-–—•*·▪●◦]+|\d{1,2}[.)])\s*")]
    private static partial Regex BulletPrefix();

    [GeneratedRegex(
        @"\b(?:nice to have|is a plus|as a plus|a big plus|would be a plus|will be a plus|bonus|preferably|optional)\b|буде плюсом|бажано",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NiceToHaveHint();
}
