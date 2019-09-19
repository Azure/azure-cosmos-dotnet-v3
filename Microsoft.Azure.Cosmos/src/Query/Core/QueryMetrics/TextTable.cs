//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Query runtime execution times in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class TextTable
    {
        private const char CellLeftTop = '┌';
        private const char CellRightTop = '┐';
        private const char CellLeftBottom = '└';
        private const char CellRightBottom = '┘';
        private const char CellHorizontalJointTop = '┬';
        private const char CellHorizontalJointBottom = '┴';
        private const char CellVerticalJointLeft = '├';
        private const char CellTJoint = '┼';
        private const char CellVerticalJointRight = '┤';
        private const char CellHorizontalLine = '─';
        private const char CellVerticalLine = '│';

        private const int PaddingLength = 3;

        private readonly List<Column> columns;

        private readonly string header;
        private readonly string topLine;
        private readonly string middleLine;
        private readonly string bottomLine;

        private readonly string rowFormatString;

        /// <summary>
        /// Initializes a new instance of the TextTable class.
        /// </summary>
        /// <param name="columns">The columns of the table.</param>
        public TextTable(params Column[] columns)
        {
            this.columns = new List<Column>(columns);

            // Building the table header
            string headerFormatString = TextTable.BuildLineFormatString("{{{0},-{1}}}", columns);
            this.header = string.Format(
                headerFormatString,
                columns.Select(textTableColumn => textTableColumn.ColumnName).ToArray());

            // building the different lines
            this.topLine = TextTable.BuildLine(CellLeftTop, CellRightTop, CellHorizontalJointTop, columns);
            this.middleLine = TextTable.BuildLine(CellVerticalJointLeft, CellVerticalJointRight, CellTJoint, columns);
            this.bottomLine = TextTable.BuildLine(CellLeftBottom, CellRightBottom, CellHorizontalJointBottom, columns);

            // building the row format string
            this.rowFormatString = TextTable.BuildLineFormatString("{{{0},{1}}}", columns);
        }

        public string Header
        {
            get
            {
                return this.header;
            }
        }

        public string TopLine
        {
            get
            {
                return this.topLine;
            }
        }

        public string MiddleLine
        {
            get
            {
                return this.middleLine;
            }
        }

        public string BottomLine
        {
            get
            {
                return this.bottomLine;
            }
        }

        public string GetRow(params object[] cells)
        {
            if (cells.Length != this.columns.Count)
            {
                throw new ArgumentException("Cells in a row needs to have exactly 1 element per column");
            }

            return string.Format(this.rowFormatString, cells);
        }

        private static string BuildLine(char firstChar, char lastChar, char seperator, IEnumerable<Column> columns)
        {
            StringBuilder lineStringBuilder = new StringBuilder();
            lineStringBuilder.Append(firstChar);
            foreach (Column column in columns.Take(columns.Count() - 1))
            {
                lineStringBuilder.Append(CellHorizontalLine, column.ColumnWidth);
                lineStringBuilder.Append(seperator);
            }

            lineStringBuilder.Append(CellHorizontalLine, columns.Last().ColumnWidth);
            lineStringBuilder.Append(lastChar);

            return lineStringBuilder.ToString();
        }

        private static string BuildLineFormatString(string cellFormatString, IEnumerable<Column> columns)
        {
            StringBuilder lineFormatStringBuilder = new StringBuilder();
            lineFormatStringBuilder.Append(CellVerticalLine);
            int index = 0;
            foreach (Column column in columns)
            {
                lineFormatStringBuilder.Append(
                    string.Format(
                        cellFormatString,
                        index++,
                        column.ColumnWidth));
                lineFormatStringBuilder.Append(CellVerticalLine);
            }

            return lineFormatStringBuilder.ToString();
        }

        internal struct Column
        {
            public readonly string ColumnName;
            public readonly int ColumnWidth;
            // TODO (brchon): accept a format string, so that all string interpolation is pushed down.

            public Column(string columnName, int columnWidth)
            {
                this.ColumnName = columnName;
                this.ColumnWidth = columnWidth;
            }
        }
    }
}