using System;
using UnityEngine;

namespace Ouija
{
    [Serializable]
    public class MoodLines
    {
        [Tooltip("Used when the ghost's temperament contains this word.")] [SerializeField]
        private string temperament;

        [SerializeField] private string[] lines;

        public MoodLines(string temperament, params string[] lines)
        {
            this.temperament = temperament;
            this.lines = lines;
        }

        public string Temperament => temperament;
        public string[] Lines => lines;
    }

    [Serializable]
    public class OuijaIntent
    {
        [SerializeField] private string name;

        [Tooltip("Single words score 1 (with fuzzy matching), multi-word phrases score 2.")] [SerializeField]
        private string[] triggers = Array.Empty<string>();

        [Tooltip("Adds 0.5 when the question starts with one of these words.")] [SerializeField]
        private string[] startWords = Array.Empty<string>();

        [SerializeField] private float minScore = 1f;
        [Tooltip("Wins ties against intents with the same score.")] [SerializeField] private int priority;

        [Tooltip("Slots: {NAME} {FIRST} {LAST} {YEAR} {AGE} {CAUSE} {MEMORY} {SECRET}. Empty = silence.")]
        [SerializeField]
        private string[] lines = Array.Empty<string>();

        [SerializeField] private MoodLines[] moodLines = Array.Empty<MoodLines>();
        [Tooltip("Used instead of the normal lines while the ghost is angry.")] [SerializeField]
        private string[] angryLines = Array.Empty<string>();

        [Tooltip("Used when the same thing is asked twice in a row.")] [SerializeField]
        private string[] repeatLines = Array.Empty<string>();

        [Tooltip("Used when no other line can be filled in, e.g. no memories unlocked yet.")] [SerializeField]
        private string[] fallbackLines = Array.Empty<string>();

        [SerializeField] private float anger;
        [SerializeField] private float sanityCost;

        public OuijaIntent(string name)
        {
            this.name = name;
        }

        public string Name => name;
        public string[] Triggers => triggers;
        public string[] StartWords => startWords;
        public float MinScore => minScore;
        public int Priority => priority;
        public string[] Lines => lines;
        public MoodLines[] Moods => moodLines;
        public string[] AngryLines => angryLines;
        public string[] RepeatLines => repeatLines;
        public string[] FallbackLines => fallbackLines;
        public float Anger => anger;
        public float SanityCost => sanityCost;

        public OuijaIntent On(params string[] words)
        {
            triggers = words;
            return this;
        }

        public OuijaIntent StartingWith(params string[] words)
        {
            startWords = words;
            return this;
        }

        public OuijaIntent Say(params string[] answers)
        {
            lines = answers;
            return this;
        }

        public OuijaIntent Mood(string temperament, params string[] answers)
        {
            Array.Resize(ref moodLines, moodLines.Length + 1);
            moodLines[moodLines.Length - 1] = new MoodLines(temperament, answers);
            return this;
        }

        public OuijaIntent Angry(params string[] answers)
        {
            angryLines = answers;
            return this;
        }

        public OuijaIntent Repeat(params string[] answers)
        {
            repeatLines = answers;
            return this;
        }

        public OuijaIntent Fallback(params string[] answers)
        {
            fallbackLines = answers;
            return this;
        }

        public OuijaIntent Costs(float angerDelta, float sanity)
        {
            anger = angerDelta;
            sanityCost = sanity;
            return this;
        }

        public OuijaIntent Scoring(float minimum, int tieBreak)
        {
            minScore = minimum;
            priority = tieBreak;
            return this;
        }
    }

    [Serializable]
    public class WordSwap
    {
        [SerializeField] private string from;
        [SerializeField] private string to;

        public WordSwap(string from, string to)
        {
            this.from = from;
            this.to = to;
        }

        public string From => from;
        public string To => to;
    }

    [CreateAssetMenu(menuName = "Ouija/Response Set", fileName = "Ouija Responses")]
    public class OuijaResponseSet : ScriptableObject
    {
        [Header("Behaviour")] [SerializeField] [Range(0f, 1f)]
        private float silenceChance = 0.05f;

        [SerializeField] [Range(0f, 1f)] private float stutterChance = 0.07f;
        [SerializeField] [Range(0f, 1f)] private float repeatRemarkChance = 0.6f;
        [SerializeField] private float repeatAnger = 0.1f;
        [SerializeField] private float angerDecayPerMinute = 0.15f;
        [SerializeField] [Range(0f, 1f)] private float angryThreshold = 0.5f;
        [SerializeField] private float leaveThreshold = 1f;
        [SerializeField] [Range(0f, 1f)] private float insanityAnger = 0.3f;
        [SerializeField] private int secretAfterQuestions = 4;
        [SerializeField] private int maxFollowUpWords = 3;
        [SerializeField] private int recentLineMemory = 8;

        [Header("Unprompted")] [SerializeField]
        private Vector2 unpromptedDelay = new(25f, 60f);

        [SerializeField] [Range(0f, 1f)] private float unpromptedChance = 0.5f;

        [SerializeField] private string[] unpromptedLines =
        {
            "HELP", "COLD", "I SEE YOU", "{FIRST}", "BEHIND YOU", "STILL HERE", "{MEMORY}", "LISTEN", "DO NOT GO"
        };

        [Header("General lines")] [SerializeField]
        private string[] unknownLines =
            { "", "WHAT", "SPEAK CLEARLY", "COLD", "LISTEN", "I DO NOT KNOW", "HELP", "ASK AGAIN" };

        [SerializeField] private string[] angryLines = { "GET OUT", "LEAVE", "ENOUGH", "STOP", "MY HOUSE" };
        [SerializeField] private string[] departLines = { "GOODBYE", "ENOUGH GOODBYE", "NO MORE GOODBYE" };

        [Header("Language")] [SerializeField]
        private string[] followUpWords = { "why", "how", "what", "really", "more", "explain", "meaning", "mean" };

        [SerializeField] private string[] stopWords =
        {
            "a", "an", "the", "and", "or", "of", "to", "in", "on", "at", "by", "for", "with", "under", "into",
            "behind", "that", "who", "was", "were", "is", "it", "his", "her", "my", "your", "you", "i", "me",
            "nobody", "knew", "about", "there", "this", "from", "when"
        };

        [SerializeField] private WordSwap[] swaps =
        {
            new("weegee", "ouija"), new("wigi", "ouija"), new("wheres", "where is"), new("whats", "what is"),
            new("whos", "who is"), new("im", "i am"), new("youre", "you are"), new("dont", "do not"),
            new("didnt", "did not"), new("doesnt", "does not"), new("cant", "can not"), new("cannot", "can not"),
            new("theres", "there is"), new("u", "you"), new("ur", "your"), new("ya", "you"), new("wanna", "want to"),
            new("gonna", "going to"), new("hows", "how is"), new("allo", "hello")
        };

        [Tooltip("If the cause of death contains the first word, the board spells the second.")] [SerializeField]
        private WordSwap[] causeWords =
        {
            new("drown", "WATER"), new("burn", "FIRE"), new("fire", "FIRE"), new("fell", "FELL"),
            new("stairs", "STAIRS"), new("fever", "FEVER"), new("poison", "POISON"), new("froze", "COLD"),
            new("freez", "COLD"), new("crush", "CRUSHED"), new("hang", "ROPE"), new("stab", "KNIFE"),
            new("shot", "BULLET"), new("murder", "MURDER"), new("kill", "MURDER")
        };

        [Header("Intents")] [SerializeField] private OuijaIntent[] intents = DefaultIntents();

        public float SilenceChance => silenceChance;
        public float StutterChance => stutterChance;
        public float RepeatRemarkChance => repeatRemarkChance;
        public float RepeatAnger => repeatAnger;
        public float AngerDecayPerMinute => angerDecayPerMinute;
        public float AngryThreshold => angryThreshold;
        public float LeaveThreshold => leaveThreshold;
        public float InsanityAnger => insanityAnger;
        public int SecretAfterQuestions => secretAfterQuestions;
        public int MaxFollowUpWords => maxFollowUpWords;
        public int RecentLineMemory => recentLineMemory;
        public Vector2 UnpromptedDelay => unpromptedDelay;
        public float UnpromptedChance => unpromptedChance;
        public string[] UnpromptedLines => unpromptedLines;
        public string[] UnknownLines => unknownLines;
        public string[] AngryLines => angryLines;
        public string[] DepartLines => departLines;
        public string[] FollowUpWords => followUpWords;
        public string[] StopWords => stopWords;
        public WordSwap[] Swaps => swaps;
        public WordSwap[] CauseWords => causeWords;
        public OuijaIntent[] Intents => intents;

        [ContextMenu("Reset Intents To Defaults")]
        private void ResetIntents()
        {
            intents = DefaultIntents();
        }

        private static OuijaIntent[] DefaultIntents()
        {
            return new[]
            {
                new OuijaIntent("Greeting")
                    .On("hello", "hi", "hey", "greetings", "anyone", "anybody", "someone", "is anyone here",
                        "is anybody there", "are you there", "can you hear", "is someone", "speak to us",
                        "talk to us", "present")
                    .Say("HELLO", "I AM HERE", "YES", "WHO CALLS", "I HEAR YOU", "HELLO")
                    .Mood("angry", "WHAT", "GO AWAY", "YOU AGAIN")
                    .Mood("playful", "HELLO HELLO", "PLAY WITH ME", "HI")
                    .Mood("confused", "WHO IS THERE", "MOTHER")
                    .Mood("mournful", "HELLO FRIEND", "FINALLY")
                    .Mood("bitter", "WHAT DO YOU WANT")
                    .Repeat("I AM HERE", "STILL HERE", "YES YES"),

                new OuijaIntent("Name")
                    .On("name", "called", "who are you", "who is this", "who is there", "who am i speaking",
                        "who are we", "identify", "spell your")
                    .Say("{NAME}", "{FIRST}", "MY NAME IS {FIRST}", "{FIRST} {LAST}", "{FIRST}")
                    .Mood("angry", "NOT YOURS TO KNOW", "{LAST}")
                    .Mood("confused", "I FORGET", "{FIRST} I THINK")
                    .Mood("bitter", "{LAST}", "WHY")
                    .Angry("NO", "NOT YOURS")
                    .Repeat("I TOLD YOU", "{FIRST} I SAID", "ALREADY SAID")
                    .Scoring(1f, 1),

                new OuijaIntent("Age")
                    .On("old", "age", "how old", "young", "years old", "birthday", "born")
                    .Say("{AGE}", "{AGE} YEARS", "I WAS {AGE}")
                    .Mood("confused", "{AGE} I THINK", "I FORGET")
                    .Repeat("I TOLD YOU", "{AGE} I SAID")
                    .Scoring(1f, 1),

                new OuijaIntent("Year")
                    .On("when", "year", "what year", "century", "how long", "long ago", "date")
                    .Say("{YEAR}", "IN {YEAR}", "LONG AGO", "{YEAR}")
                    .Mood("confused", "WHAT YEAR IS IT", "TODAY")
                    .Mood("mournful", "SO LONG AGO", "{YEAR}")
                    .Repeat("I TOLD YOU", "{YEAR} I SAID")
                    .Scoring(1f, 2),

                new OuijaIntent("Cause")
                    .On("die", "died", "death", "dead", "kill", "killed", "murder", "murdered", "how did you die",
                        "what happened", "happened", "cause", "end", "accident")
                    .Say("{CAUSE}", "{CAUSE} TOOK ME", "IT WAS {CAUSE}", "REMEMBER {CAUSE}")
                    .Mood("angry", "DO NOT ASK", "{CAUSE}")
                    .Mood("mournful", "{CAUSE} ALWAYS {CAUSE}", "IT HURT")
                    .Mood("confused", "I AM NOT DEAD", "DEAD")
                    .Repeat("{CAUSE} I SAID", "STOP ASKING")
                    .Costs(0.05f, 1f),

                new OuijaIntent("Object")
                    .On("object", "thing", "item", "bound", "tied", "attached", "belong", "belongs", "haunt",
                        "haunting", "find", "search", "remember", "memory", "possess", "possessing", "anchor",
                        "what are you bound to", "what do you haunt", "where should we look", "what is it",
                        "describe", "clue", "hint")
                    .Say("{MEMORY}", "{MEMORY}", "{MEMORY}", "REMEMBER {MEMORY}")
                    .Mood("angry", "FIND IT YOURSELF", "{MEMORY}")
                    .Mood("playful", "WARMER", "{MEMORY}")
                    .Fallback("I CANNOT REMEMBER", "I FORGET", "TOO DARK", "KEEP TALKING")
                    .Scoring(1f, 1),

                new OuijaIntent("Guess")
                    .On("is it", "is it the", "is this", "is that", "am i close", "getting closer", "this one",
                        "that one", "the one", "is it in", "is it near")
                    .StartingWith("is", "am", "are", "could", "would", "does", "maybe")
                    .Say("I CANNOT RECALL", "MAYBE", "{MEMORY}", "I DO NOT KNOW", "PERHAPS", "LOOK CLOSER")
                    .Mood("playful", "WARMER", "COLDER", "HAHA")
                    .Mood("angry", "FIND IT", "WRONG")
                    .Scoring(2.5f, 3),

                new OuijaIntent("Secret")
                    .On("secret", "hide", "hid", "hidden", "hiding", "confess", "truth", "lie", "lying", "regret",
                        "guilt", "sin", "what are you hiding", "tell me the truth")
                    .Say("{SECRET}", "{SECRET}", "NEVER TELL", "{SECRET}")
                    .Mood("bitter", "NEVER", "{SECRET}")
                    .Mood("playful", "GUESS", "{SECRET}")
                    .Fallback("NOT YET", "I CANNOT SAY", "LATER", "NO")
                    .Scoring(1f, 1),

                new OuijaIntent("Want")
                    .On("want", "need", "stay", "purpose", "rest", "peace", "help", "why are you here",
                        "what do you want", "how can we help", "free", "move on")
                    .Say("REST", "PEACE", "FIND IT", "HELP ME", "LET ME GO")
                    .Mood("angry", "GET OUT", "YOU GONE")
                    .Mood("mournful", "I WANT TO REST", "SO TIRED")
                    .Mood("playful", "PLAY", "GAMES")
                    .Mood("protective", "PROTECT HOUSE", "KEEP AWAY")
                    .Mood("confused", "GO HOME", "WHERE AM I")
                    .Mood("bitter", "JUSTICE", "THEM")
                    .Repeat("I TOLD YOU", "LISTEN"),

                new OuijaIntent("Location")
                    .On("where", "room", "location", "place", "where are you", "upstairs", "downstairs", "basement",
                        "attic", "kitchen", "near", "close")
                    .Say("HERE", "CLOSE", "NEAR YOU", "BEHIND YOU", "IN THE WALLS", "WATCHING")
                    .Mood("playful", "FIND ME", "HIDE AND SEEK")
                    .Mood("confused", "I DO NOT KNOW", "DARK")
                    .Costs(0f, 0.5f),

                new OuijaIntent("Sign")
                    .On("sign", "show", "appear", "prove", "knock", "move", "touch", "do something",
                        "show yourself", "give us a sign", "make a noise", "visible")
                    .Say("WATCH", "NOT YET", "LOOK BEHIND YOU", "SOON", "ARE YOU SURE")
                    .Mood("playful", "BOO", "HAHA")
                    .Mood("angry", "YOU WILL SEE")
                    .Costs(0.05f, 2f)
                    .Scoring(1f, 1),

                new OuijaIntent("Others")
                    .On("how many", "alone", "others", "other", "family", "children", "child", "wife", "husband",
                        "mother", "father", "friends", "spirits")
                    .Say("ALONE", "NOT ALONE", "THEY WATCH TOO", "MANY", "GONE")
                    .Mood("mournful", "ALL GONE", "MISS THEM")
                    .Mood("confused", "WHERE ARE THEY"),

                new OuijaIntent("Fate")
                    .On("will i die", "am i going to die", "are we going to die", "will we die", "future", "fate",
                        "hurt me", "hurt us", "safe", "danger", "kill me", "kill us")
                    .Say("SOON", "NOT TODAY", "IF YOU STAY", "WHO KNOWS", "YES")
                    .Mood("protective", "LEAVE AND LIVE", "NOT IF YOU GO")
                    .Mood("playful", "MAYBE", "HAHA")
                    .Costs(0.05f, 2f)
                    .Scoring(2f, 4),

                new OuijaIntent("Alive")
                    .On("are you dead", "you are dead", "alive", "ghost", "spirit", "demon", "human", "real")
                    .Say("YES", "DEAD", "DEAD SINCE {YEAR}", "ONCE")
                    .Mood("confused", "I AM NOT DEAD", "ALIVE", "WHAT")
                    .Mood("mournful", "DEAD", "LONG DEAD")
                    .Mood("angry", "DEMON", "WORSE")
                    .Scoring(1f, 2),

                new OuijaIntent("Insult")
                    .On("fuck", "fucking", "shit", "stupid", "idiot", "hate", "ugly", "dumb", "loser", "bitch",
                        "shut up", "go away", "leave us alone", "get out", "destroy", "exorcise", "banish", "kill you",
                        "not real", "fake", "scared of you", "boring")
                    .Say("NO", "MY HOUSE", "YOU WILL PAY", "LEAVE NOW", "CAREFUL", "HOW DARE YOU")
                    .Mood("playful", "HAHA", "MAKE ME")
                    .Mood("mournful", "WHY", "CRUEL")
                    .Angry("YOU WILL PAY", "DIE", "GET OUT", "RUN")
                    .Costs(0.35f, 3f)
                    .Scoring(1f, 5),

                new OuijaIntent("Kind")
                    .On("thank", "thanks", "sorry", "love", "friend", "kind", "please", "nice", "beautiful",
                        "forgive", "sorry for your loss")
                    .Say("THANK YOU", "YES", "KIND", "STAY")
                    .Mood("angry", "LIAR", "NO")
                    .Mood("mournful", "THANK YOU", "FRIEND")
                    .Mood("bitter", "WORDS", "LIAR")
                    .Costs(-0.2f, 0f),

                new OuijaIntent("YesNo")
                    .StartingWith("is", "are", "am", "do", "does", "did", "can", "could", "will", "would", "was",
                        "were", "have", "has", "had", "should", "shall")
                    .Say("YES", "YES", "NO", "NO", "MAYBE")
                    .Mood("angry", "NO", "NO", "YES")
                    .Mood("playful", "MAYBE", "YES", "NO", "PERHAPS")
                    .Mood("confused", "I DO NOT KNOW", "MAYBE")
                    .Mood("bitter", "NO", "NEVER")
                    .Scoring(0.5f, -1)
            };
        }
    }
}
