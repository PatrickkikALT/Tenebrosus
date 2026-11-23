using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Random = UnityEngine.Random;

namespace Ouija
{
    public readonly struct OuijaContext
    {
        public readonly GhostProfile Ghost;
        public readonly IReadOnlyList<string> Memories;
        public readonly int QuestionsAsked;
        public readonly float Insanity;

        public OuijaContext(GhostProfile ghost, IReadOnlyList<string> memories, int questionsAsked, float insanity)
        {
            Ghost = ghost;
            Memories = memories ?? Array.Empty<string>();
            QuestionsAsked = questionsAsked;
            Insanity = insanity;
        }
    }

    public class OuijaReply
    {
        public string Text = "";
        public string Intent = "Unknown";
        public float Score;
        public bool FollowUp;
        public bool Repeated;
        public bool Departing;
        public float SanityCost;
        public string Heard = "";
        public string Template = "";
        public string Scores = "";
    }

    public class OuijaResponder
    {
        private const int MaxBoardWords = 5;
        private const int FuzzyMinLength = 6;
        private static readonly Regex Slot = new(@"\{([A-Z]+)\}");
        private static readonly Regex NonBoard = new("[^A-Z0-9]+");
        private static readonly Regex NonWord = new("[^a-z0-9]+");

        private readonly Queue<string> _recent = new();
        private readonly OuijaResponseSet _set;
        private readonly HashSet<string> _stopWords;
        private readonly HashSet<int> _toldMemories = new();

        private OuijaIntent _lastIntent;
        private int _pendingMemory = -1;

        public OuijaResponder(OuijaResponseSet set)
        {
            _set = set;
            _stopWords = new HashSet<string>(set.StopWords.Select(w => w.ToLowerInvariant()));
        }

        public float Anger { get; private set; }

        public void Reset()
        {
            Anger = 0f;
            _recent.Clear();
            _toldMemories.Clear();
            _lastIntent = null;
        }

        public void Tick(float deltaTime)
        {
            Anger = Math.Max(0f, Anger - _set.AngerDecayPerMinute * deltaTime / 60f);
        }

        public OuijaReply Answer(string question, OuijaContext context)
        {
            List<string> tokens = Tokenize(question);
            string text = " " + string.Join(" ", tokens) + " ";
            OuijaReply reply = new() { Heard = string.Join(" ", tokens) };

            OuijaIntent best = Classify(tokens, text, out float score, out string scores);
            reply.Scores = scores;

            if ((best == null || score < 1f) && _lastIntent != null && tokens.Count <= _set.MaxFollowUpWords &&
                tokens.Any(t => _set.FollowUpWords.Contains(t)))
            {
                best = _lastIntent;
                reply.FollowUp = true;
            }

            reply.Intent = best?.Name ?? "Unknown";
            reply.Score = score;
            reply.Repeated = best != null && best == _lastIntent && !reply.FollowUp;
            reply.SanityCost = best?.SanityCost ?? 0f;

            Anger = Math.Max(0f, Anger + (best?.Anger ?? 0f));
            if (reply.Repeated && best.RepeatLines.Length > 0) Anger += _set.RepeatAnger;
            float mood = Anger + context.Insanity * _set.InsanityAnger;
            _lastIntent = best;

            if (Anger >= _set.LeaveThreshold)
            {
                reply.Departing = true;
                reply.Text = Choose(_set.DepartLines, context, out reply.Template) ?? "GOODBYE";
                return reply;
            }

            if (Random.value < _set.SilenceChance)
            {
                reply.Template = "(silence)";
                return reply;
            }

            string line = Choose(PoolFor(best, reply.Repeated, mood, context.Ghost), context, out reply.Template);
            if (line == null && best != null) line = Choose(best.FallbackLines, context, out reply.Template);
            line ??= Choose(_set.UnknownLines, context, out reply.Template) ?? "";

            reply.Text = Stutter(line);
            reply.Departing = Regex.IsMatch(reply.Text, @"\bGOODBYE\b");
            return reply;
        }

        public string Unprompted(OuijaContext context)
        {
            return Choose(_set.UnpromptedLines, context, out _);
        }

        private IEnumerable<string> PoolFor(OuijaIntent intent, bool repeated, float mood, GhostProfile ghost)
        {
            if (intent == null) return _set.UnknownLines;
            if (repeated && intent.RepeatLines.Length > 0 && Random.value < _set.RepeatRemarkChance)
                return intent.RepeatLines;
            if (mood >= _set.AngryThreshold)
            {
                if (intent.AngryLines.Length > 0) return intent.AngryLines;
                if (Random.value < 0.35f) return _set.AngryLines;
            }

            string temperament = ghost.Temperament.ToLowerInvariant();
            return intent.Lines.Concat(intent.Moods
                .Where(m => !string.IsNullOrEmpty(m.Temperament) &&
                            temperament.Contains(m.Temperament.ToLowerInvariant()))
                .SelectMany(m => m.Lines));
        }

        private OuijaIntent Classify(List<string> tokens, string text, out float bestScore, out string scores)
        {
            OuijaIntent best = null;
            bestScore = 0f;
            List<(string name, float score)> ranking = new();

            foreach (OuijaIntent intent in _set.Intents)
            {
                float score = 0f;
                foreach (string raw in intent.Triggers)
                {
                    string trigger = Normalize(raw);
                    if (trigger.Length == 0) continue;
                    if (trigger.Contains(' '))
                    {
                        if (text.Contains(" " + trigger + " ")) score += 2f;
                    }
                    else if (tokens.Any(t => WordMatch(t, trigger)))
                    {
                        score += 1f;
                    }
                }

                if (tokens.Count > 0 && intent.StartWords.Any(w => Normalize(w) == tokens[0])) score += 0.5f;
                if (score > 0f) ranking.Add((intent.Name, score));
                if (score < intent.MinScore) continue;
                if (best == null || score > bestScore || (score == bestScore && intent.Priority > best.Priority))
                {
                    best = intent;
                    bestScore = score;
                }
            }

            scores = string.Join(", ", ranking.OrderByDescending(r => r.score).Take(4)
                .Select(r => $"{r.name} {r.score:0.#}"));
            return best;
        }

        private static bool WordMatch(string token, string trigger)
        {
            if (token == trigger) return true;
            if (trigger.Length >= 4 && (token == trigger + "s" || trigger == token + "s")) return true;
            return token.Length >= FuzzyMinLength && trigger.Length >= FuzzyMinLength &&
                   Math.Abs(token.Length - trigger.Length) <= 1 && Levenshtein(token, trigger) <= 1;
        }

        private static int Levenshtein(string a, string b)
        {
            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }

        private List<string> Tokenize(string text)
        {
            List<string> tokens = new();
            foreach (string word in Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                WordSwap swap = _set.Swaps.FirstOrDefault(s => s.From == word);
                if (swap != null) tokens.AddRange(swap.To.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                else tokens.Add(word);
            }

            return tokens;
        }

        private static string Normalize(string text)
        {
            string lower = (text ?? "").ToLowerInvariant().Replace("'", "").Replace("’", "");
            return NonWord.Replace(lower, " ").Trim();
        }

        private string Choose(IEnumerable<string> pool, OuijaContext context, out string template)
        {
            template = null;
            _pendingMemory = -1;
            List<(string template, string text)> options = new();
            foreach (string line in pool)
            {
                string filled = Fill(line ?? "", context);
                if (filled != null) options.Add((line, filled));
            }

            if (options.Count == 0) return null;
            List<(string template, string text)> fresh = options.Where(o => !_recent.Contains(o.text)).ToList();
            (string template, string text) pick = (fresh.Count > 0 ? fresh : options)[
                Random.Range(0, fresh.Count > 0 ? fresh.Count : options.Count)];

            template = pick.template;
            if (pick.template.Contains("{MEMORY}") && _pendingMemory >= 0) _toldMemories.Add(_pendingMemory);
            _recent.Enqueue(pick.text);
            while (_recent.Count > _set.RecentLineMemory) _recent.Dequeue();
            return pick.text;
        }

        private string Fill(string template, OuijaContext context)
        {
            bool missing = false;
            string filled = Slot.Replace(template, match =>
            {
                string value = SlotValue(match.Groups[1].Value, context);
                if (string.IsNullOrEmpty(value)) missing = true;
                return value ?? "";
            });
            return missing ? null : Board(filled);
        }

        private string SlotValue(string slot, OuijaContext context)
        {
            GhostProfile ghost = context.Ghost;
            string[] name = Board(ghost.GhostName).Split(' ', 2);
            return slot switch
            {
                "NAME" => Board(ghost.GhostName),
                "FIRST" => name[0],
                "LAST" => name.Length > 1 ? name[1] : null,
                "YEAR" => ghost.DeathYear.ToString(),
                "AGE" => ghost.AgeAtDeath.ToString(),
                "CAUSE" => CauseWord(ghost.CauseOfDeath),
                "MEMORY" => MemoryText(context.Memories),
                "SECRET" => SecretText(ghost.Secret, context.QuestionsAsked),
                _ => null
            };
        }

        private string CauseWord(string cause)
        {
            string lower = (cause ?? "").ToLowerInvariant();
            WordSwap match = _set.CauseWords.FirstOrDefault(c => !string.IsNullOrEmpty(c.From) && lower.Contains(c.From));
            if (match != null) return match.To;
            return ContentWords(cause).FirstOrDefault();
        }

        private string MemoryText(IReadOnlyList<string> memories)
        {
            List<int> usable = Enumerable.Range(0, memories.Count)
                .Where(i => !string.IsNullOrWhiteSpace(memories[i])).ToList();
            if (usable.Count == 0) return null;
            List<int> untold = usable.Where(i => !_toldMemories.Contains(i)).ToList();
            _pendingMemory = untold.Count > 0 ? untold[untold.Count - 1] : usable[Random.Range(0, usable.Count)];
            string[] words = Board(memories[_pendingMemory]).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > MaxBoardWords) words = ContentWords(memories[_pendingMemory]).ToArray();
            return string.Join(" ", words.Take(MaxBoardWords));
        }

        private string SecretText(string secret, int questionsAsked)
        {
            if (questionsAsked < _set.SecretAfterQuestions) return null;
            List<string> words = ContentWords(secret).ToList();
            if (words.Count == 0) return null;
            int count = Math.Min(Math.Min(words.Count, MaxBoardWords),
                1 + (questionsAsked - _set.SecretAfterQuestions) / 2);
            return string.Join(" ", words.Skip(words.Count - count));
        }

        private IEnumerable<string> ContentWords(string text)
        {
            return Board(text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !_stopWords.Contains(w.ToLowerInvariant()));
        }

        private string Stutter(string line)
        {
            if (line.Length == 0 || Random.value >= _set.StutterChance) return line;
            int split = line.LastIndexOf(' ');
            string last = line.Substring(split + 1);
            if (last.Length < 4 || last.All(char.IsDigit)) return line;
            StringBuilder cut = new(line.Substring(0, split + 1));
            cut.Append(last, 0, Random.Range(2, last.Length));
            return cut.ToString();
        }

        public static string Board(string text)
        {
            return NonBoard.Replace((text ?? "").ToUpperInvariant(), " ").Trim();
        }
    }
}
