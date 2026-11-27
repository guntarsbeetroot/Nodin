using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nodin.Examples
{
    internal sealed class CollectionExamples : ExamplePage
    {
        [Serializable]
        public sealed class Route
        {
            [TableColumnWidth(130, Resizable = false)]
            public string destination;

            [TableColumnWidth(75)]
            [MinValue(0)]
            public int stops;

            [HideInTables]
            public string internalCode;

            public Route() { }

            public Route(string destination, int stops, string internalCode) =>
                (this.destination, this.stops, this.internalCode) = (destination, stops, internalCode);
        }

        [Searchable]
        [ListDrawerSettings(Expanded = true, NumberOfItemsPerPage = 3, ListElementLabelName = nameof(Route.destination),
            ShowIndexLabels = true, CustomAddFunction = nameof(NewRoute))]
        public List<Route> searchableRoutes = new()
        {
            new("Briar", 2, "B01"), new("Hollow", 3, "H02"), new("Marsh", 1, "M03"), new("Ridge", 5, "R04")
        };

        [TableList(AlwaysExpanded = true, ShowPaging = true, NumberOfItemsPerPage = 3)]
        public Route[] scheduledRoutes = { new("Harbor", 2, "X58"), new("Quarry", 4, "Q99") };

        [ValueDropdown(nameof(ServiceLevels), DropdownTitle = "Service level", ExpandAllMenuItems = true)]
        public int serviceLevel = 2;

        [ValueDropdown(nameof(ServiceLevels), IsUniqueList = true, ExcludeExistingValuesInList = true)]
        [ListDrawerSettings(Expanded = true)]
        public List<int> enabledServiceLevels = new() { 1 };

        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.ExpandedFoldout,
            KeyLabel = "Stop name", ValueLabel = "Daily arrivals")]
        public Dictionary<string, int> stopCapacity = new() { ["Harbor"] = 24, ["Marsh"] = 5 };

        [TableMatrix(HorizontalTitle = "Occupancy", VerticalTitle = "Rows are shifts", SquareCells = true)]
        public int[,] occupancy = { { 1, 5 }, { 4, 2 }, { 2, 0 } };

        [TableMatrix(HorizontalTitle = "Grid (read only)", Transpose = true, IsReadOnly = true, HideRowIndices = true)]
        public string[,] readOnlyGrid = { { "A1", "A2" }, { "B1", "B2" } };

        private IEnumerable<ValueDropdownItem<int>> ServiceLevels => new[]
        {
            new ValueDropdownItem<int>("Local/Standard", 1),
            new ValueDropdownItem<int>("Local/Express", 2),
            new ValueDropdownItem<int>("Regional/Night", 3)
        };

        private Route NewRoute() => new("New stop", 0, "NEW");
    }
}
