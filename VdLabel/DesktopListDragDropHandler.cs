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
            targetItem.Id != Guid.Empty &&
            dropInfo.TargetCollection is System.Collections.IList collection)
        {
            // Find the current positions
            var sourceIndex = -1;
            var targetInsertIndex = dropInfo.InsertIndex;
            
            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i] is DesktopConfigViewModel item && item.Id == sourceItem.Id)
                {
                    sourceIndex = i;
                    break;
                }
            }

            if (sourceIndex < 0)
            {
                return;
            }

            // Calculate the actual desktop index
            // When moving down (sourceIndex < targetInsertIndex), we need to adjust by -1
            // because after removing the source, all indices shift up
            var actualTargetIndex = targetInsertIndex;
            if (sourceIndex < targetInsertIndex)
            {
                actualTargetIndex -= 1;
            }

            // Adjust for "All Desktops" being the first item (index 0)
            // The actual desktop index should be -1 from the list index
            if (actualTargetIndex > 0)
            {
                actualTargetIndex -= 1;
            }

            // Move the desktop
            this.virualDesktopService.MoveDesktop(sourceItem.Id, actualTargetIndex);
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
