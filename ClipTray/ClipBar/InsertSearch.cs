using System;
using System.Collections.Generic;
using ClipTray.Models;

namespace ClipTray.ClipBar
{
    /// <summary>
    /// Ranks inserts against a typed query. Pure logic with no UI or I/O, so the
    /// ordering rules are unit tested directly.
    /// </summary>
    public static class InsertSearch
    {
        private const int ScoreExactTitle = 1000;
        private const int ScoreTitlePrefix = 900;
        private const int ScoreTitleWordStart = 800;
        private const int ScoreTitleContains = 700;
        private const int ScoreTitleSubsequence = 600;
        private const int ScoreBodyContains = 400;
        private const int NoMatch = -1;

        /// <summary>
        /// Returns the best matches, highest score first. Entries with equal scores
        /// keep their original file order. An empty query returns the first
        /// <paramref name="limit"/> entries unfiltered.
        /// </summary>
        /// <param name="recentTitles">
        /// Optional, most recently used first. Recency only separates entries that
        /// matched equally well, so it can reorder peers but never promote a weak
        /// match above a strong one.
        /// </param>
        public static List<ClipEntry> Rank(IList<ClipEntry> entries, string query, int limit,
            IList<string> recentTitles = null)
        {
            var results = new List<ClipEntry>();
            if (entries == null || limit <= 0) return results;

            var scored = new List<ScoredEntry>();
            bool matchAll = string.IsNullOrWhiteSpace(query);
            var trimmed = matchAll ? null : query.Trim();

            for (int index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null) continue;

                int score = 0;
                if (!matchAll)
                {
                    score = Score(entry.Title, entry.Text, trimmed);
                    if (score == NoMatch) continue;
                }

                scored.Add(new ScoredEntry(entry, score, index, RecencyOf(entry, recentTitles)));
            }

            // Stable: match quality first, then recency, then original file order.
            scored.Sort((left, right) =>
            {
                int byScore = right.Score.CompareTo(left.Score);
                if (byScore != 0) return byScore;

                int byRecency = left.Recency.CompareTo(right.Recency);
                if (byRecency != 0) return byRecency;

                return left.Index.CompareTo(right.Index);
            });

            for (int index = 0; index < scored.Count && results.Count < limit; index++)
                results.Add(scored[index].Entry);

            return results;
        }

        /// <summary>Position in the recently-used list, or "never used" when absent.</summary>
        private static int RecencyOf(ClipEntry entry, IList<string> recentTitles)
        {
            if (recentTitles == null || entry == null || entry.Title == null) return int.MaxValue;

            for (int index = 0; index < recentTitles.Count; index++)
            {
                if (string.Equals(recentTitles[index], entry.Title, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return int.MaxValue;
        }

        internal static int Score(string title, string body, string query)
        {
            title = title ?? string.Empty;
            body = body ?? string.Empty;

            if (string.Equals(title, query, StringComparison.OrdinalIgnoreCase))
                return ScoreExactTitle;

            if (title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return ScoreTitlePrefix;

            if (HasWordStartingWith(title, query))
                return ScoreTitleWordStart;

            if (title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return ScoreTitleContains;

            if (IsSubsequenceOf(title, query))
                return ScoreTitleSubsequence;

            if (body.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return ScoreBodyContains;

            return NoMatch;
        }

        /// <summary>Matches "sig" against the "signature" in "Support signature".</summary>
        private static bool HasWordStartingWith(string text, string query)
        {
            for (int index = 0; index < text.Length; index++)
            {
                bool atWordStart = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                if (!atWordStart) continue;

                if (index + query.Length > text.Length) return false;
                if (string.Compare(text, index, query, 0, query.Length,
                        StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            }
            return false;
        }

        /// <summary>Matches "mfu" against "Meeting follow-up".</summary>
        private static bool IsSubsequenceOf(string text, string query)
        {
            if (query.Length == 0) return true;

            int queryIndex = 0;
            for (int index = 0; index < text.Length; index++)
            {
                if (char.ToLowerInvariant(text[index]) != char.ToLowerInvariant(query[queryIndex]))
                    continue;

                queryIndex++;
                if (queryIndex == query.Length) return true;
            }
            return false;
        }

        private struct ScoredEntry
        {
            public ScoredEntry(ClipEntry entry, int score, int index, int recency)
            {
                Entry = entry;
                Score = score;
                Index = index;
                Recency = recency;
            }

            public ClipEntry Entry { get; }
            public int Score { get; }
            public int Index { get; }
            public int Recency { get; }
        }
    }
}
