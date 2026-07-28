#nullable enable
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Content.Client.CharacterInfo;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests.Chat;

public sealed class ChatHighlightTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly IConfigurationManager _configManager = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _uiManager = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestCustomHighlightsPreserved()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();

        // 1. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify they are saved
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // 3. Simulate character update
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            "Captain",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "John Doe"
        );

        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.That(method, Is.Not.Null);

        // Set internal state to allow character update processing
        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);
        attachField.SetValue(chatController, true);

        // Invoke update
        method.Invoke(chatController, new object[] { characterData });

        // 4. Assertions:
        // - Custom highlights in config must remain unchanged
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Internal active regex highlights must contain both custom & auto-filled highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        var speechQuoteField = chatController.GetType().GetField(
            "_chatSpeechDoubleQuoteBegin",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(speechQuoteField, Is.Not.Null);
        var quoteChar = (string)speechQuoteField.GetValue(chatController)!;

        // Check that custom and auto highlights are loaded
        // Custom:
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        // Auto:
        Assert.That(activeHighlights, Contains.Item("Captain"));
        Assert.That(activeHighlights, Contains.Item("(?<!\\w)Cap(?!\\w)")); // "Cap" becomes regex-escaped and word-bounded
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)John\ Doe(?!\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)John(?!\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)Doe(?!\w)"));

        // 5. Disable auto-fill highlights and verify auto-filled highlights are removed
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights, Does.Not.Contain("Captain"));
        Assert.That(activeHighlights, Does.Not.Contain($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)John\ Doe(?!\w)"));
        Assert.That(activeHighlights, Does.Not.Contain($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)John(?!\w)"));
        Assert.That(activeHighlights, Does.Not.Contain($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)Doe(?!\w)"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestEnablingAutoFillPreservesCustomHighlights()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();

        // 1. Start with auto-fill disabled
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify active matches are ONLY custom highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights.Count, Is.EqualTo(2));

        // 3. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 4. Simulate character update (spawning into round)
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            "Captain",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "John Doe"
        );

        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.That(method, Is.Not.Null);

        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);
        attachField.SetValue(chatController, true);

        // Invoke character update
        method.Invoke(chatController, new object[] { characterData });

        // 5. Assertions:
        // - Config highlights MUST NOT be wiped and remain as custom highlights
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Active highlights list must now merge both custom and auto-filled ones
        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        var speechQuoteField = chatController.GetType().GetField(
            "_chatSpeechDoubleQuoteBegin",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(speechQuoteField, Is.Not.Null);
        var quoteChar = (string)speechQuoteField.GetValue(chatController)!;

        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights, Contains.Item("Captain"));
        Assert.That(activeHighlights, Contains.Item("(?<!\\w)Cap(?!\\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)John\ Doe(?!\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)John(?!\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)Doe(?!\w)"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestNameHighlightSplittingAndFiltering()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);

        var speechQuoteField = chatController.GetType().GetField(
            "_chatSpeechDoubleQuoteBegin",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(speechQuoteField, Is.Not.Null);
        var quoteChar = (string)speechQuoteField.GetValue(chatController)!;

        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);

        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(method, Is.Not.Null);

        // Case 1: Cyborg name with single letters and digits ("C-3-D2")
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            "Cyborg",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "C-3-D2"
        );
        attachField.SetValue(chatController, true);
        method.Invoke(chatController, new object[] { characterData });

        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        
        // C-3-D2 should be highlighted, as well as D2
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)C-3-D2(?!\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)D2(?!\w)"));
        
        // Single characters and digits (C, 3) must be excluded
        foreach (var highlight in activeHighlights)
        {
            Assert.That(highlight, Is.Not.EqualTo($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)C(?!\w)"));
            Assert.That(highlight, Is.Not.EqualTo($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)3(?!\w)"));
        }

        // Case 2: Multi-hyphen name with long parts ("Eats-The-Food")
        var eatsTheFoodData = new CharacterInfoSystem.CharacterData(
            default,
            "Civilian",
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            "Eats-The-Food"
        );
        attachField.SetValue(chatController, true);
        method.Invoke(chatController, new object[] { eatsTheFoodData });
        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        // "Eats-The-Food", "Eats", and "Food" should be highlighted
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)Eats-The-Food(?!\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)Eats(?!\w)"));
        Assert.That(activeHighlights, Contains.Item($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)Food(?!\w)"));
        
        // "The" is in IgnoredNameParts so it must not be highlighted
        foreach (var highlight in activeHighlights)
        {
            Assert.That(highlight, Is.Not.EqualTo($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)The(?!\w)"));
            Assert.That(highlight, Is.Not.EqualTo($@"(?<=(?<=^.?OOC:.*:.*)|(?<=,.*{quoteChar}.*)|(?<=\n.*))(?<!\w)the(?!\w)"));
        }
    }
}
