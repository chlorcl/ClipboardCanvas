﻿using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

using ClipboardCanvas.DataModels;
using ClipboardCanvas.DataModels.PastedContentDataModels;
using ClipboardCanvas.Models;
using ClipboardCanvas.ModelViews;
using ClipboardCanvas.EventArguments.InfiniteCanvasEventArgs;
using ClipboardCanvas.Extensions;

namespace ClipboardCanvas.ViewModels.UserControls
{
    public class InteractableCanvasControlViewModel : ObservableObject, IInteractableCanvasControlModel, IDisposable
    {
        #region Private Members

        private readonly IInteractableCanvasControlView _view;

        private readonly DispatcherTimer _saveTimer;

        #endregion

        #region Public Properties

        public ObservableCollection<InteractableCanvasControlItemViewModel> Items { get; private set; }

        private bool _NoItemsTextLoad;
        public bool NoItemsTextLoad
        {
            get => _NoItemsTextLoad;
            set => SetProperty(ref _NoItemsTextLoad, value);
        }

        #endregion

        #region Events

        public event EventHandler<InfiniteCanvasSaveRequestedEventArgs> OnInfiniteCanvasSaveRequestedEvent;

        #endregion

        #region Constructor

        public InteractableCanvasControlViewModel(IInteractableCanvasControlView view)
        {
            this._view = view;

            this._saveTimer = new DispatcherTimer();
            this.Items = new ObservableCollection<InteractableCanvasControlItemViewModel>();

            this._saveTimer.Interval = TimeSpan.FromMilliseconds(Constants.UI.CanvasContent.INFINITE_CANVAS_SAVE_INTERVAL);
            this._saveTimer.Tick += SaveTimer_Tick;
        }

        #endregion

        #region Event Handlers

        private async void SaveTimer_Tick(object sender, object e)
        {
            _saveTimer.Stop();

            await SaveCanvas();
        }

        private void Item_OnInfiniteCanvasItemRemovalRequestedEvent(object sender, InfiniteCanvasItemRemovalRequestedEventArgs e)
        {
            RemoveItem(e.itemToRemove);
        }

        #endregion

        #region Private Helpers

        private async Task SaveCanvas()
        {
            IRandomAccessStream canvasImageStream = await _view.GetCanvasImageStream();

            OnInfiniteCanvasSaveRequestedEvent?.Invoke(this, new InfiniteCanvasSaveRequestedEventArgs(canvasImageStream));
        }

        #endregion

        #region Public Helpers

        public async Task<InteractableCanvasControlItemViewModel> AddItem(ICollectionModel collectionModel, BaseContentTypeModel contentType, CanvasItem canvasFile, CancellationToken cancellationToken)
        {
            var item = new InteractableCanvasControlItemViewModel(_view, collectionModel, contentType, canvasFile, cancellationToken);
            item.OnInfiniteCanvasItemRemovalRequestedEvent += Item_OnInfiniteCanvasItemRemovalRequestedEvent;
            Items.Add(item);
            await item.InitializeItem();
            NoItemsTextLoad = false;

            return item;
        }

        public void RemoveItem(InteractableCanvasControlItemViewModel item)
        {
            item.OnInfiniteCanvasItemRemovalRequestedEvent -= Item_OnInfiniteCanvasItemRemovalRequestedEvent;
            Items.Remove(item);

            NoItemsTextLoad = Items.IsEmpty();
        }

        public InfiniteCanvasConfigurationModel ConstructConfigurationModel()
        {
            var canvasConfigurationModel = new InfiniteCanvasConfigurationModel();

            foreach (var item in Items)
            {
                canvasConfigurationModel.elements.Add(new InfiniteCanvasConfigurationItemModel(item.CanvasItem.AssociatedItem.Path, item.ItemPosition));
            }

            return canvasConfigurationModel;
        }

        public void SetConfigurationModel(InfiniteCanvasConfigurationModel canvasConfigurationModel)
        {
            foreach (var item1 in Items)
            {
                foreach (var item2 in canvasConfigurationModel.elements)
                {
                    if (item1.CanvasItem.AssociatedItem.Path == item2.associatedItemPath)
                    {
                        item1.ItemPosition = item2.locationVector;
                    }
                }
            }
        }

        public async Task RegenerateCanvasPreview()
        {
            await SaveCanvas();
        }

        public void CanvasLoaded()
        {
            NoItemsTextLoad = Items.IsEmpty();
        }

        public void ItemRearranged()
        {
            if (!_saveTimer.IsEnabled)
            {
                _saveTimer.Start();
            }
            else
            {
                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var currentItem = Items[i];

                currentItem.OnInfiniteCanvasItemRemovalRequestedEvent -= Item_OnInfiniteCanvasItemRemovalRequestedEvent;
                currentItem.Dispose();
            }

            Items.Clear();

            this._saveTimer.Stop();
            this._saveTimer.Tick -= SaveTimer_Tick;
        }

        #endregion
    }
}