using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SimpleKVM.GUI
{
    public class ListViewEx<T> : ListView
    {
        public ListViewEx(IList<(string ColumnName, Func<T, object> ValueLookup, Func<T, string> DisplayStringLookup)> columnInfo) : base()
        {
            ColumnInfo = columnInfo;
            DoubleBuffered = true;

            //Create the columns
            columnInfo
                .Select(columnInfo => columnInfo.ColumnName)
                .ToList()
                .ForEach(columnName =>
                {
                    var col = Columns.Add(columnName);
                    col.Width = -2;
                });

            //Add the sorter
            lvwColumnSorter = new ListViewColumnSorter<T>(columnInfo)
            {
                SortOrder = SortOrder.None
            };

            ListViewItemSorter = lvwColumnSorter;
            ColumnClick += ListViewEx_ColumnClick;
        }

        IList<(string ColumnName, Func<T, object> ValueLookup, Func<T, string> DisplayStringLookup)> ColumnInfo { get; }

        private readonly ListViewColumnSorter<T> lvwColumnSorter;

        public void Add(T item)
        {
            var lvi = Items.Add("");
            lvi.Tag = item;

            RefreshContent();
        }

        public void AddRange(IList<T> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        public void Remove(T item)
        {
            if (item == null) return;
            if (Items == null) return;

            var lvi = Items
                        .Cast<ListViewItem>()
                        .Select(lvi => new
                        {
                            ListViewItem = lvi,
                            Obj = (T)lvi.Tag
                        })
                        .First(lvi => item.Equals(lvi.Obj))
                        .ListViewItem;

            Items.Remove(lvi);

            RefreshContent();
        }

        public List<T> GetSelectedItems()
        {
            var result = SelectedItems
                            .OfType<ListViewItem>()
                            .Select(lvi => (T)lvi.Tag)
                            .ToList();

            return result;
        }

        public List<T> GetItems()
        {
            var result = Items
                            .OfType<ListViewItem>()
                            .Select(lvi => (T)lvi.Tag)
                            .ToList();

            return result;
        }

        public void RefreshContent()
        {
            var columnsChanged = new List<int>();

            Items
                .Cast<ListViewItem>()
                .Select(lvi => new
                {
                    ListViewItem = lvi,
                    Obj = (T)lvi.Tag
                })
                .ToList()
                .ForEach(lvi =>
                {
                    //Update the contents of this ListViewItem
                    ColumnInfo
                        .Select((column, index) => new
                        {
                            Column = column,
                            Index = index
                        })
                        .ToList()
                        .ForEach(col =>
                        {
                            var newDisplayValue = col.Column.DisplayStringLookup(lvi.Obj);
                            if (lvi.ListViewItem.SubItems.Count <= col.Index)
                            {
                                lvi.ListViewItem.SubItems.Add("");
                            }

                            var subitem = lvi.ListViewItem.SubItems[col.Index];
                            var oldDisplayValue = subitem.Text ?? "";

                            if (!oldDisplayValue.Equals(newDisplayValue))
                            {
                                subitem.Text = newDisplayValue;
                                columnsChanged.Add(col.Index);
                            }
                        });
                });

            columnsChanged.ForEach(col => { Columns[col].Width = -2; });

            //AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            //AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void ListViewEx_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (e.Column == lvwColumnSorter.ColumnToSort)
            {
                if (lvwColumnSorter.SortOrder == SortOrder.Ascending)
                {
                    lvwColumnSorter.SortOrder = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.SortOrder = SortOrder.Ascending;
                }
            }
            else
            {
                lvwColumnSorter.ColumnToSort = e.Column;
                lvwColumnSorter.SortOrder = SortOrder.Ascending;
            }

            Sort();
        }
    }

    public class ListViewColumnSorter<T> : IComparer
    {
        public ListViewColumnSorter(IList<(string ColumnName, Func<T, object> ValueLookup, Func<T, string> DisplayStringLookup)> columnInfo)
        {
            ColumnInfo = columnInfo;
        }

        public int Compare(object? x, object? y)
        {
            if (x == null || y == null) return 0;

            int compareResult;

            var listviewX = (ListViewItem)x;
            var listviewY = (ListViewItem)y;

            var objX = (T)listviewX.Tag;
            var objY = (T)listviewY.Tag;

            if (objX == null || objY == null) return 0;

            var valueX = ColumnInfo[ColumnToSort].ValueLookup(objX);
            var valueY = ColumnInfo[ColumnToSort].ValueLookup(objY);

            compareResult = Comparer.Default.Compare(valueX, valueY);

            if (SortOrder == SortOrder.Ascending)
            {
                return compareResult;
            }
            else if (SortOrder == SortOrder.Descending)
            {
                return -compareResult;
            }
            else
            {
                return 0;
            }
        }

        public int ColumnToSort { get; set; } = 0;

        public SortOrder SortOrder { get; set; } = SortOrder.Ascending;

        public IList<(string ColumnName, Func<T, object> ValueLookup, Func<T, string> DisplayStringLookup)> ColumnInfo { get; }
    }
}
