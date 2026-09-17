using OnScreenChat.Core.Config;
using OnScreenChat.Core.Data;
using OnScreenChat.Core.Llm;
using OnScreenChat.Core.Prompts;
using OnScreenChat.Core.Text;

namespace OnScreenChat.SelfTest;

/// <summary>
/// Headless checks that need no GUI — the Windows counterpart of the Mac app's
/// <c>--selftest</c> and <c>--markers</c> flags. The WinUI head has no console,
/// so these live in their own executable rather than behind an argument.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Contains("--markers"))
        {
            var index = Array.IndexOf(args, "--markers");
            return Markers(string.Join(' ', args.Skip(index + 1)));
        }

        if (args.Contains("--gemini"))
        {
            return GeminiProbe().GetAwaiter().GetResult();
        }

        if (args.Length == 0 || args.Contains("--selftest"))
        {
            return Run();
        }

        Console.WriteLine("Usage: OnScreenChat.SelfTest [--selftest] [--markers \"<text>\"] [--gemini]");
        return 1;
    }

    /// <summary>Exercises the data layer and the formatting guarantees.</summary>
    private static int Run()
    {
        // Never touch the real user profile: the self-test writes its own file.
        var sandbox = Path.Combine(Path.GetTempPath(), $"onscreenchat-selftest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sandbox);

        try
        {
            using var database = new AppDatabase(Path.Combine(sandbox, "app.sqlite"));

            var skills = database.AllSkills();
            Console.WriteLine($"✅ DB opened. Seeded {skills.Count} skills:");
            foreach (var skill in skills)
            {
                Console.WriteLine($"  • [{skill.Id}] {skill.Name} — hint: {skill.InputHint}");
            }

            // Round-trip an edit to prove save/upsert works.
            var first = skills[0];
            database.Save(first with { Name = first.Name + " (edited)" });
            var reloaded = database.AllSkills().First(skill => skill.Id == first.Id);
            Console.WriteLine($"✅ Edit round-trip: {reloaded.Name}");
            database.Save(first); // restore

            Expect(database.AllSkills().Count == skills.Count, "no duplicate on upsert",
                $"{database.AllSkills().Count}");

            // Message persistence round-trip.
            database.ClearMessages();
            database.AppendMessage(ChatRole.User, "hello", 0);
            database.AppendMessage(ChatRole.Assistant, "hi there", 1);
            var loaded = database.LoadMessages();
            Console.WriteLine($"✅ Messages persisted: {loaded.Count} " +
                              $"({string.Join(", ", loaded.Select(m => $"{m.Role}:{m.Text}"))})");
            database.ClearMessages();
            Console.WriteLine($"✅ Cleared messages: now {database.LoadMessages().Count}");

            CheckRules(database);
            CheckFormatting();
            CheckModels();

            Console.WriteLine("\nAll checks passed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine($"❌ SelfTest failed: {error}");
            return 1;
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Standing rules survive a round-trip through SQLite.</summary>
    private static void CheckRules(AppDatabase database)
    {
        var store = new RuleStore(database);
        var existing = store.Rules.Count;

        var saved = store.Add("keep it under five lines");
        Expect(saved == "keep it under five lines", "rule round-trip", saved ?? "nil");
        Expect(store.Add("KEEP IT UNDER FIVE LINES") is null, "duplicate rule rejected", "added");
        Expect(store.PromptBlock.Contains("1. keep it under five lines"),
            "rule reaches the prompt block", store.PromptBlock);

        store.Delete(store.Rules.Last());
        Expect(store.Rules.Count == existing, "rule delete", $"{store.Rules.Count}");
        Expect(store.PromptBlock.Length == 0, "empty rules add nothing to the prompt", store.PromptBlock);
    }

    /// <summary>
    /// The formatting guarantees: no <c>**</c> survives, however the stream
    /// splits, and the markers are parsed out of the input correctly.
    /// </summary>
    private static void CheckFormatting()
    {
        // Split at the worst possible points: mid-run, and a run spanning chunks.
        var stripper = new AsteriskStripper();
        var streamed = string.Empty;
        foreach (var chunk in new[] { "Hey ", "*", "*bold", "*", "*", " and ", "***loud***", " and 2*3" })
        {
            streamed += stripper.Push(chunk);
        }
        streamed += stripper.Flush();
        Expect(streamed == "Hey bold and loud and 2*3", "asterisk stripping", streamed);
        Expect(!ResponseStyle.StripAsterisks("a **b** c").Contains("**"), "no ** remains", "ok");

        var (message, commands) = PromptBuilder.ExtractCommands(
            "/command {always be brief} summarize this /COMMAND {no emojis}");
        Expect(message == "summarize this", "command stripped from message", message);
        Expect(commands.SequenceEqual(["always be brief", "no emojis"]), "commands captured",
            string.Join(", ", commands));

        var (body, directive) = PromptBuilder.ExtractDirective("reply WW:{one line}");
        Expect(body == "reply" && directive == "one line", "directive parsing", $"{body} / {directive}");

        const string messy = "One.\n\n\n\nTwo.   \n\nThree.";
        Expect(ResponseStyle.Normalized(messy) == "One.\n\nTwo.\n\nThree.", "blank-line collapse",
            ResponseStyle.Normalized(messy));
        Expect(ResponseStyle.Paragraphs(messy).Count == 3, "paragraph split",
            $"{ResponseStyle.Paragraphs(messy).Count}");
    }

    /// <summary>The model picker and the key it depends on.</summary>
    private static void CheckModels()
    {
        Expect(ModelOption.All.Count == 2, "two Gemini models offered", $"{ModelOption.All.Count}");
        Expect(ModelOption.All.All(option => option.Provider == ModelProvider.Gemini),
            "no local models in the Windows build", "found a non-Gemini provider");
        Expect(ModelOption.Option("nonsense").Id == ModelOption.Default.Id,
            "unknown model id falls back to the default", ModelOption.Option("nonsense").Id);

        var key = EnvLoader.Value("GEMINI_API_KEY");
        Console.WriteLine(string.IsNullOrEmpty(key)
            ? "⚠️  No GEMINI_API_KEY found — the app will run, but every send will show the missing-key notice."
            : "✅ GEMINI_API_KEY found.");
    }

    /// <summary>
    /// Dry-run of the input markers. Shows exactly what the app would strip,
    /// save, steer with, and send, without writing anything or calling a model.
    /// </summary>
    private static int Markers(string text)
    {
        var input = text.Trim();
        if (input.Length == 0)
        {
            Console.WriteLine(
                "Usage: OnScreenChat.SelfTest --markers \"summarize this /command {no emojis} WW:{one line}\"");
            return 1;
        }

        var (afterCommands, commands) = PromptBuilder.ExtractCommands(input);
        var (afterDirective, directive) = PromptBuilder.ExtractDirective(afterCommands);
        var message = afterDirective.Trim();

        Console.WriteLine($"Input          {input}");
        Console.WriteLine();
        Console.WriteLine("/command  →  standing rules, saved and applied to EVERY future reply");
        Console.WriteLine(commands.Count == 0
            ? "   (none)"
            : string.Join("\n", commands.Select(command => $"   • {command}")));
        Console.WriteLine("WW:       →  steers THIS reply only, then discarded");
        Console.WriteLine($"   {(directive is null ? "(none)" : $"• {directive}")}");
        Console.WriteLine();

        if (message.Length == 0 && commands.Count > 0)
        {
            Console.WriteLine("Sent to model  (nothing — rules only)");
            Console.WriteLine("Action         saves the rule(s), shows a confirmation note, no request");
        }
        else
        {
            var outgoing = message.Length == 0 ? input : message;
            Console.WriteLine($"Sent to model  \"{outgoing}\"");
            Console.WriteLine(message.Length == 0
                ? "Action         no message left to steer, so the raw text is sent as written"
                : "Action         sends that text; markers above never reach the model as text");
        }

        Console.WriteLine();
        Console.WriteLine("Dry run — nothing was written.");
        return 0;
    }

    /// <summary>Streams one real reply from Gemini, to prove the transport end-to-end.</summary>
    private static async Task<int> GeminiProbe()
    {
        var key = EnvLoader.Value("GEMINI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine(GeminiProtocol.MissingKeyMessage);
            return 1;
        }

        var model = ModelOption.Default;
        var client = new GeminiClient(model.ModelName, key);
        var messages = new[] { new ChatMessage(ChatRole.User, "Say hello in one short sentence.") };
        var prompt = PromptBuilder.SystemPrompt(SkillDefaults.Fallback.SystemPrompt, string.Empty, null);

        Console.WriteLine($"Streaming from {model.Label}…\n");

        var stripper = new AsteriskStripper();
        var received = 0;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var chunk in client.StreamAsync(messages, prompt, cancellation.Token))
        {
            Console.Write(stripper.Push(chunk));
            received++;
        }
        Console.Write(stripper.Flush());
        Console.WriteLine($"\n\n{(received > 0 ? "✅" : "❌")} Received {received} chunk(s).");
        return received > 0 ? 0 : 1;
    }

    private static void Expect(bool condition, string label, string got)
    {
        if (condition)
        {
            Console.WriteLine($"✅ {label}");
            return;
        }

        Console.WriteLine($"❌ {label} — got: {got}");
        Environment.Exit(1);
    }
}
