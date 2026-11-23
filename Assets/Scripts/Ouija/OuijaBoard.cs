using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Ouija
{
    [Serializable]
    public class BoardSpot
    {
        [SerializeField] private string key;
        [SerializeField] private TMP_Text label;

        public string Key => key;
        public TMP_Text Label => label;
    }

    public class OuijaBoard : Tool
    {
        private static readonly Color Ink = new(0.16f, 0.08f, 0.03f);
        private static readonly Color Glow = new(1f, 0.82f, 0.45f);
        private static readonly Color Pale = new(0.85f, 0.78f, 0.65f);

        private static readonly Regex UserGoodbye =
            new(@"^\W*(good\s*-?\s*bye|bye|farewell)\b", RegexOptions.IgnoreCase);

        private static readonly Regex SpeechNoise = new(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*");

        [Header("Ghost")]
        [Tooltip("Who is on the other side. Create more via Assets > Create > Ouija > Ghost Profile.")]
        [SerializeField]
        private GhostProfile ghost;

        [Header("Haunt")] [SerializeField] private GhostController haunt;
        [SerializeField] private Sanity sanity;
        [SerializeField] private float questionSanityCost = 2f;

        [Tooltip("Every this many questions, the spirit recalls one more memory of the object it is bound to.")]
        [SerializeField]
        private int questionsPerMemory = 3;

        [Header("Voice")] [SerializeField] private VoiceRecognition voice;
        [SerializeField] private Transform listener;
        [SerializeField] private float listenRange = 3f;

        [Header("Responses")]
        [Tooltip("Intents and answer lines. Leave empty to use the built-in defaults.")]
        [SerializeField]
        private OuijaResponseSet responses;

        [Tooltip("How long the spirit hesitates before answering, in seconds.")] [SerializeField]
        private Vector2 thinkTime = new(0.8f, 2.2f);

        [Tooltip("Answers longer than this are cut off at a word boundary.")] [SerializeField]
        private int maxAnswerChars = 40;

        [Header("Feel")] [SerializeField] [Range(0.25f, 4f)]
        private float spellSpeed = 1f;

        [SerializeField] private float moveSpeed = 0.6f;
        [SerializeField] private float wobble = 0.004f;
        [SerializeField] private float tiltPerMeter = 25f;

        [Header("Debug")] [Tooltip("Log how every question was understood and answered to the Console.")]
        [SerializeField]
        private bool debugLog = true;

        [Tooltip("Show the debug panel on screen. Toggle in Play mode with F1.")] [SerializeField]
        private bool showDebugPanel = true;

        [Header("Board parts (wired by GameObject > Ouija Board)")] [SerializeField]
        private Transform planchette;

        [SerializeField] private float planchetteHeight = 0.016f;
        [SerializeField] private Vector2 pointerOffset = new(0f, -0.05f);
        [SerializeField] private Vector2 homePosition = new(0f, 0.17f);
        [SerializeField] private List<BoardSpot> spots = new();
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text answerText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text debugText;
        [SerializeField] private GameObject debugPanel;

        private readonly Dictionary<string, TMP_Text> _labels = new();
        private readonly Dictionary<TMP_Text, Coroutine> _pulses = new();

        private float _nextUnprompted;
        private Vector2 _planchetteBase;
        private int _questionsAsked;
        private OuijaResponder _responder;
        private State _state = State.Ended;
        private float _tilt;

        public override string Reading => _state switch
        {
            State.Ended => "Hands off the board",
            State.Idle => "Listening...",
            State.Thinking => "The planchette stirs...",
            _ => "Spelling..."
        };

        private string[] Memories => haunt.BoundObject != null && haunt.BoundObject.Memories != null
            ? haunt.BoundObject.Memories
            : Array.Empty<string>();
        private int UnlockedMemories => Mathf.Min(Memories.Length, 1 + _questionsAsked / questionsPerMemory);
        private bool InRange => Vector3.Distance(listener.position, transform.position) <= listenRange;

        private void Awake()
        {
            foreach (BoardSpot spot in spots)
                _labels[spot.Key] = spot.Label;

            if (responses == null) responses = ScriptableObject.CreateInstance<OuijaResponseSet>();
            _responder = new OuijaResponder(responses);
            _planchetteBase = homePosition;
            debugPanel.SetActive(showDebugPanel);
        }

        private void Start()
        {
            questionText.text = "";
            answerText.text = "";
            SetStatus("Rest your fingers on the planchette to begin.", Pale);
            debugText.text = "<b>DEBUG</b>  (F1 to hide)\n\n" + NoParse(ghost.DisplayName) +
                             "\n\nAsk something to see how the spirit understood it.\nFull details are also logged to the Console.";
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                showDebugPanel = !showDebugPanel;
                debugPanel.SetActive(showDebugPanel);
            }

            _responder.Tick(Time.deltaTime);
            if (_state == State.Idle && !voice.IsSpeaking && Time.time >= _nextUnprompted)
            {
                ScheduleUnprompted();
                if (Random.value < responses.UnpromptedChance * (1f + sanity.Insanity))
                    StartCoroutine(UnpromptedRoutine());
            }

            if (_state == State.Thinking)
            {
                float t = Time.time * 1.1f;
                Vector2 target = homePosition + new Vector2(Mathf.Cos(t) * 0.11f, Mathf.Sin(t) * 0.06f - 0.04f);
                _planchetteBase = Vector2.Lerp(_planchetteBase, target, Time.deltaTime * 1.8f);
            }

            float n = Time.time * 1.7f;
            Vector2 jitter = new Vector2(Mathf.PerlinNoise(n, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, n) - 0.5f) *
                             wobble;
            Vector2 position = _planchetteBase + jitter;
            planchette.localPosition = new Vector3(position.x, planchetteHeight, position.y);

            _tilt = Mathf.Lerp(_tilt, -_planchetteBase.x * tiltPerMeter, Time.deltaTime * 4f);
            planchette.localEulerAngles = new Vector3(0f, _tilt, 0f);
        }

        private void OnEnable()
        {
            voice.PartialTextRecognized += OnVoicePartial;
            voice.TextRecognized += OnVoiceText;
        }

        private void OnDisable()
        {
            voice.PartialTextRecognized -= OnVoicePartial;
            voice.TextRecognized -= OnVoiceText;
            if (_state == State.Ended) return;
            _planchetteBase = homePosition;
            EndSession("You let go of the board.");
        }

        private void OnVoicePartial(string text)
        {
            if (_state != State.Idle || !InRange) return;
            string spoken = CleanSpeech(text);
            if (spoken.Length > 0) questionText.text = $"You: {spoken}…";
        }

        private void OnVoiceText(string text)
        {
            if (!InRange) return;
            string spoken = CleanSpeech(text);
            if (spoken.Length == 0) return;

            if (_state == State.Idle) Ask(spoken);
        }

        private static string CleanSpeech(string text)
        {
            return SpeechNoise.Replace(text, "").Trim();
        }

        public override void Use()
        {
            if (_state == State.Ended)
                StartCoroutine(NewSeance());
            else if (_state == State.Idle)
                StartCoroutine(UserSaysGoodbye());
        }

        private void Ask(string question)
        {
            questionText.text = "You: " + question;
            answerText.text = "…";

            if (UserGoodbye.IsMatch(question))
                StartCoroutine(UserSaysGoodbye());
            else
                StartCoroutine(AskRoutine(question));
        }

        private IEnumerator UserSaysGoodbye()
        {
            _state = State.Spelling;
            if (debugLog) Debug.Log("[Ouija] You said goodbye: closing the board.", this);
            yield return Spell("GOODBYE", false);
            EndSession("You took your hands off the board.");
        }

        private void EndSession(string message)
        {
            _state = State.Ended;
            _responder.Reset();
            SetStatus(message, Pale);
            debugText.text += "\n<color=#ffd27a>Séance ended.</color>";
        }

        private IEnumerator NewSeance()
        {
            _state = State.Spelling;
            questionText.text = "";
            answerText.text = "";
            SetStatus("Summoning…", Pale);
            yield return MoveTo(homePosition, 0.5f);
            SetStatus("Speak to the board…", Pale);
            if (debugLog) Debug.Log("[Ouija] New séance started.", this);
            _responder.Reset();
            EnterIdle();
        }

        private void EnterIdle()
        {
            _state = State.Idle;
            ScheduleUnprompted();
        }

        private void ScheduleUnprompted()
        {
            Vector2 delay = responses.UnpromptedDelay;
            _nextUnprompted = Time.time + Random.Range(delay.x, delay.y) / (1f + sanity.Insanity);
        }

        private OuijaContext Context()
        {
            string[] unlocked = new string[UnlockedMemories];
            Array.Copy(Memories, unlocked, unlocked.Length);
            return new OuijaContext(ghost, unlocked, _questionsAsked, sanity.Insanity);
        }

        private IEnumerator UnpromptedRoutine()
        {
            string line = _responder.Unprompted(Context());
            if (string.IsNullOrEmpty(line)) yield break;
            _state = State.Thinking;
            yield return new WaitForSeconds(Random.Range(thinkTime.x, thinkTime.y));
            if (debugLog) Debug.Log($"[Ouija] Unprompted: \"{line}\"", this);
            questionText.text = "";
            yield return Spell(Trim(line));
            EnterIdle();
        }

        private IEnumerator AskRoutine(string question)
        {
            _state = State.Thinking;
            OuijaReply reply = _responder.Answer(question, Context());
            sanity.Drain(questionSanityCost + reply.SanityCost);
            _questionsAsked++;
            string answer = Trim(reply.Text);
            ReportDebug(question, reply, answer);

            yield return new WaitForSeconds(Random.Range(thinkTime.x, thinkTime.y));
            SetStatus("", Pale);
            yield return Spell(answer, !reply.Departing);

            if (reply.Departing)
            {
                EndSession("The spirit has left the board.");
                yield break;
            }

            EnterIdle();
        }

        private string Trim(string answer)
        {
            string cleaned = OuijaResponder.Board(answer);
            if (cleaned.Length <= maxAnswerChars) return cleaned;
            int cut = cleaned.LastIndexOf(' ', maxAnswerChars);
            return cleaned.Substring(0, cut > 0 ? cut : maxAnswerChars);
        }

        private IEnumerator Spell(string answer, bool returnHome = true)
        {
            _state = State.Spelling;
            StringBuilder shown = new();
            answerText.text = "";

            if (answer.Length == 0)
            {
                yield return MoveTo(homePosition + new Vector2(0.09f, -0.025f), 0.5f);
                yield return MoveTo(homePosition, 0.5f);
                answerText.text = "…";
                yield break;
            }

            string[] words = answer.Split(' ');
            for (int w = 0; w < words.Length; w++)
            {
                string word = words[w];
                if (w > 0)
                {
                    shown.Append("  ");
                    yield return new WaitForSeconds(0.45f / spellSpeed);
                }

                if (word.Length > 1 && _labels.ContainsKey(word))
                {
                    yield return Visit(word, 1.6f);
                    shown.Append(word);
                    answerText.text = shown.ToString();
                    continue;
                }

                foreach (char c in word)
                {
                    string key = c.ToString();
                    if (!_labels.ContainsKey(key)) continue;
                    yield return Visit(key, 1f);
                    shown.Append(c);
                    answerText.text = shown.ToString();
                }
            }

            yield return new WaitForSeconds(0.6f / spellSpeed);
            if (returnHome) yield return MoveTo(homePosition, 0.6f);
        }

        private IEnumerator Visit(string key, float dwellMultiplier)
        {
            TMP_Text label = _labels[key];
            Vector3 local = label.transform.localPosition;
            Vector2 target = new Vector2(local.x, local.z) + pointerOffset;
            if ((target - _planchetteBase).magnitude < 0.001f)
                yield return MoveTo(target + new Vector2(0f, -0.05f), 1.6f);

            yield return MoveTo(target, 1f);

            if (_pulses.TryGetValue(label, out Coroutine running) && running != null) StopCoroutine(running);
            _pulses[label] = StartCoroutine(Pulse(label));
            yield return new WaitForSeconds(0.4f * dwellMultiplier / spellSpeed);
        }

        private IEnumerator MoveTo(Vector2 target, float speedMultiplier)
        {
            Vector2 start = _planchetteBase;
            float distance = (target - start).magnitude;
            if (distance < 0.001f) yield break;

            float duration = (0.3f + distance / moveSpeed) / (spellSpeed * speedMultiplier);
            Vector2 direction = (target - start) / distance;
            Vector2 perpendicular = new(-direction.y, direction.x);
            float bend = Random.Range(-0.18f, 0.18f) * distance;

            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                _planchetteBase = Vector2.Lerp(start, target, eased) +
                                  perpendicular * (Mathf.Sin(eased * Mathf.PI) * bend);
                yield return null;
            }

            _planchetteBase = target;
        }

        private IEnumerator Pulse(TMP_Text label)
        {
            const float duration = 0.9f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float strength = 1f - t;
                label.color = Color.Lerp(Ink, Glow, strength);
                label.transform.localScale = Vector3.one * (1f + 0.18f * strength);
                yield return null;
            }

            label.color = Ink;
            label.transform.localScale = Vector3.one;
        }

        private void SetStatus(string message, Color color)
        {
            statusText.text = message;
            statusText.color = color;
        }

        private string GhostSummary()
        {
            string bound = haunt.BoundObject != null ? haunt.BoundObject.DisplayName : "nothing";
            return $"<color=#ffd27a>Ghost</color>\n{NoParse(ghost.DisplayName)}\n" +
                   $"Bound to {NoParse(bound)}, memories {UnlockedMemories}/{Memories.Length}\n" +
                   $"Anger {_responder.Anger:0.00}, insanity {sanity.Insanity:0.00}, questions {_questionsAsked}";
        }

        private void ReportDebug(string question, OuijaReply reply, string spelled)
        {
            string flags = (reply.FollowUp ? " follow-up" : "") + (reply.Repeated ? " repeated" : "") +
                           (reply.Departing ? " departing" : "");
            if (debugLog)
                Debug.Log($"[Ouija] Q: \"{question}\" heard \"{reply.Heard}\" → {reply.Intent} ({reply.Score:0.#}){flags}" +
                          $" [{reply.Scores}] template \"{reply.Template}\" → board: \"{spelled}\"" +
                          $" anger {_responder.Anger:0.00}", this);

            StringBuilder panel = new();
            panel.AppendLine("<b>DEBUG</b>  (F1 to hide, full log in Console)");
            panel.AppendLine();
            panel.AppendLine(GhostSummary());
            panel.AppendLine();
            panel.AppendLine("<color=#ffd27a>Question</color>");
            panel.AppendLine(NoParse(question));
            panel.AppendLine($"<color=#aaa>heard: {NoParse(reply.Heard)}</color>");
            panel.AppendLine();
            panel.AppendLine("<color=#ffd27a>Intent</color>");
            panel.AppendLine($"{NoParse(reply.Intent)} ({reply.Score:0.#}){flags}");
            panel.AppendLine($"<color=#aaa>{NoParse(reply.Scores.Length > 0 ? reply.Scores : "no matches")}</color>");
            panel.AppendLine();
            panel.AppendLine("<color=#ffd27a>Template</color>");
            panel.AppendLine(NoParse(reply.Template ?? ""));
            panel.AppendLine();
            panel.AppendLine("<color=#ffd27a>Spelled on board</color>");
            panel.AppendLine(spelled.Length > 0 ? spelled : "<i>(nothing)</i>");
            debugText.text = panel.ToString();
        }

        private static string NoParse(string text)
        {
            return "<noparse>" + (text ?? "").Replace("</noparse>", "</ noparse>") + "</noparse>";
        }

        private enum State
        {
            Idle,
            Thinking,
            Spelling,
            Ended
        }
    }
}
