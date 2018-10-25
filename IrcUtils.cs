using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharpyBot
{
    public static class IrcUtils
    {
        public static bool IsChan(string s, char[] chanTypes)
        {
            if (chanTypes != null)
            {
                return chanTypes.Any(c => s.StartsWith(c));
            }
            else
            {
                return false;
            }
        }

        public static Dictionary<string, int> FindKarma(string message)
        {
            var karma = new Dictionary<string, int>();

            Regex karmaRegex = new Regex(@"((?<subject>[^\s\+\-]+)(?:[\+\-]{2})+)+");
            MatchCollection matches = karmaRegex.Matches(message);

            Regex positiveKarmaRegex = new Regex(@"[\+]{2}");
            Regex negativeKarmaRegex = new Regex(@"[\-]{2}");

            foreach (Match match in matches)
            {
                string subject = match.Groups["subject"].Value;
                string s = match.Captures[0].Value;

                int positiveKarma = positiveKarmaRegex.Matches(s).Count;
                int negativeKarma = negativeKarmaRegex.Matches(s).Count;

                if (karma.ContainsKey(subject))
                {
                    karma[subject] += positiveKarma - negativeKarma;
                }
                else
                {
                    karma.Add(subject, positiveKarma - negativeKarma);
                }
            }

            return karma;
        }
    }
}