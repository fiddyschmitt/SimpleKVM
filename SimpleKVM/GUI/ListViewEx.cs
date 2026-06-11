using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SimpleKVM.GUI
{
    public class ListViewEx<T> : ListView where T : class
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
                            Obj = lvi.Tag as T
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
                            .Select(lvi => lvi.Tag)
                            .OfType<T>()
                            .ToList();

            return result;
        }

        public List<T> GetItems()
        {
            var result = Items
                            .OfType<ListViewItem>()
                            .Select(lvi => lvi.Tag)
                            .OfType<T>()
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
                    Obj = lvi.Tag as T
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
                            if (lvi.Obj == null) return;

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

            if (columnsChanged.Count > 0)
            {
                AutoFitColumns();
            }
        }

        public void AutoFitColumns()
        {
            if (Columns.Count == 0) return;

            //Before the handle exists, the negative sentinel widths aren't computed by the control and
            //reading Width back just returns them, so the maths below would corrupt the layout.
            //OnHandleCreated runs the autofit once the control is real.
            if (!IsHandleCreated) return;

            BeginUpdate();
            try
            {
                //Size each column to the wider of its header and its content. Width = -2
                //(LVSCW_AUTOSIZE_USEHEADER) does exactly that - except on the last column, where it
                //instead fills the remaining control width as of that moment. That value goes stale
                //when the control is later resized, leaving a phantom horizontal scrollbar, so the
                //last column is sized manually and stretched to the edge instead.
                for (int i = 0; i < Columns.Count - 1; i++)
                {
                    Columns[i].Width = -2;
                }

                var lastColumn = Columns[^1];
                lastColumn.Width = -1;  //fit content
                lastColumnNaturalWidth = Math.Max(lastColumn.Width, MeasureHeaderWidth(lastColumn));

                StretchLastColumn();
            }
            finally
            {
                EndUpdate();
            }
        }

        int lastColumnNaturalWidth;

        int MeasureHeaderWidth(ColumnHeader column)
        {
            return TextRenderer.MeasureText(column.Text, Font).Width + 12;    //+12 approximates the header cell padding
        }

        void StretchLastColumn()
        {
            if (Columns.Count == 0) return;

            if (lastColumnNaturalWidth == 0)
            {
                lastColumnNaturalWidth = MeasureHeaderWidth(Columns[^1]);
            }

            var otherColumnsWidth = 0;
            for (int i = 0; i < Columns.Count - 1; i++)
            {
                otherColumnsWidth += Columns[i].Width;
            }

            //Fill the control exactly, so there is neither a gap nor a horizontal scrollbar.
            //If the control is too narrow, fall back to the column's natural width and let the scrollbar appear.
            Columns[^1].Width = Math.Max(lastColumnNaturalWidth, ClientSize.Width - otherColumnsWidth);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);    //realizes the columns and any items added before the handle existed

            AutoFitColumns();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);

            if (IsHandleCreated)
            {
                StretchLastColumn();
            }
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

    public class ListViewColumnSorter<T> : IComparer where T : class
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

            if (listviewX.Tag is not T objX || listviewY.Tag is not T objY) return 0;

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
