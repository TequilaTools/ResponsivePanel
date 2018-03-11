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

    /// <summary>
    /// Main control - acts as a scrollviewer which hosts the actual <see cref="ResponsivePanel"/>.
    /// Needed so we can track visible area within the scroller and adapt the display based on it.
    /// </summary>
    [ContentProperty( nameof(Children) )]
    public class VerticalScrollPanel : ScrollViewer
    {
        /// <summary>
        /// Dependency property to allow multiple child content to be housed in this element.
        /// </summary>
        private static readonly DependencyPropertyKey ChildrenProperty = DependencyProperty.RegisterReadOnly(
            nameof(Children),
            typeof( UIElementCollection ),
            typeof( VerticalScrollPanel ),
            new PropertyMetadata() );

        /// <summary>
        /// Initialises a new instance of the <see cref="VerticalScrollPanel"/> class.
        /// </summary>
        public VerticalScrollPanel()
        {
            // Create a responsive panel as a single child of this control.
            Panel = new ResponsivePanel();
            Panel.VerticalAlignment = VerticalAlignment.Top;
            Content = Panel;

            // Initialise the children of this control to the same container used by the Panel. This causes
            // nested elements of this control to be placed directly in the ResponsivePanel container instead.
            Children = Panel.Children;
            Panel.Margin = new Thickness( 0 );

            // Ensure that the ResponsivePanel fills the full space of the ScrollViewer.
            VerticalAlignment = VerticalAlignment.Stretch;
            
        }

        /// <summary>
        /// Gets the child elements which will be arranged by the panel. 
        /// </summary>
        /// <remarks>
        /// Dependency property to allow multiple child content to be housed in this element. <see cref="ChildrenProperty"/>
        /// </remarks>
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

        /// <summary>
        /// Gets a reference to the underlying <see cref="ResponsivePanel"/> used to layout elements.
        /// </summary>
        internal ResponsivePanel Panel { get; }

        /// <summary>
        /// Measures the size in layout required for child elements and determines a size.
        /// </summary>
        /// <returns>
        /// The size that this element determines it needs during layout, based on its calculations of child element sizes.
        /// </returns>
        /// <param name="availableSize">
        /// The available size that this element can give to child elements.
        /// Infinity can be specified as a value to indicate that the element will size to whatever content is available.
        /// </param>
        protected override Size MeasureOverride( Size availableSize )
        {
            // Store the available visible space, for use in the Panels own MeasureOverride and layout logic.
            Panel.VisibleBound = availableSize;

            // Invalidate the panel measurements, as otherwise vertical scrolling may not cause a re-layout.
            Panel.InvalidateMeasure();
            return base.MeasureOverride( availableSize );
        }
    }

    /// <summary>
    /// Contains the main logic for laying out content (sahred between measure and arrange passes).
    /// </summary>
    internal class LayoutManager
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="LayoutManager"/> class.
        /// </summary>
        /// <param name="visibleSize">
        /// The visible space allocated to the layout, if contents exceed this then scrolling is required,
        /// this is used to check if columns can house new content without scrolling and to adjust column widths to visible space.
        /// </param>
        public LayoutManager( Size visibleSize )
        {
            VisibleSize = visibleSize;
        }

        /// <summary>
        /// Gets a collection of the columns which are available in the layout. These are created automatically
        /// when <see cref="AddElements"/> is called.
        /// </summary>
        public List<Column> Columns { get; } = new List<Column>();

        /// <summary>
        /// Gets current layout state as a human readable string, useful for debugging purposes.
        /// </summary>
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

        /// <summary>
        /// Adds UI elements to the layout manager and causes those elements to be assigned to a particular column.
        /// </summary>
        /// <param name="children"></param>
        public void AddElements( UIElementCollection children )
        {
            foreach ( UIElement child in children )
            {
                var column = AssignToColumn( child );
                column.Elements.Add( child );
            }
        }

        /// <summary>
        /// Calculates the widths of columns in the layout, content should first be added with <see cref="AddElements"/>.
        /// </summary>
        public void CalculateColumnSizes()
        {
            // Current implementation stretches columns uniformly to fill the remaining width which is unused.
            var totalDesiredWidth = Columns.Sum( col => col.DesiredWidth );
            var totalSpareSpace = VisibleSize.Width - totalDesiredWidth;
            var spareSpacePerColumn = totalSpareSpace / Columns.Count;

            foreach ( var column in Columns )
            {
                column.CalculatedWidth = column.DesiredWidth + spareSpacePerColumn;
            }
        }

        /// <summary>
        /// Gets the total size of the layout, this includes content which may be position outside of the
        /// visible area of the control.
        /// </summary>
        /// <returns>
        /// Height and Width required by the layout.
        /// </returns>
        public Size GetSize()
        {
            var width = Columns.Sum( column => column.CalculatedWidth );
            var height = Columns.Max( column => column.DesiredHeight );

            return new Size( width, height );
        }

        /// <summary>
        /// Assigns a UI element to a column.
        /// </summary>
        /// <param name="element">
        /// The control to assign.
        /// </param>
        /// <returns>
        /// A reference to the column in which the element was assigned.
        /// </returns>
        private Column AssignToColumn( UIElement element )
        {
            // First determine if any existing columns can house the element in their empty space.
            foreach ( var column in Columns )
            {
                if ( column.CanAccomodate( element ) )
                {
                    return column;
                }
            }

            // If no existing column is suitable, attempt to create a new column, if we have enough horizontal space to do so.
            var width = Columns.Sum( column => column.DesiredWidth );
            if ( !Columns.Any() || width + element.DesiredSize.Width < VisibleSize.Width )
            {
                var column = new Column( VisibleSize.Height );
                Columns.Add( column );
                return column;
            }

            // No column accomodated the content and there was no room for additional columns
            // In this case we assign the content to the shortest column and allow scrolling.
            return Columns.OrderBy( col => col.DesiredHeight ).First();
        }
    }

    /// <summary>
    /// Stores layout state of a single column within the panel.
    /// </summary>
    internal class Column
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="Column"/> class.
        /// </summary>
        /// <param name="visibleHeight">
        /// The visible height allocated to the column, if contents exceed this then scrolling is required,
        /// this is used to check if the column can house new content without scrolling.
        /// </param>
        public Column( double visibleHeight )
        {
            VisibleHeight = visibleHeight;
        }

        public double CalculatedWidth { get; set; }

        /// <summary>
        /// Gets the desired width of the column, measures the content housed within the column.
        /// </summary>
        public double DesiredWidth => Elements.Max( e => (double?)e.DesiredSize.Width ) ?? 0;

        /// <summary>
        /// Gets the desired height of the column, measures the content housed within the column.
        /// </summary>
        public double DesiredHeight => Elements.Sum(e => (double?)e.DesiredSize.Height) ?? 0;

        /// <summary>
        /// Gets a collection of the UI elements which are assigned to this column.
        /// </summary>
        public List<UIElement> Elements { get; } = new List<UIElement>();

        /// <summary>
        /// Gets the visible height allocated to the column, if contents exceed this then scrolling is required,
        /// this is used to check if the column can house new content without scrolling.
        /// </summary>
        public double VisibleHeight { get; }

        /// <summary>
        /// Determines if the specified element can be housed within this column without causing a need for scrolling.
        /// </summary>
        /// <param name="element">
        /// The element to check, this element should alreadh have been measured.
        /// </param>
        /// <returns>
        /// true if the element can be added to this column within the remaining visible space; false otherwise.
        /// </returns>
        public bool CanAccomodate( UIElement element )
        {
            return DesiredHeight + element.DesiredSize.Height <= VisibleHeight;
        }
    }

    /// <summary>
    /// The main panel used to layout columns. Contains the main implementation of panel.
    /// </summary>
    internal class ResponsivePanel : Panel
    {
        /// <summary>
        /// Gets or sets the visible area in which the Panel should respond (adjust its content to try and fit the space
        /// without needing to scroll).
        /// </summary>
        public Size VisibleBound { get; set; }

        /// <summary>
        /// Positions child elements and determines a size for the <see cref="ResponsivePanel"/> class.
        /// </summary>
        /// <param name="finalSize">
        /// The final area within the parent that this element should use to arrange itself and its children.
        /// </param>
        /// <returns>
        /// The actual size used.
        /// </returns>
        protected override Size ArrangeOverride( Size finalSize )
        {
            // Restrict height to available visible space.
            finalSize.Height = Math.Min( VisibleBound.Height, finalSize.Height );

            // Arrange content in to columns using the LayoutManager
            var layout = new LayoutManager( finalSize );
            layout.AddElements( InternalChildren );
            layout.CalculateColumnSizes();

            // Loop through each column and element and position the elements at the appropriate
            // x and y co-ordinates for the arrangement
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

            var layout = new LayoutManager( availableSize );

            layout.AddElements( InternalChildren );
            layout.CalculateColumnSizes();

            var size = layout.GetSize();

            return size;
            
        }
    }
}