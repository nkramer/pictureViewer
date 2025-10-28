using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Folio.Book {
    public enum Aspect {
        // 3x2 aspect ratio in landscape mode
        Landscape3x2,

        // 3x2 aspect ratio in portrait mode
        Portrait2x3,

        // Unspecified aspect ratio
        // TODO: Why do we need this?
        None,
    }

    public enum RowOrColumn {
        Row,
        Column,
    }

    // Allows you to specify constraints of the form row A = column B,
    // row A = row B, column A = row B, or column A = column B.
    public class ExtraConstraint {
        public int RowColA;
        public int RowColB;
        public RowOrColumn RowOrColumnA;
        public RowOrColumn RowOrColumnB;
    }

    // A layout container that preserves the desired aspect ratio of the images.
    // Its a lot like a Grid, with the additional constraints around aspect ratio.
    // Size to content is not supported, the parent is expected to provide the size.
    // 
    // In addition to specifying the child's Row and Column, you also need to set  the AspectPreservingGrid.Aspect on the child.
    // For most layouts, you'll also need to provide ExtraConstraints to make different images in the layout matchup in size.
    public class AspectPreservingGrid : Grid {
        // only a Grid to get the Row/ColDefinitions

        public ExtraConstraint[] ExtraConstraints = null;
        private List<GridLength> rowDefs;
        private List<GridLength> colDefs;

        public static readonly GridLength MagicNumberCanBeNegative = new GridLength(98765, GridUnitType.Star);

        public AspectPreservingGrid() {
        }

        public static Aspect GetAspect(DependencyObject obj) {
            return (Aspect)obj.GetValue(AspectProperty);
        }

        public static void SetAspect(DependencyObject obj, Aspect value) {
            obj.SetValue(AspectProperty, value);
        }

        // Using a DependencyProperty as the backing store for Aspect.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AspectProperty =
            DependencyProperty.RegisterAttached("Aspect", typeof(Aspect), typeof(AspectPreservingGrid), new UIPropertyMetadata(Aspect.None));

        private List<double> BlankRow(int cols) {
            var res = new List<double>();
            for (int i = 0; i < cols; i++)
                res.Add(0);
            return res;
        }

        // w / h, or 0 if don't care
        private static double GetAspectRatio(UIElement elt) {
            var fe = (FrameworkElement)elt;
            switch (GetAspect(elt)) {
                case Aspect.Landscape3x2: return 4.0 / 3.0;
                case Aspect.Portrait2x3: return 3.0 / 4.0;
                //case Aspect.Landscape3x2: return 3.0 / 2.0;
                //case Aspect.Portrait2x3: return 2.0 / 3.0;
                case Aspect.None: return 0;
                default: throw new Exception();
            }
        }

        // The layout constraints to solve, in Ax=b form, where A is called "constraints".
        private class ConstraintData {
            public List<List<double>> constraints; // Coefficients of the polynomials. 
            public List<double> b; // The values of each polynomial. 
            public int numVars; // # of columns in constraints -- ie, constraints.All(c => c.Count == numVars)
            public ExtraSpace extraSpace; // If extra white space can be put at the top or the sides of the page to make the layout work. 
            public int firstRowVar;  // Index of the first "real" coefficient that represents rows, skipping over any extra coefficients added for extraSpace. 
            public int firstColVar;  // Index of the first coefficient that represents columns


        }

        // A layout solution. 
        private class GridSizes {
            public readonly double[] rowSizes;
            public readonly double[] colSizes;
            public readonly Point padding; // xywh, aka topleft xy and distance from bottomright

            public GridSizes(double[] rowSizes, double[] colSizes, Point padding) {
                this.rowSizes = rowSizes;
                this.colSizes = colSizes;
                this.padding = padding;
            }

            public void AssertEqual(GridSizes right) {
                if (this.rowSizes.Length != right.rowSizes.Length)
                    throw new Exception("mismatched # rows");
                if (this.colSizes.Length != right.colSizes.Length)
                    throw new Exception("mismatched # cols");
                this.rowSizes.Zip(right.rowSizes, (a, b) => {
                    if (MatrixSolver.CloseEnough(a, b))
                        throw new Exception(string.Format("mismatched row value, {0} vs {1}", a, b));
                    return 0;
                }).ToArray(); // ToArray just forces evaluation
                this.colSizes.Zip(right.colSizes, (a, b) => {
                    if (!MatrixSolver.CloseEnough(a, b))
                        throw new Exception(string.Format("mismatched row value, {0} vs {1}", a, b));
                    return 0;
                }).ToArray(); // ToArray just forces evaluation
                if (!MatrixSolver.CloseEnough(this.padding.X, right.padding.X))
                    throw new Exception(string.Format("mismatched padding.X, {0} vs {1}", this.padding.X, right.padding.X));
                if (!MatrixSolver.CloseEnough(this.padding.Y, right.padding.Y))
                    throw new Exception(string.Format("mismatched padding.Y, {0} vs {1}", this.padding.Y, right.padding.Y));
            }

            public void DebugPrint() {
                Debug.Write("          ");
                foreach (var col in colSizes) {
                    Debug.Write(string.Format("{0,10}", col));
                }
                Debug.WriteLine("");
                foreach (var row in rowSizes) {
                    Debug.WriteLine(string.Format("{0,10}", row));
                }
            }

            public static void DebugPrint(GridSizes sizes) {
                if (sizes == null)
                    Debug.WriteLine("null");
                else
                    sizes.DebugPrint();
            }
        }

        private double magicNumberSignifyingPadding = 87452;

        private enum ExtraSpace { None, Width, Height }

        // returns success (true) or failure. eltHeight is height of 1st elt w/ aspect ratio.
        private GridSizes CalcConstraints(double width, double height, ExtraSpace extraSpace) {
            int fakeRows = 0;
            int fakeCols = 0;
            if (extraSpace == ExtraSpace.Height) {
                // add extra rows to take up extra height
                this.rowDefs.Add(new GridLength(magicNumberSignifyingPadding, GridUnitType.Star));
                fakeRows++;
            } else if (extraSpace == ExtraSpace.Width) {
                this.colDefs.Add(new GridLength(magicNumberSignifyingPadding, GridUnitType.Star));
                fakeCols++;
            }

            var constraints = new List<List<double>>();
            var b = new List<double>(); // Ax=b
            // x is row0.height, row1.height, ..., rowM.height, col0.width, col1.width, ..., colM.width

            var constraintData = new ConstraintData() {
                constraints = constraints,
                b = b,
                numVars = this.rowDefs.Count + this.colDefs.Count,
                extraSpace = extraSpace,
                firstRowVar = 0 + fakeRows,
                firstColVar = rowDefs.Count + fakeCols,
            };

            AddHeightWidthConstraints(width, height, constraintData);
            AddAspectRatioConstraints(constraintData);
            AddFixedSizeConstraints(constraintData, this.rowDefs, 0);
            AddFixedSizeConstraints(constraintData, this.colDefs, rowDefs.Count);
            AddStarSizeConstraints(constraintData, this.rowDefs, 0);
            AddStarSizeConstraints(constraintData, this.colDefs, rowDefs.Count);

            if (ExtraConstraints != null) {
                foreach (var extra in ExtraConstraints) {
                    var a = BlankRow(constraintData.numVars);

                    if (extra.RowOrColumnA == RowOrColumn.Row)
                        a[constraintData.firstRowVar + extra.RowColA] = 1;
                    else
                        a[constraintData.firstColVar + extra.RowColA] = 1;

                    if (extra.RowOrColumnB == RowOrColumn.Row)
                        a[constraintData.firstRowVar + extra.RowColB] = -1;
                    else
                        a[constraintData.firstColVar + extra.RowColB] = -1;

                    constraintData.constraints.Add(a);
                    constraintData.b.Add(0);
                }
            }

            int numVars = this.rowDefs.Count + this.colDefs.Count;
            Debug.Assert(constraints.All(c => c.Count == numVars));
            double[][] A = constraints.Select(list => list.ToArray()).ToArray();
            double[] bPrime = b.ToArray();
            //Debug.WriteLine("Before");
            //MatrixSolver.DebugPrintMatrix(A, bPrime);
            double[] rowColSizes = MatrixSolver.SolveLinearEquations(A, bPrime);
            //Debug.WriteLine("After");
            //MatrixSolver.DebugPrintMatrix(A, bPrime);

            Point padding = new Point(0, 0);

            if (extraSpace == ExtraSpace.Height) {
                Debug.Assert(IsPagePadding(this.rowDefs[rowDefs.Count - 1]));
                padding.Y = bPrime[0];
                this.rowDefs.RemoveAt(rowDefs.Count - 1);
            } else if (extraSpace == ExtraSpace.Width) {
                Debug.Assert(IsPagePadding(this.colDefs[colDefs.Count - 1]));
                this.colDefs.RemoveAt(colDefs.Count - 1);
                padding.X = bPrime[rowDefs.Count];
            }

            //if (rowColSizes == null)
            //    Debug.WriteLine("unsolvable matrix");
            //else if (!rowColSizes.All(size => !double.IsNaN(size) && size >= 0) || padding.X < 0 || padding.Y < 0)
            //    Debug.WriteLine("requires Negative Sizes");

            bool exists = rowColSizes != null;
            bool unique = exists && rowColSizes.All(size => !double.IsNaN(size));

            // Check non-negativity for columns/rows that are NOT marked as +-
            bool nonNegative = unique
                && this.rowDefs.Select((def, i) => new { def, i })
                    .All(x => CanBeNegative(x.def) || rowColSizes[x.i] >= 0) 
                && this.colDefs.Select((def, i) => new { def, i })
                    .All(x => CanBeNegative(x.def) || rowColSizes[this.rowDefs.Count + x.i] >= 0);

            bool uniqueAndExists = exists && unique && nonNegative;
            Debug.WriteLine($"exists:{exists} unique:{unique} nonNegative:{nonNegative} all:{uniqueAndExists}");

            if (uniqueAndExists) {
                //var rowsizes = rowColSizes.Skip(fakeRows).Take(this.rowDefs.Count).ToArray();
                var rowsizes = rowColSizes.Take(this.rowDefs.Count).ToArray();
                var colsizes = rowColSizes.Skip(fakeRows).Skip(this.rowDefs.Count).Take(this.colDefs.Count).ToArray();
                Debug.Assert(this.rowDefs.Count == rowsizes.Count());
                Debug.Assert(this.colDefs.Count == colsizes.Count());
                var gridSizes = new GridSizes(rowsizes, colsizes, padding);
                return gridSizes;
            } else {
                return null; // no unique solution
            }
        }

        private bool IsPagePadding(GridLength rowColDef) {
            return rowColDef.Value == magicNumberSignifyingPadding;
        }

        private bool CanBeNegative(GridLength rowColDef) {
            return rowColDef.Equals(MagicNumberCanBeNegative);
        }

        private void AddHeightWidthConstraints(double width, double height, ConstraintData constraintData) {
            // overall height + width constraints
            {
                // row0.height + row1.height +  ... + rowM.height = this.height
                var a = this.rowDefs.Select(rd => (double)1).ToList();
                a = a.Concat(this.colDefs.Select(cd => (double)0)).ToList();
                constraintData.constraints.Add(a);
                constraintData.b.Add(height);
            }
            {
                // col0.width + col1.width +  ... + colM.width = this.width
                var a = this.colDefs.Select(cd => (double)1).ToList();
                a = this.rowDefs.Select(rd => (double)0).Concat(a).ToList();
                constraintData.constraints.Add(a);
                constraintData.b.Add(width);
                Debug.Assert(constraintData.constraints.Count == constraintData.b.Count);
            }
        }

        private void AddAspectRatioConstraints(ConstraintData constraintData) {
            // Add aspect ratio constraints
            foreach (UIElement child in this.Children) {
                // r1 + r2 + r3 = AspectRatio * (c1 + c2 + c3), or
                // r1 + r2 + r3 - AspectRatio * c1 - AspectRatio * c2 - AspectRatio * c3 = 0
                // for a 3rowspan/3colspan elt
                double aspect = GetAspectRatio(child);
                if (aspect != 0) {
                    aspect = 1.0 / aspect;
                    var a = BlankRow(constraintData.numVars);
                    for (int i = 0; i < Grid.GetRowSpan(child); i++)
                        a[i + Grid.GetRow(child) + 0] = 1;
                    for (int i = 0; i < Grid.GetColumnSpan(child); i++)
                        a[i + Grid.GetColumn(child) + this.rowDefs.Count] = -1 * aspect;
                    constraintData.constraints.Add(a);
                    constraintData.b.Add(0);
                }
            }
        }

        private void AddFixedSizeConstraints(ConstraintData constraintData, List<GridLength> rowColDefs, int firstVarIndex) {
            // Account for fixed (absolute) rows & columns, usually gutters or margins
            for (int rowColNum = 0; rowColNum < rowColDefs.Count; rowColNum++) {
                GridLength rowCol = rowColDefs[rowColNum];
                if (rowCol.IsAbsolute) {
                    // rowCol[n] = rowColheight 
                    var a = BlankRow(constraintData.numVars);
                    a[rowColNum + firstVarIndex] = 1;
                    constraintData.constraints.Add(a);
                    constraintData.b.Add(rowCol.Value);
                }
            }
        }

        private void AddStarSizeConstraints(ConstraintData constraintData,
            List<GridLength> rowColDefs, int firstVarIndex) {
            int firstRowVar = 0; // ignore difference between real & ExtraHeight rows/cols
            int firstColVar = this.rowDefs.Count;
            int numVars = this.rowDefs.Count + this.colDefs.Count;
            // set *-sized cols to minwidth

            switch (constraintData.extraSpace) {
                case ExtraSpace.Width:
                    SetStarDefsToMinLength(constraintData, this.rowDefs, firstRowVar, numVars);
                    SetStarDefsToSameSize(constraintData, this.colDefs, firstColVar, numVars);
                    //SetPaddingDefsToZero(constraintData, this.rowDefs, firstRowVar, numVars);
                    break;
                case ExtraSpace.Height:
                    SetStarDefsToMinLength(constraintData, this.colDefs, firstColVar, numVars);
                    SetStarDefsToSameSize(constraintData, this.rowDefs, firstRowVar, numVars);
                    //SetPaddingDefsToZero(constraintData, this.colDefs, firstColVar, numVars);
                    break;
                case ExtraSpace.None:
                    //SetStarDefsToMinLength(constraintData, this.rowDefs, firstRowVar, numVars);
                    //SetStarDefsToMinLength(constraintData, this.colDefs, firstColVar, numVars);
                    break;
            }

        }

        private void SetStarDefsToMinLength(ConstraintData constraintData, List<GridLength> rowColDefs, int firstRowColIndex, int numVars) {
            for (int defNum = 0; defNum < rowColDefs.Count; defNum++) {
                GridLength def = rowColDefs[defNum];
                if (def.IsStar && !CanBeNegative(def)) {
                    // col[n] = minwidth
                    var a = BlankRow(numVars);
                    a[defNum + firstRowColIndex] = 1;
                    constraintData.constraints.Add(a);
                    constraintData.b.Add(0);
                }
            }
        }

        private void SetStarDefsToSameSize(ConstraintData constraintData, List<GridLength> rowColDefs, int firstRowColIndex, int numVars) {
            // set all *-sized rows to same height
            var starDefsAndIndexes = rowColDefs
                .Select((length, index) => new { GridLength = length, Index = index })
                .Where(pair => pair.GridLength.IsStar && !IsPagePadding(pair.GridLength) && !CanBeNegative(pair.GridLength));
            if (starDefsAndIndexes.Count() > 1) {
                var def1 = starDefsAndIndexes.First();
                foreach (var def2 in starDefsAndIndexes.Skip(1)) {
                    // row[n1] - row[n2] = 0
                    var a = BlankRow(numVars);
                    a[def1.Index + firstRowColIndex] = 1;
                    a[def2.Index + firstRowColIndex] = -1;
                    constraintData.constraints.Add(a);
                    constraintData.b.Add(0);
                }
            }
        }

        private void SetPaddingDefsToZero(ConstraintData constraintData, List<GridLength> rowColDefs, int firstRowColIndex, int numVars) {
            Debug.Assert(IsPagePadding(rowColDefs.First()));
            //Debug.Assert(IsPagePadding(rowColDefs.Last()));
            var a = BlankRow(numVars);
            a[0 + firstRowColIndex] = 1;
            //a[rowColDefs.Count - 1 + firstRowColIndex] = -1;
            constraintData.constraints.Add(a);
            constraintData.b.Add(0);
        }

        //private void AddMinCaptionSize(bool isWidthConstrained, List<List<double>> constraints, List<double> b) {
        //    // Add aspect ratio constraints
        //    int firstRowVar = 0;
        //    int firstColVar = this.rowDefs.Count;
        //    int numVars = this.rowDefs.Count + this.colDefs.Count;
        //    foreach (UIElement child in this.Children) {
        //        if (child is CaptionView) {
        //            double min = 100;
        //            // r1 + r2 + r3 = min, or
        //            // c1 + c2 + c3 = min
        //            var a = BlankRow(numVars);
        //            if (isWidthConstrained) {
        //                for (int i = 0; i < Grid.GetColumnSpan(child); i++)
        //                    a[i + Grid.GetColumn(child) + firstColVar] = 1;
        //            } else {
        //                for (int i = 0; i < Grid.GetRowSpan(child); i++)
        //                    a[i + Grid.GetRow(child) + firstRowVar] = 1;
        //            }
        //            constraints.Add(a);
        //            b.Add(min);
        //        }
        //    }
        //}

        //private Size lastMeasureSize = new Size(0, 0);
        //private double[] lastMeasureResults = null;

        protected override Size MeasureOverride(Size constraint) {
            GridSizes sizes = ComputeSizes(constraint);
            IterateOverChildren(sizes, LayoutPass.Measure);
            return constraint;
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            GridSizes sizes = ComputeSizes(arrangeSize);
            IterateOverChildren(sizes, LayoutPass.Arrange);
            return arrangeSize;
        }

        private enum LayoutPass {
            Measure,
            Arrange,
        }

        private void IterateOverChildren(GridSizes sizes, LayoutPass layoutPass) {
            foreach (UIElement child in Children) {
                int row = Grid.GetRow(child);
                int col = Grid.GetColumn(child);
                int rowspan = Grid.GetRowSpan(child);
                int colspan = Grid.GetColumnSpan(child);

                double x = 0;
                for (int i = 0; i < col; i++) {
                    x += sizes.colSizes[i];
                }

                double y = 0;
                for (int i = 0; i < row; i++) {
                    y += sizes.rowSizes[i];
                }

                double width = 0;
                for (int i = col; i < col + colspan; i++) {
                    width += sizes.colSizes[i];
                }

                double height = 0;
                for (int i = row; i < row + rowspan; i++) {
                    height += sizes.rowSizes[i];
                }

                switch (layoutPass) {
                    case LayoutPass.Measure: child.Measure(new Size(width, height)); break;
                    case LayoutPass.Arrange: child.Arrange(new Rect(x, y, width, height)); break;
                }
            }
        }

        private GridSizes ComputeSizes(Size arrangeSize) {
            InitializeRowAndColumnDefs();

            int numRows = rowDefs.Count;
            int numCols = colDefs.Count;

            //Debug.WriteLine(this.Tag);
            //DebugPrintLayoutAttempted();
            //DebugPrintTemplateShortString();

            Debug.WriteLine("natural:");
            GridSizes sizes0 = CalcConstraints(arrangeSize.Width, arrangeSize.Height, ExtraSpace.None);
            //Debug.Assert(numRows == rowDefs.Count && numCols == colDefs.Count, "'temporary' row/col wasn't so temporary");
            //GridSizes.DebugPrint(sizes0);
            if (sizes0 != null)
                return sizes0;

            // width constrained
            Debug.WriteLine("extra width:");
            GridSizes sizes1 = CalcConstraints(arrangeSize.Width, arrangeSize.Height, ExtraSpace.Width);
            //Debug.Assert(numRows == rowDefs.Count && numCols == colDefs.Count, "'temporary' row/col wasn't so temporary");
            //GridSizes.DebugPrint(sizes1);

            // height constrained
            Debug.WriteLine("extra height:");
            GridSizes sizes2 = CalcConstraints(arrangeSize.Width, arrangeSize.Height, ExtraSpace.Height);
            //Debug.Assert(numRows == rowDefs.Count && numCols == colDefs.Count, "'temporary' row/col wasn't so temporary");
            //GridSizes.DebugPrint(sizes2);

            //if (!success1 && !success2) {
            //    // try adding minsizes to captions
            //    success1 = CalcConstraints(arrangeSize.Width, arrangeSize.Height, true, true, out eltHeight1, out sizes1);
            //    success2 = CalcConstraints(arrangeSize.Width, arrangeSize.Height, false, true, out eltHeight2, out sizes2);
            //}

            if (sizes1 == null && sizes2 == null) {
                Debug.WriteLine(this.Tag);
                throw new Exception("Can't solve layout");
            }

            // TODO: leftover space 
            bool useFirst = false;
            if (sizes1 == null) {
                useFirst = false;
            } else if (sizes2 == null) {
                useFirst = true;
            } else if (sizes1.padding.Y > sizes2.padding.Y) {
                useFirst = true;
            } else {
                useFirst = false;
            }

            GridSizes sizes = (useFirst) ? sizes1 : sizes2;
            rowDefs = null;
            colDefs = null;
            return sizes;
        }

        public void InitializeRowAndColumnDefs() {
            this.rowDefs = new List<GridLength>();
            foreach (RowDefinition r in this.RowDefinitions) {
                this.rowDefs.Add(r.Height);
            }
            this.colDefs = new List<GridLength>();
            foreach (ColumnDefinition c in this.ColumnDefinitions) {
                this.colDefs.Add(c.Width);
            }
        }

        public override string ToString() {
            return "{APG " + this.Tag + " " + GetHashCode() + "}";
        }

        public string LayoutToString() {
            StringBuilder builder = new StringBuilder();
            //builder.Append("\n");
            int[][] cell = new int[rowDefs.Count][];
            for (int i = 0; i < rowDefs.Count; i++) {
                cell[i] = new int[colDefs.Count];
                for (int j = 0; j < colDefs.Count; j++)
                    cell[i][j] = -1;
            }

            for (int childnum = 0; childnum < this.Children.Count; childnum++) {
                //            foreach (UIElement child in this.Children) {
                var child = this.Children[childnum];
                for (int i = 0; i < Grid.GetRowSpan(child); i++)
                    for (int j = 0; j < Grid.GetColumnSpan(child); j++) {
                        Debug.Assert(cell[i + Grid.GetRow(child)][j + Grid.GetColumn(child)] == -1, "overlapping elements -- legal but weird");
                        cell[i + Grid.GetRow(child)][j + Grid.GetColumn(child)] = childnum;
                    }
            }

            Debug.Write(string.Format("{0,7}", ""));
            //Debug.Write("       ");
            foreach (var col in colDefs) {
                string colString = RowColToString(col);
                builder.Append(string.Format("{0,7}", colString));
            }
            builder.Append("\n");
            for (int i = 0; i < rowDefs.Count; i++) {
                var row = this.rowDefs[i];
                //foreach (var row in rowDefs) {
                string rowString = RowColToString(row);
                builder.Append(string.Format("{0,7}", rowString));
                var rowCells = cell[i].Select(c =>
                    //string.Format("{0,7}", c == -1 ? "-" : c.ToString()))
                    string.Format("{0,7}", CellString(c)))
                    .Aggregate((s1, s2) => s1 + s2);
                builder.Append(rowCells);
                builder.Append("\n");
            }
            builder.Append("\n");
            return builder.ToString();
        }

        public void DebugPrintLayoutAttempted() {
            Debug.Write(LayoutToString());
        }

        private string CellString(int childIndex) {
            if (childIndex == -1)
                return "-";

            UIElement child = this.Children[childIndex];
            string s = null;
            if (child is CaptionView) {
                s = "C";
            } else if (child is DroppableImageDisplay) {
                if (GetAspectRatio(child) < 1)
                    s = "P";
                else
                    s = "L";
            } else if (child is Border) {
                s = "border";
            } else {
                s = "?";
            }

            return s + childIndex.ToString();
        }

        private void DebugPrintTemplateShortString() {
            //Debug.Write("********* ");
            foreach (var row in rowDefs) {
                string rowString = RowColToString(row);
                Debug.Write(rowString);
            }
            Debug.Write(" ");

            foreach (var col in colDefs) {
                string colString = RowColToString(col);
                Debug.Write(colString);
            }

            foreach (UIElement child in this.Children) {
                Debug.Write(" ");
                if (child is CaptionView) {
                    Debug.Write("c");
                } else if (child is DroppableImageDisplay) {
                    Debug.Write("i");
                    if ((child as FrameworkElement).Height > (child as FrameworkElement).Width)
                        Debug.Write("P");
                    else
                        Debug.Write("L");
                } else {
                    Debug.Fail("");
                }

                Debug.Write(Grid.GetRow(child));
                Debug.Write(Grid.GetRowSpan(child));
                Debug.Write(Grid.GetColumn(child));
                Debug.Write(Grid.GetColumnSpan(child));
            }
            //Debug.Write("********* ");
            Debug.WriteLine(" ");
        }

        public static void DebugPrintTemplateShortString(Grid parent) {
            //Debug.Write("********* ");
            foreach (var row in parent.RowDefinitions) {
                string rowString = RowColToString(row.Height);
                Debug.Write(rowString);
            }
            Debug.Write(" ");

            foreach (var col in parent.ColumnDefinitions) {
                string colString = RowColToString(col.Width);
                Debug.Write(colString);
            }

            foreach (UIElement child in parent.Children) {
                Debug.Write(" ");
                if (child is CaptionView) {
                    Debug.Write("c");
                } else if (child is DroppableImageDisplay) {
                    Debug.Write("i");
                    Debug.Assert(GetAspectRatio(child) > 0); // signals prop not set
                    if (GetAspectRatio(child) < 1)
                        Debug.Write("P");
                    else
                        Debug.Write("L");
                } else if (child is Border) {
                    Debug.Write("border");
                } else {
                    Debug.Fail("");
                }

                Debug.Write(Grid.GetRow(child));
                Debug.Write(Grid.GetRowSpan(child));
                Debug.Write(Grid.GetColumn(child));
                Debug.Write(Grid.GetColumnSpan(child));
            }
            //Debug.Write("********* ");
            //Debug.WriteLine(" ");
        }

        private static string RowColToString(GridLength rowCol) {
            string colString;
            switch (rowCol.GridUnitType) {
                case GridUnitType.Star:
                    if (Math.Abs(rowCol.Value - MagicNumberCanBeNegative.Value) < 0.1) {
                        colString = "+-";
                    } else {
                        colString = "*";
                    }
                    break;
                case GridUnitType.Auto: colString = "a"; break;
                case GridUnitType.Pixel:
                    if (rowCol.Value == 50)
                        colString = "m";
                    else if (rowCol.Value == 20)
                        colString = "g";
                    else
                        colString = string.Format("({0:f0})", rowCol.Value); // (234) -- no decimal
                    break;
                default:
                    Debug.Fail("");
                    colString = "";
                    break;
            }
            return colString;
        }

    }
}
