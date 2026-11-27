using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nodin.Editor.Tests
{
    public sealed class ExpressionTests
    {
        private static IEnumerable<TestCaseData> ExpressionCases()
        {
            yield return new TestCaseData("@!_hasAuthToken", true);
            yield return new TestCaseData("@buildStep == 1", true);
            yield return new TestCaseData("@buildStep == 1 && IsAndroid", true);
            yield return new TestCaseData("@buildStep == 1 && IsWebGL", false);
            yield return new TestCaseData("@buildStep == 2", false);
            yield return new TestCaseData("@buildStep == 0", false);
            yield return new TestCaseData("@buildStep == 2 && buildIsFinished == true", false);
            yield return new TestCaseData("@!controlledByTimeline", true);
            yield return new TestCaseData("@controlledBySignal", true);
            yield return new TestCaseData("@!string.IsNullOrEmpty(id)", true);
            yield return new TestCaseData("@amount != 0", true);
            yield return new TestCaseData("@!string.IsNullOrEmpty(text)", true);
            yield return new TestCaseData("@!string.IsNullOrEmpty(categoryId)", true);
            yield return new TestCaseData("@!string.IsNullOrEmpty(itemId)", true);
            yield return new TestCaseData("@!string.IsNullOrEmpty(presetId)", true);
            yield return new TestCaseData("@!string.IsNullOrEmpty(groupName)", true);
            yield return new TestCaseData("@!string.IsNullOrEmpty(entryId)", true);
            yield return new TestCaseData("@items != null && items.Length > 0", true);
            yield return new TestCaseData("@enabled && fogMode == UnityEngine.FogMode.Linear", true);
            yield return new TestCaseData("@enabled && fogMode != UnityEngine.FogMode.Linear", false);
            yield return new TestCaseData("@SelectedObject != null", true);
            yield return new TestCaseData("@!groupEnabled", true);
            yield return new TestCaseData("@groupEnabled", false);
            yield return new TestCaseData("@this.metadataType == MetadataType.Texture", false);
            yield return new TestCaseData("@this.metadataType == MetadataType.Material", true);
            yield return new TestCaseData("$GetTitle", "fixture-title");
            yield return new TestCaseData("$HighlightTitle", "fixture-highlight");
            yield return new TestCaseData("@buildStep >= 0", true);
            yield return new TestCaseData("@buildStep >= 1", true);
            yield return new TestCaseData("@CanBuild()", true);
            yield return new TestCaseData("@buildStep >= 2", false);
            yield return new TestCaseData("@UnityEngine.Application.isPlaying", Application.isPlaying);
            yield return new TestCaseData("@previewSettings != null && SelectedObject != null && ChildrenCount > 0", true);
            yield return new TestCaseData("@!useDefaults", true);
            yield return new TestCaseData("@!useDefaults&&!useCustomSettings", true);
            yield return new TestCaseData("@itemData != null && !string.IsNullOrEmpty(itemData.id)", true);
            yield return new TestCaseData("@GetBuildButtonLabel()", "fixture-build");
            yield return new TestCaseData("$GetButtonName", "fixture-button");
            yield return new TestCaseData("@Name", "fixture-name");
            yield return new TestCaseData("@loading.color", Color.cyan);
            yield return new TestCaseData("@inEditor.color", Color.cyan);
            yield return new TestCaseData("@header.color", Color.cyan);
            yield return new TestCaseData("@thumbnail.color", Color.cyan);
            yield return new TestCaseData("@background.color", Color.cyan);
            yield return new TestCaseData("@foreground.color", Color.cyan);
            yield return new TestCaseData("@outline.color", Color.cyan);
            yield return new TestCaseData("@selection.color", Color.cyan);
            yield return new TestCaseData("@highlight.color", Color.cyan);
            yield return new TestCaseData("@help.color", Color.cyan);
            yield return new TestCaseData("@summary.color", Color.cyan);
            yield return new TestCaseData("@warning.color", Color.cyan);
            yield return new TestCaseData("@error.color", Color.cyan);
            yield return new TestCaseData("@primaryAction.color", Color.cyan);
            yield return new TestCaseData("@secondaryAction.color", Color.cyan);
            yield return new TestCaseData("@addAction.color", Color.cyan);
            yield return new TestCaseData("@removeAction.color", Color.cyan);
            yield return new TestCaseData("@categories.color", Color.cyan);
            yield return new TestCaseData("@labels.color", Color.cyan);
            yield return new TestCaseData("@icons.color", Color.cyan);
            yield return new TestCaseData("@preview.color", Color.cyan);
            yield return new TestCaseData("@navigation.color", Color.cyan);
            yield return new TestCaseData("@overlays.color", Color.cyan);
            yield return new TestCaseData("@audio.color", Color.cyan);
            yield return new TestCaseData("@Overview.StatusColor", Color.cyan);
            yield return new TestCaseData("@Input.StatusColor", Color.cyan);
            yield return new TestCaseData("@Output.StatusColor", Color.cyan);
            yield return new TestCaseData("@Defaults.StatusColor", Color.cyan);
            yield return new TestCaseData("@Overrides.StatusColor", Color.cyan);
            yield return new TestCaseData("@Rendering.StatusColor", Color.cyan);
            yield return new TestCaseData("@Lighting.StatusColor", Color.cyan);
            yield return new TestCaseData("@Materials.StatusColor", Color.cyan);
            yield return new TestCaseData("@Textures.StatusColor", Color.cyan);
            yield return new TestCaseData("@Geometry.StatusColor", Color.cyan);
            yield return new TestCaseData("@Animation.StatusColor", Color.cyan);
            yield return new TestCaseData("@Import.StatusColor", Color.cyan);
            yield return new TestCaseData("@Export.StatusColor", Color.cyan);
            yield return new TestCaseData("$GetButtonColor", Color.cyan);
            yield return new TestCaseData("@previewAsset == null", true);
        }

        [TestCaseSource(nameof(ExpressionCases))]
        public void EvaluatesExpression(string expression, object expected)
        {
            Assert.That(NodinExpression.Evaluate(new ExpressionFixture(), expression), Is.EqualTo(expected));
        }

        [Test]
        public void LogicalBranchesDoNotEvaluateNullOrUnknownMembers()
        {
            var fixture = new ExpressionFixture { items = null, itemData = null };
            Assert.That(NodinExpression.Evaluate(fixture, "@items != null && items.Length > 0"), Is.False);
            Assert.That(NodinExpression.Evaluate(fixture, "@itemData != null && !string.IsNullOrEmpty(itemData.id)"), Is.False);
            Assert.That(NodinExpression.Evaluate(fixture, "@true || unknown.Value"), Is.True);
            Assert.That(NodinExpression.Evaluate(fixture, "@false ? unknown.Value : (3 + 2 * 4 >= 9)"), Is.True);
        }

        [Test]
        public void InvalidReferenceReturnsDiagnostic()
        {
            Assert.That(NodinExpression.TryEvaluate(new ExpressionFixture(), "@missingMethod()", out _, out var error), Is.False);
            Assert.That(error, Does.Contain("missingMethod"));
            Assert.That(NodinExpression.TryEvaluate(new ExpressionFixture(), "@buildStep = 1", out _, out error), Is.False);
            Assert.That(error, Does.Contain("token"));
        }
    }
}
