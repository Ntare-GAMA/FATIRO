using System;
using System.Collections.Generic;

namespace Fatiro.Domain
{
    /// <summary>
    /// Placed members and the load paths between them. A member's supports are the members it
    /// rests on; its dependents are the members resting on it. Braces stiffen the members they connect.
    /// </summary>
    public sealed class ConnectionGraph
    {
        private readonly Dictionary<string, StructuralMember> members = new Dictionary<string, StructuralMember>();
        private readonly List<StructuralMember> ordered = new List<StructuralMember>();

        public int Count => members.Count;
        public IReadOnlyList<StructuralMember> Members => ordered;

        public void Add(StructuralMember member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (members.ContainsKey(member.Id)) throw new InvalidOperationException($"{member.Id} is already placed.");
            members.Add(member.Id, member);
            ordered.Add(member);
        }

        public bool Remove(string memberId)
        {
            if (!members.TryGetValue(memberId, out var member)) return false;
            members.Remove(memberId);
            ordered.Remove(member);
            return true;
        }

        public bool Contains(string memberId) => members.ContainsKey(memberId);

        public StructuralMember Get(string memberId) => members.TryGetValue(memberId, out var member) ? member : null;

        public void Clear()
        {
            members.Clear();
            ordered.Clear();
        }

        /// <summary>Placed members this one rests on, optionally including props, filtered by isActive.</summary>
        public List<StructuralMember> SupportsOf(StructuralMember member, Func<StructuralMember, bool> isActive, bool includeExtra = true)
        {
            var result = new List<StructuralMember>();
            AddActive(member.Spec.Supports, isActive, result);
            if (includeExtra) AddActive(member.Spec.ExtraSupports, isActive, result);
            return result;
        }

        /// <summary>Placed members resting on this one.</summary>
        public List<StructuralMember> DependentsOf(string memberId)
        {
            var result = new List<StructuralMember>();
            foreach (var member in ordered)
            {
                if (Array.IndexOf(member.Spec.Supports, memberId) >= 0 || Array.IndexOf(member.Spec.ExtraSupports, memberId) >= 0)
                    result.Add(member);
            }
            return result;
        }

        /// <summary>True when an active brace connects to this member.</summary>
        public bool IsBraced(StructuralMember member, Func<StructuralMember, bool> isActive)
        {
            foreach (var candidate in ordered)
            {
                if (candidate.Kind != MemberKind.Brace || !isActive(candidate)) continue;
                if (Array.IndexOf(candidate.Spec.Supports, member.Id) >= 0) return true;
            }
            return false;
        }

        /// <summary>True when an optional extra support (prop) under this member is active.</summary>
        public bool IsPropped(StructuralMember member, Func<StructuralMember, bool> isActive)
        {
            foreach (var id in member.Spec.ExtraSupports)
            {
                var prop = Get(id);
                if (prop != null && isActive(prop)) return true;
            }
            return false;
        }

        /// <summary>
        /// Orders members so each one comes before the members that support it. Loads can then be
        /// pushed down the structure in one pass, from roof to foundations.
        /// </summary>
        public List<StructuralMember> TopDownOrder(Func<StructuralMember, bool> isActive)
        {
            var depth = new Dictionary<string, int>();
            var visiting = new HashSet<string>();
            foreach (var member in ordered)
                if (isActive(member)) DepthOf(member, isActive, depth, visiting);

            var result = new List<StructuralMember>();
            foreach (var member in ordered)
                if (isActive(member)) result.Add(member);
            result.Sort((left, right) => depth[right.Id].CompareTo(depth[left.Id]));
            return result;
        }

        private int DepthOf(StructuralMember member, Func<StructuralMember, bool> isActive,
            Dictionary<string, int> depth, HashSet<string> visiting)
        {
            if (depth.TryGetValue(member.Id, out var known)) return known;
            if (!visiting.Add(member.Id)) throw new InvalidOperationException($"Support cycle detected at {member.Id}.");

            var deepest = 0;
            foreach (var support in SupportsOf(member, isActive))
                deepest = Math.Max(deepest, DepthOf(support, isActive, depth, visiting) + 1);

            visiting.Remove(member.Id);
            depth[member.Id] = deepest;
            return deepest;
        }

        private void AddActive(string[] ids, Func<StructuralMember, bool> isActive, List<StructuralMember> result)
        {
            foreach (var id in ids)
            {
                var support = Get(id);
                if (support != null && isActive(support)) result.Add(support);
            }
        }
    }
}
