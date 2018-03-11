namespace Tequilla.Responsive
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Markup;

    [ContentProperty( nameof(Children) )]
    public class VerticalScrollPanel : ScrollViewer
    {
        private static readonly DependencyPropertyKey ChildrenProperty = DependencyProperty.RegisterReadOnly(
            nameof(Children),
            typeof( UIElementCollection ),
            typeof( VerticalScrollPanel ),
            new PropertyMetadata() );

        public VerticalScrollPanel()
        {
            Panel = new ResponsivePanel();
            Panel.VerticalAlignment = VerticalAlignment.Top;
            Children = Panel.Children;
            Panel.Margin = new Thickness( 0 );

            VerticalAlignment = VerticalAlignment.Stretch;
            Content = Panel;
        }

        [DesignerSerializationVisibility( DesignerSerializationVisibility.Content )]
        public UIElementCollection Children
        {
            get
            {
                return (UIElementCollection)GetValue( ChildrenProperty.DependencyProperty );
            }
            private set
            {
                SetValue( ChildrenProperty, value );
            }
        }

        internal ResponsivePanel Panel { get; }

        protected override Size MeasureOverride( Size availableSize )
        {
            Panel.VisibleBound = availableSize;

            // Invalidate the panel measurements, as otherwise vertical scrolling may not 
            Panel.InvalidateMeasure();
            return base.MeasureOverride( availableSize );
        }
    }

    internal class Layout
    {
        public Layout( Size visibleSize )
        {
            VisibleSize = visibleSize;
        }

        public List<Column> Columns { get; } = new List<Column>();

        public string Debug
        {
            get
            {
                StringBuilder s = new StringBuilder();
                for ( var index = 0; index < Columns.Count; index++ )
                {
                    var column = Columns[index];
                    s.AppendLine( $"== Column {index} ==" );
                    foreach ( var element in column.Elements )
                    {
                        s.AppendLine(
                            $"* {( element as FrameworkElement ).Name} - {element.DesiredSize.Width} x {element.DesiredSize.Height}" );
                    }
                }

                return s.ToString();
            }
        }

        /// <summary>
        /// Gets the amount of visible space in which we will attempt to layout the controls before scrolling occurs.
        /// </summary>
        public Size VisibleSize { get; }

        public void AddElements( UIElementCollection children )
        {
            foreach ( UIElement child in children )
            {
                var column = AssignToColumn( child );
                column.Elements.Add( child );
            }
        }

        public void CalculateColumnSizes()
        {
            var totalDesiredWidth = Columns.Sum( col => col.DesiredWidth );
            var totalSpareSpace = VisibleSize.Width - totalDesiredWidth;
            var spareSpacePerColumn = totalSpareSpace / Columns.Count;

            foreach ( var column in Columns )
            {
                column.CalculatedWidth = column.DesiredWidth + spareSpacePerColumn;
            }
        }

        public Size GetSize()
        {
            var width = Columns.Sum( column => column.CalculatedWidth );
            var height = Columns.Max( column => column.Height );

            return new Size( width, height );
        }

        private Column AssignToColumn( UIElement child )
        {
            foreach ( var column in Columns )
            {
                if ( column.CanAccomodate( child ) )
                {
                    return column;
                }
            }

            // Create a new Column:
            var width = Columns.Sum( column => column.DesiredWidth );
            if ( !Columns.Any() || width + child.DesiredSize.Width < VisibleSize.Width )
            {
                var column = new Column( VisibleSize.Height );
                Columns.Add( column );
                return column;
            }

            return Columns.OrderBy( col => col.Height ).First();
        }
    }

    internal class Column
    {
        public Column( double visibleHeight )
        {
            VisibleHeight = visibleHeight;
        }

        public double CalculatedWidth { get; set; }

        public double DesiredWidth => Elements.Max( e => (double?)e.DesiredSize.Width ) ?? 0;

        public List<UIElement> Elements { get; } = new List<UIElement>();

        public double Height => Elements.Sum( e => (double?)e.DesiredSize.Height ) ?? 0;

        public bool OverFlows => Height > VisibleHeight;

        public double VisibleHeight { get; }

        public bool CanAccomodate( UIElement el )
        {
            return Height + el.DesiredSize.Height <= VisibleHeight;
        }
    }

    internal class ResponsivePanel : Panel
    {
        public Size VisibleBound { get; set; }

        protected override Size ArrangeOverride( Size finalSize )
        {
            // Restrict height to available visible space.
            finalSize.Height = Math.Min( VisibleBound.Height, finalSize.Height );

            var layout = new Layout( finalSize );
            layout.AddElements( InternalChildren );
            layout.CalculateColumnSizes();

            double x = 0;
            double y = 0;

            foreach ( var column in layout.Columns )
            {
                foreach ( UIElement element in column.Elements )
                {
                    var elementSize = element.DesiredSize;
                    elementSize.Width = column.CalculatedWidth;

                    element.Arrange( new Rect( new Point( x, y ), elementSize ) );
                    y = y + element.DesiredSize.Height;
                }
                x = x + column.CalculatedWidth;
                y = 0;
            }

            var size = layout.GetSize();
            return size;
        }

        /// <summary>
        /// Measures the size in layout required for child elements and determines a size.
        /// </summary>
        /// <returns>
        /// The size that this element determines it needs during layout, based on its calculations of child element
        /// sizes.
        /// </returns>
        /// <param name="availableSize">
        /// The available size that this element can give to child elements.
        /// Infinity can be specified as a value to indicate that the element will size to whatever content is available.
        /// </param>
        protected override Size MeasureOverride( Size availableSize )
        {
            // Restrict height to available visible space.
            availableSize.Height = Math.Min( VisibleBound.Height, availableSize.Height );

            // Measure the desired size for all child elements.
            foreach ( UIElement child in InternalChildren )
            {
                child.Measure( availableSize );
            }

            var layout = new Layout( availableSize );

            layout.AddElements( InternalChildren );
            layout.CalculateColumnSizes();

            var size = layout.GetSize();

            return size;
            
        }
    }
}