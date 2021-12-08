﻿using ClipboardCanvas.Models;
using ClipboardCanvas.ViewModels;

namespace ClipboardCanvas.ModelViews
{
    public interface ICollectionPreviewPageView
    {
        ICollectionModel AssociatedCollectionModel { get; }

        ISearchControlModel SearchControlModel { get; }

        void PrepareConnectedAnimation(int itemIndex);

        void ScrollItemToView(CollectionPreviewItemViewModel itemToScrollTo);

        void ScrollToItemOnInitialization(CollectionPreviewItemViewModel itemToScrollTo);
    }
}
