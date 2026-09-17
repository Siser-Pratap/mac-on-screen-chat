using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OnScreenChat.Core.Chat;
using OnScreenChat.Core.Config;
using OnScreenChat.Core.Data;
using OnScreenChat.Core.Llm;
using OnScreenChat.Core.Prompts;

namespace OnScreenChat.ViewModels;

/// <summary>
/// Binds the UI to <see cref="ChatSession"/> and the stores. Deliberately thin:
/// the send/stop/ordering rules live in Core where they are tested, and this
/// layer only holds selection state and exposes it to XAML.
/// </summary>
public sealed class ChatViewModel : INotifyPropertyChanged
{
    private readonly AppDatabase _database;
    private readonly SettingsStore _settings;

    private Skill _selectedSkill;
    private ModelOption _selectedModel;
    private string _inputText = string.Empty;

    public ChatViewModel(AppDatabase database, SettingsStore settings)
    {
        _database = database;
        _settings = settings;

        Skills = new SkillStore(database);
        Rules = new RuleStore(database);
        Session = new ChatSession(database, Rules);

        _selectedSkill = Skills.Skills.Count > 0 ? Skills.Skills[0] : SkillDefaults.Fallback;
        _selectedModel = ModelOption.Option(settings.SelectedModelId);

        Session.StreamingChanged += () =>
        {
            Raise(nameof(IsStreaming));
            Raise(nameof(IsNotStreaming));
            Raise(nameof(CanSend));
        };

        Skills.Changed += () => Raise(nameof(SkillOptions));
    }

    public ChatSession Session { get; }

    public SkillStore Skills { get; }

    public RuleStore Rules { get; }

    public ObservableCollection<ChatMessage> Messages => Session.Messages;

    public IReadOnlyList<Skill> SkillOptions => Skills.Skills;

    public IReadOnlyList<ModelOption> ModelOptions => ModelOption.All;

    public Skill SelectedSkill
    {
        get => _selectedSkill;
        set
        {
            if (_selectedSkill.Id == value.Id && _selectedSkill.Name == value.Name) return;
            _selectedSkill = value;
            Raise(nameof(SelectedSkill));
            Raise(nameof(InputHint));
            Raise(nameof(ShowsHeatDial));
        }
    }

    public ModelOption SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (_selectedModel.Id == value.Id) return;
            _selectedModel = value;
            _settings.SelectedModelId = value.Id;
            Raise(nameof(SelectedModel));
        }
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText == value) return;
            _inputText = value;
            Raise(nameof(InputText));
            Raise(nameof(CanSend));
        }
    }

    public string InputHint => SelectedSkill.InputHint;

    public bool IsStreaming => Session.IsStreaming;

    public bool IsNotStreaming => !Session.IsStreaming;

    public bool CanSend => !string.IsNullOrWhiteSpace(InputText) && !IsStreaming;

    /// <summary>The dating-only controls apply to exactly one skill.</summary>
    public bool ShowsHeatDial => SelectedSkill.Id == DatingHeatExtensions.SkillId;

    public IReadOnlyList<string> HeatLabels { get; } =
        Enum.GetValues<DatingHeat>().Select(heat => heat.Label()).ToList();

    public int HeatIndex
    {
        get => (int)DatingHeatExtensions.Parse(_settings.DatingHeat);
        set
        {
            if (value < 0) return;
            _settings.DatingHeat = ((DatingHeat)value).ToString().ToLowerInvariant();
            Raise(nameof(HeatIndex));
        }
    }

    public string DatingStyle
    {
        get => _settings.DatingStyle;
        set
        {
            _settings.DatingStyle = value;
            Raise(nameof(DatingStyle));
        }
    }

    /// <summary>
    /// The system prompt actually sent: the skill's prompt, plus — for the dating
    /// skill — my texting-style block and the heat calibration.
    /// </summary>
    public string EffectivePrompt
    {
        get
        {
            if (!ShowsHeatDial) return SelectedSkill.SystemPrompt;

            var prompt = SelectedSkill.SystemPrompt;
            var style = DatingStyle.Trim();
            if (style.Length > 0)
            {
                prompt += "\n\nMY TEXTING STYLE (write all three options in THIS voice — " +
                          $"my humor, slang, and message length):\n{style}";
            }

            return prompt + DatingHeatExtensions.Parse(_settings.DatingHeat).PromptDirective();
        }
    }

    public async Task SendAsync()
    {
        if (!CanSend) return;

        var text = InputText;
        InputText = string.Empty;

        await Session.SendAsync(text, EffectivePrompt, CreateClient());
    }

    public void Stop() => Session.Stop();

    public void NewChat()
    {
        Session.NewChat();
        InputText = string.Empty;
    }

    public void SaveSkill(Skill skill)
    {
        Skills.Save(skill);
        if (skill.Id == SelectedSkill.Id) SelectedSkill = skill;
    }

    /// <summary>
    /// With local models dropped there is no fallback, so a missing key becomes
    /// a message in the transcript rather than a silent failure.
    /// </summary>
    private ILlmClient CreateClient()
    {
        var key = EnvLoader.Value("GEMINI_API_KEY");
        return string.IsNullOrEmpty(key)
            ? new StaticClient(GeminiProtocol.MissingKeyMessage)
            : new GeminiClient(SelectedModel.ModelName, key);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
