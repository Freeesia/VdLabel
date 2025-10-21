using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VdLabel;

/// <summary>
/// Adorner that shows the dragged item following the mouse cursor
/// </summary>
internal class DragAdorner : Adorner
{
    private readonly ContentPresenter contentPresenter;
    private readonly AdornerLayer adornerLayer;
    private Point position;

    public DragAdorner(UIElement adornedElement, UIElement draggedElement, AdornerLayer adornerLayer)
        : base(adornedElement)
    {
        this.adornerLayer = adornerLayer;
        this.contentPresenter = new ContentPresenter
        {
            Content = draggedElement,
            Opacity = 0.5
        };
    }

    public void UpdatePosition(Point position)
    {
        this.position = position;
        this.adornerLayer.Update(this.AdornedElement);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        this.contentPresenter.Measure(constraint);
        return this.contentPresenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        this.contentPresenter.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override Visual GetVisualChild(int index)
    {
        return this.contentPresenter;
    }

    protected override int VisualChildrenCount => 1;

    public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
    {
        var result = new GeneralTransformGroup();
        result.Children.Add(base.GetDesiredTransform(transform));
        result.Children.Add(new TranslateTransform(this.position.X, this.position.Y));
        return result;
    }
}

/// <summary>
/// Adorner that shows the insertion position indicator
/// </summary>
internal class InsertionAdorner : Adorner
{
    private readonly Pen pen;
    private readonly bool isAbove;

    public InsertionAdorner(UIElement adornedElement, bool isAbove)
        : base(adornedElement)
    {
        this.isAbove = isAbove;
        this.pen = new Pen(Brushes.DodgerBlue, 2);
        this.IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(this.AdornedElement.RenderSize);
        var y = this.isAbove ? rect.Top : rect.Bottom;
        var point1 = new Point(rect.Left, y);
        var point2 = new Point(rect.Right, y);

        drawingContext.DrawLine(this.pen, point1, point2);
    }
}
