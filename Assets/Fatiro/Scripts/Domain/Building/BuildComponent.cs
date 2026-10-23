using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>
    /// A building component placed in one go, the way a site crew works: "Strip footings",
    /// "Ground-floor walls", "Roof sheeting". The solver still sees every member inside it.
    /// </summary>
    public sealed class BuildComponent
    {
        public BuildComponent(string name, IReadOnlyList<MemberSpec> members)
        {
            if (members == null || members.Count == 0) throw new ArgumentException($"Component {name} has no members.");
            Name = name;
            Members = members;
            Stage = members[0].Stage;
            Required = members[0].Required;
            foreach (var member in members)
            {
                if (member.Stage != Stage)
                    throw new ArgumentException($"{member.Id} is in a different stage from the rest of {name}.");
                if (member.Required != Required)
                    throw new ArgumentException($"{member.Id} mixes required and optional members in {name}.");
            }
        }

        public string Name { get; }
        public BuildStage Stage { get; }
        public bool Required { get; }
        public IReadOnlyList<MemberSpec> Members { get; }

        /// <summary>What the crew lays, e.g. "6 brick walls, 2 columns".</summary>
        public string Summary
        {
            get
            {
                var counts = new List<(string piece, int count)>();
                foreach (var member in Members)
                {
                    var piece = EnumText.PieceName(member);
                    var index = counts.FindIndex(entry => entry.piece == piece);
                    if (index < 0) counts.Add((piece, 1));
                    else counts[index] = (piece, counts[index].count + 1);
                }

                var parts = new List<string>();
                foreach (var (piece, count) in counts)
                    parts.Add(count == 1 ? piece.ToLowerInvariant() : $"{count} {Plural(piece.ToLowerInvariant())}");
                var text = string.Join(", ", parts);
                return char.ToUpperInvariant(text[0]) + text.Substring(1);
            }
        }

        /// <summary>Engineering note from the first member that has one.</summary>
        public string Hint
        {
            get
            {
                foreach (var member in Members)
                    if (!string.IsNullOrEmpty(member.Hint)) return member.Hint;
                return string.Empty;
            }
        }

        public bool Contains(string memberId)
        {
            foreach (var member in Members)
                if (member.Id == memberId) return true;
            return false;
        }

        private static string Plural(string piece)
        {
            if (piece.EndsWith("ing") || piece.EndsWith("glazing") || piece.EndsWith("tiles")) return piece;
            if (piece.EndsWith("s") || piece.EndsWith("ch")) return piece + "es";
            return piece + "s";
        }
    }
}
