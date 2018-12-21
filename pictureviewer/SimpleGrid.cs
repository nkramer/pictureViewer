using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace pictureviewer {
    // Similar to a UniformGrid -- all cells are the same size.
    // A UniformGrid has a fixed number of rows and columns and variable cell size,
    // a simpleGrid has fixed cell size and variable number of rows and columns
    public class SimpleGrid : Panel{
        public SimpleGrid() {
        }

        internal PhotoGrid photogrid; // messy interface

        protected override Size MeasureOverride(Size availableSize) {
            if (Children.Count == 0) return new Size(0, 0);
            for (int i = 0, count = Children.Count; i < count; ++i) {
                UIElement child = Children[i];
                child.Measure(availableSize);
            }
            Size cellSize = Children[0].DesiredSize; // assume all children return the same number

            if (double.IsPositiveInfinity(availableSize.Height)) {
                int columns = (int)(availableSize.Width / cellSize.Width);
                //if (double.IsPositiveInfinity(availableSize.Width)) columns = Children.Count;
                int rows = (int) Math.Ceiling(1.0 * Children.Count / columns);
                return new Size(availableSize.Width, rows * Children[0].DesiredSize.Height);
            } else {
                return availableSize;
            }
        }

        public int numberVisible;

        protected override Size ArrangeOverride(Size finalSize) {
            if (this.Children.Count == 0) return finalSize;
            Size cellSize = Children[0].DesiredSize; // assume all children return the same number
            this.columns = (int)(finalSize.Width / cellSize.Width);
            if (columns == 0) columns = 1;
            this.rows = (int)(finalSize.Height / cellSize.Height);

            for (int i = 0, count = Children.Count; i < count; ++i) {
                UIElement child = Children[i];
                int row = RowOf(i);
                int column = ColumnOf(i);
                var rectangle = new Rect(column * cellSize.Width, row * cellSize.Height, cellSize.Width, cellSize.Height);
                child.Arrange(rectangle);
            }

            this.numberVisible = columns * rows;
            //photogrid.OnGotDimensions(finalSize, columns * rows);


            return new Size(cellSize.Width * columns, cellSize.Height * rows);
            //return finalSize;
        }

        //public UIElement HitTest(Point point) {
        //    Size cellSize = Children[0].DesiredSize; // assume all children return the same number
        //    int row = (int)(point.Y / cellSize.Height);
        //    int column = (int)(point.X / cellSize.Width);
        //    int index = row * Rows + column;
        //    if (index < Children.Count)
        //        return Children[index];
        //    else
        //        return null;
        //}

        private int rows = 0;

        public int Rows {
            get { return rows; }
        }
        private int columns = 0;

        public int Columns {
            get { return columns; }
        }

        public int RowOf(int i) {
            int row = i / columns;
            return row;
        }

        public int ColumnOf(int i) {
            int column = i % columns;
            return column;
        }
    }
}
