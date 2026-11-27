using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Nodin.Editor.Tests
{
    public sealed class LayoutAndArgumentTests
    {
        [Test]
        public void TabWidthsFitTheInspectorAndFavorLongerLabels()
        {
            var minimum = new[] { 80f, 45f, 78f, 44f, 50f, 63f };
            var widths = NodinInspectorLayout.TabWidths(minimum, 370);
            Assert.That(widths.Sum(), Is.EqualTo(370).Within(0.001));
            Assert.That(widths[0], Is.GreaterThan(widths[1]));
            Assert.That(NodinInspectorLayout.TabWidths(minimum, 210).Sum(), Is.EqualTo(210).Within(0.001));
        }

        [Test]
        public void TableColumnsDistributeSpaceAfterFixedColumns()
        {
            var widths = NodinCollections.TableWidths(new int?[] { 160, null, null }, 540);
            Assert.That(widths, Is.EqualTo(new[] { 160f, 187f, 187f }));
            var narrower = NodinCollections.TableWidths(new int?[] { 160, null, null }, 250);
            Assert.That(narrower, Is.EqualTo(new[] { 160f, 55f, 55f }));
        }

        [Test]
        public void CursorTableKeepsBothPreviewsAndTheHotspotAtNarrowWidths()
        {
            var requested = new int?[] { 100, null, null, null };
            var contentWidths = new[] { 0f, 40f, 58f, 110f };
            Assert.That(NodinCollections.TableWidths(requested, 400, contentWidths),
                Is.EqualTo(new[] { 100f, 40f, 58f, 110f }));
            Assert.That(NodinCollections.TableWidths(requested, 180, contentWidths),
                Is.EqualTo(new[] { 100f, 40f, 58f, 110f }));
        }

        [Test]
        public void PreviewDefaultsMatchMeasuredInspectorGeometry()
        {
            var preview = new PreviewFieldAttribute();
            Assert.That(preview.Height, Is.EqualTo(50));
            Assert.That(preview.Alignment, Is.EqualTo(ObjectFieldAlignment.Right));
            Assert.That(new PreviewFieldAttribute(80, ObjectFieldAlignment.Left).Alignment,
                Is.EqualTo(ObjectFieldAlignment.Left));
        }

        [Test]
        public void ShortTabPathComposesWithItsExplicitTabGroup()
        {
            var tab = new TabGroupAttribute("root", "Body");
            var box = new BoxGroupAttribute("Body/Import preview");
            var paths = new Dictionary<string, string[]> { ["Body"] = new[] { "root/Body" } };
            Assert.That(NodinInspectorLayout.NormalizeGroupPath(box, tab, paths), Is.EqualTo("root/Body/Import preview"));
            Assert.That(NodinInspectorLayout.NormalizeGroupPath(box, null, paths), Is.EqualTo("root/Body/Import preview"));
            Assert.That(NodinInspectorLayout.NormalizeGroupPath(new BoxGroupAttribute("root/Body/Import preview"), tab, paths),
                Is.EqualTo("root/Body/Import preview"));
        }

        [Test]
        public void BrowsePreservesAssetPathsAndSupportsParentRelativePaths()
        {
            var project = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Project"));
            var parent = Path.Combine(project, "Assets");
            var current = "Assets/Old/file.png";
            var selected = Path.Combine(parent, "New", "file.png");
            Assert.That(NodinValueControls.GetInitialPath(project, parent, current),
                Is.EqualTo(Path.Combine(parent, "Old", "file.png")));
            Assert.That(NodinValueControls.StorePath(project, parent, current, selected, false), Is.EqualTo("Assets/New/file.png"));
            Assert.That(NodinValueControls.StorePath(project, parent, "Old/file.png", selected, false), Is.EqualTo("New/file.png"));
            Assert.That(NodinValueControls.StorePath(project, parent, "", selected, false), Is.EqualTo("Assets/New/file.png"));
            Assert.That(NodinValueControls.StorePath(project, parent, current, selected, true), Is.EqualTo(selected));
        }

        public class Arguments
        {
            public readonly string constant = "fixed";
            public int count = 2;
            public List<string> keys = new();
        }

        [Test]
        public void StructuredButtonArgumentsExposeWritablePublicFieldsAndCanCreateLists()
        {
            var fields = NodinMethodArguments.EditableFields(typeof(Arguments)).Select(field => field.Name).ToArray();
            Assert.That(fields, Is.EquivalentTo(new[] { "count", "keys" }));
            Assert.That(NodinMethodArguments.Create(typeof(Arguments), null), Is.TypeOf<Arguments>());
            Assert.That(NodinMethodArguments.Create(typeof(IList<string>), typeof(string)), Is.TypeOf<List<string>>());
            Assert.That(NodinMethodArguments.Create(typeof(string[]), typeof(string)), Is.TypeOf<string[]>());
        }
        public struct Schedule
        {
            public int hour;
        }

        public class Dispatch
        {
            public Schedule window = new Schedule { hour = 8 };
            public List<Schedule> stops = new() { new Schedule { hour = 9 } };
        }

        [Test]
        public void NestedStructArgumentsAreWrittenBackFromTheirEditableBoxes()
        {
            var dispatch = new Dispatch();
            var window = typeof(Dispatch).GetField(nameof(Dispatch.window));
            var hour = typeof(Schedule).GetField(nameof(Schedule.hour));

            object UpdateHour(object boxed)
            {
                hour.SetValue(boxed, 13);
                return boxed;
            }

            NodinMethodArguments.EditField(dispatch, window, UpdateHour);
            NodinMethodArguments.EditListElement(dispatch.stops, 0, typeof(Schedule), UpdateHour);

            Assert.That(dispatch.window.hour, Is.EqualTo(13));
            Assert.That(dispatch.stops[0].hour, Is.EqualTo(13));
        }
    }
}
