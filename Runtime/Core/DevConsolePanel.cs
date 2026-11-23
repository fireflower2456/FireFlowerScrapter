using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireFlower
{
    // Cheat console (uGUI) at the bottom of the screen. Replaced IMGUI, which can't take typing in builds using Input System only.
    // ConsoleState opens and closes it with SetOpen on enter and exit.
    // Log piles up above; it stays visible briefly after closing, then fades
    public abstract class DevConsolePanel : MonoBehaviour, IUIPanel
    {
        [SerializeField] GameObject root;             // Input field and suggestion list. On only while open
        [SerializeField] TMP_InputField input;
        [SerializeField] RectTransform suggestionList;  // Vertical list shown above the word being typed
        [SerializeField] TMP_Text suggestionText;
        [SerializeField] TMP_Text ghostText;          // Remaining letters of the selected suggestion, dimmed inside the input
        [SerializeField] TMP_Text logText;            // Log. Stays after closing
        [SerializeField] CanvasGroup logGroup;
        [SerializeField, Min(1)] int visibleLines = 12;
        [SerializeField, Min(1)] int visibleSuggestions = 10;
        [SerializeField, Min(0f)] float lingerSeconds = 5f;   // How long the log stays visible after closing
        [SerializeField, Min(0.01f)] float fadeSeconds = 1f;
        [SerializeField, Min(0f)] float repeatDelay = 0.3f;        // Holding ↑↓ waits this long, then
        [SerializeField, Min(0.01f)] float repeatInterval = 0.05f; // keeps moving at this interval

        DeveloperConsole _console;
        int _heldDir;
        float _nextRepeat;
        bool _open;
        float _lingerUntil;

        // Typing a word (or Tab on an empty word) shows the list. While shown, ↑↓ picks suggestions; otherwise it browses history
        List<string> _candidates = new();
        int _selected;
        bool _forceList;
        bool ListShown => _candidates.Count > 0 && (_forceList || CurrentWord.Length > 0);
        int WordStart => input.text.LastIndexOfAny(new[] { ' ', '/' }) + 1;
        string CurrentWord => input.text.Substring(WordStart);

        // History browse position. History.Count means viewing the current draft (_draft)
        int _historyIndex;
        string _draft = "";

        void Awake()
        {
            _console = DeveloperConsole.Instance;
            input.onFocusSelectAll = false;   // Keeps the / inserted on open from being selected and overwritten by the first key
            input.onSubmit.AddListener(OnSubmit);
            input.onValueChanged.AddListener(_ => RefreshSuggestions());
            _console.Written += OnWritten;
            root.SetActive(false);
            logGroup.alpha = 0f;
            RefreshLog();
        }

        void OnDestroy() => _console.Written -= OnWritten;

        // Disabled when the screen is built. The log must linger after closing, so this stays on and only its contents toggle
        public void SetOpen(bool open)
        {
            gameObject.SetActive(true);
            _open = open;
            root.SetActive(open);
            _lingerUntil = Time.unscaledTime + lingerSeconds;

            if (open)
            {
                ResetHistory();
                SetText("/");   // Opened with the / key, so start typing a command right away
                input.ActivateInputField();
                return;
            }

            input.DeactivateInputField();
        }

        void Update()
        {
            logGroup.alpha = _open ? 1f : Mathf.Clamp01((_lingerUntil + fadeSeconds - Time.unscaledTime) / fadeSeconds);
            if (!_open)
                return;

            int dir = ArrowStep();
            if (dir != 0)
            {
                if (ListShown) Select(dir);
                else Navigate(dir);
            }
            else if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                if (ListShown) Accept();
                else if (_candidates.Count > 0) { _forceList = true; RenderSuggestions(); }
            }
        }

        // One step on press, then one step every repeatInterval after repeatDelay while held. Time is paused in the console, so uses unscaled time
        int ArrowStep()
        {
            Keyboard k = Keyboard.current;
            int held = k.upArrowKey.isPressed ? -1 : k.downArrowKey.isPressed ? 1 : 0;
            if (held == 0)
            {
                _heldDir = 0;
                return 0;
            }

            if (held != _heldDir || k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame)
            {
                _heldDir = held;
                _nextRepeat = Time.unscaledTime + repeatDelay;
                return held;
            }

            if (Time.unscaledTime < _nextRepeat)
                return 0;
            _nextRepeat = Time.unscaledTime + repeatInterval;
            return held;
        }

        // Undoes the input field moving the caret to the start on ↑↓
        void LateUpdate()
        {
            if (_open && _heldDir != 0)
                input.caretPosition = input.text.Length;
        }

        // Going past the bottom returns to the text typed before browsing
        void Navigate(int dir)
        {
            var history = _console.History;
            if (history.Count == 0)
                return;

            if (_historyIndex == history.Count)
                _draft = input.text;

            _historyIndex = Mathf.Clamp(_historyIndex + dir, 0, history.Count);

            SetText(_historyIndex == history.Count ? _draft : history[_historyIndex]);
        }

        void ResetHistory()
        {
            _historyIndex = _console.History.Count;
            _draft = "";
        }

        void Select(int dir)
        {
            _selected = Mathf.Clamp(_selected + dir, 0, _candidates.Count - 1);
            RenderSuggestions();
        }

        // Replaces the word being typed with the selected suggestion
        void Accept()
        {
            SetText(input.text.Substring(0, WordStart) + _candidates[_selected] + " ");
            input.ActivateInputField();
        }

        void SetText(string text)
        {
            input.text = text;
            input.caretPosition = text.Length;   // Caret to the end
        }

        void RefreshSuggestions()
        {
            _candidates = _console.Complete(input.text);
            _selected = 0;
            _forceList = false;
            RenderSuggestions();
        }

        void RenderSuggestions()
        {
            bool shown = ListShown;
            suggestionList.gameObject.SetActive(shown);
            ghostText.text = "";
            if (!shown)
                return;

            // Scrolls so the selection is visible
            int first = Mathf.Clamp(_selected - visibleSuggestions + 1, 0, Mathf.Max(0, _candidates.Count - visibleSuggestions));
            int last = Mathf.Min(_candidates.Count, first + visibleSuggestions);
            var sb = new StringBuilder();
            for (int i = first; i < last; i++)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(i == _selected ? $"<color=#FFFF55><noparse>{_candidates[i]}</noparse></color>" : $"<noparse>{_candidates[i]}</noparse>");
            }
            suggestionText.text = sb.ToString();

            string word = CurrentWord;
            string pick = _candidates[_selected];
            if (pick.Length > word.Length)
                ghostText.text = $"<alpha=#00><noparse>{input.text}</noparse><alpha=#FF><noparse>{pick.Substring(word.Length)}</noparse>";

            PlaceList();
        }

        // Aligns the list's left edge with the first letter of the word being typed
        void PlaceList()
        {
            TMP_Text text = input.textComponent;
            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            int start = Mathf.Min(WordStart, info.characterCount);
            float x = start > 0 ? info.characterInfo[start - 1].xAdvance : text.rectTransform.rect.xMin + text.margin.x;
            Vector3 world = text.rectTransform.TransformPoint(new Vector3(x, 0f, 0f));
            suggestionList.position = new Vector3(world.x, suggestionList.position.y, suggestionList.position.z);
        }

        void OnWritten(ConsoleLine _)
        {
            _lingerUntil = Time.unscaledTime + lingerSeconds;
            RefreshLog();
        }

        void RefreshLog()
        {
            var lines = _console.Lines;
            var sb = new StringBuilder();
            for (int i = Mathf.Max(0, lines.Count - visibleLines); i < lines.Count; i++)
                sb.AppendLine(Colored(lines[i]));
            logText.text = sb.ToString();
        }

        static string Colored(ConsoleLine line) => line.Kind switch
        {
            ConsoleLineKind.Input => $"<color=#9A9A9A><noparse>{line.Text}</noparse></color>",
            ConsoleLineKind.Error => $"<color=#FF6060><noparse>{line.Text}</noparse></color>",
            _ => $"<noparse>{line.Text}</noparse>",
        };

        // Enter. Closes on success; if the command fails, reopens with the typed text so it can be fixed.
        // Closes first so closing the console doesn't reset state the command changed (time stop during room transition, choice window, cutscene)
        void OnSubmit(string value)
        {
            RequestClose();
            if (_console.Submit(value))
            {
                input.text = "";
                return;
            }

            RequestOpen();
            SetText(value);
        }

        // The game's UI manager enters and exits the console state
        protected abstract void RequestClose();
        protected abstract void RequestOpen();
    }
}
