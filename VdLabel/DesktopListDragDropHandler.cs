using GongSolutions.Wpf.DragDrop;
using System.Windows;

namespace VdLabel;

/// <summary>
/// Drag-drop handler for desktop list reordering
/// </summary>
internal class DesktopListDragDropHandler : IDropTarget, IDragSource
{
    private readonly IVirualDesktopService virualDesktopService;

    public DesktopListDragDropHandler(IVirualDesktopService virualDesktopService)
    {
        this.virualDesktopService = virualDesktopService;
    }

    public void DragOver(IDropInfo dropInfo)
    {
        if (!this.virualDesktopService.IsSupportedMoveDesktop)
        {
            return;
        }

        if (dropInfo.Data is DesktopConfigViewModel sourceItem &&
            dropInfo.TargetItem is DesktopConfigViewModel targetItem)
        {
            // Don't allow dropping on or above "All Desktops" (first item with Guid.Empty)
            if (targetItem.Id == Guid.Empty)
            {
                return;
            }

            // Don't allow dragging the "All Desktops" item
            if (sourceItem.Id == Guid.Empty)
            {
                return;
            }

            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            dropInfo.Effects = DragDropEffects.Move;
        }
    }

    public void Drop(IDropInfo dropInfo)
    {
        if (!this.virualDesktopService.IsSupportedMoveDesktop)
        {
            return;
        }

        if (dropInfo.Data is DesktopConfigViewModel sourceItem &&
            dropInfo.TargetItem is DesktopConfigViewModel targetItem &&
            sourceItem.Id != targetItem.Id &&
            sourceItem.Id != Guid.Empty &&
            targetItem.Id != Guid.Empty)
        {
            // Calculate the target index in the list
            var targetIndex = dropInfo.InsertIndex;

            // Adjust for "All Desktops" being the first item (index 0)
            // The actual desktop index should be -1 from the list index
            if (targetIndex > 0)
            {
                targetIndex -= 1;
            }

            // Move the desktop
            this.virualDesktopService.MoveDesktop(sourceItem.Id, targetIndex);
        }
    }

    public void StartDrag(IDragInfo dragInfo)
    {
        if (!this.virualDesktopService.IsSupportedMoveDesktop)
        {
            return;
        }

        if (dragInfo.SourceItem is DesktopConfigViewModel item && item.Id != Guid.Empty)
        {
            dragInfo.Data = item;
            dragInfo.Effects = DragDropEffects.Move;
        }
    }

    public bool CanStartDrag(IDragInfo dragInfo)
    {
        if (!this.virualDesktopService.IsSupportedMoveDesktop)
        {
            return false;
        }

        // Don't allow dragging the "All Desktops" item
        return dragInfo.SourceItem is DesktopConfigViewModel item && item.Id != Guid.Empty;
    }

    public void Dropped(IDropInfo dropInfo)
    {
        // No additional action needed after drop
    }

    public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo)
    {
        // No cleanup needed
    }

    public void DragCancelled()
    {
        // No cleanup needed
    }

    public bool TryCatchOccurredException(Exception exception)
    {
        return false;
    }
}
