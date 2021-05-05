﻿using System;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Threading;
using System.Diagnostics;

using ClipboardCanvas.DataModels;
using ClipboardCanvas.Enums;
using ClipboardCanvas.EventArguments;
using ClipboardCanvas.Models;
using ClipboardCanvas.ModelViews;
using ClipboardCanvas.Helpers;
using ClipboardCanvas.Extensions;
using Windows.UI.ViewManagement;

namespace ClipboardCanvas.ViewModels.UserControls
{
    public class DisplayControlViewModel : ObservableObject, IDisposable
    {
        #region Private Members

        private readonly IDisplayControlView _view;

        private ICollectionsContainerModel _currentCollectionContainer;

        private CancellationTokenSource _canvasLoadCancellationTokenSource;

        private IWindowTitleBarControlModel WindowTitleBarControlModel => _view?.WindowTitleBarControlModel;

        private INavigationToolBarControlModel NavigationToolBarControlModel => _view?.NavigationToolBarControlModel;

        /// <summary>
        /// Stores canvas control
        /// <br/><br/>
        /// Note: This might be null depending on the current view
        /// </summary>
        private IPasteCanvasModel PasteCanvasModel => _view?.PasteCanvasModel;

        #endregion

        #region Public Properties

        private DisplayFrameNavigationDataModel _CurrentPageNavigation;
        public DisplayFrameNavigationDataModel CurrentPageNavigation
        {
            get => _CurrentPageNavigation;
            private set
            {
                // Page switch requested so we need to unhook events
                if (_CurrentPageNavigation != null && _CurrentPageNavigation.pageType == DisplayPageType.CanvasPage /* && value.pageType != CurrentPageType.CanvasPage)*/)
                {
                    UnhookCanvasControlEvents(); // Unhook events
                    //PasteCanvasModel.Dispose(); // Dispose stuff
                }

                if (SetProperty(ref _CurrentPageNavigation, value/*, comparer: new ComparingExtensions.DefaultEqualityComparer<DisplayFrameNavigationDataModel>()*/))
                {
                    _CurrentPageNavigation.simulateNavigation = false;
                    NavigationToolBarControlModel?.NotifyCurrentPageChanged(value);

                    // Hook events if the page navigated is canvas page
                    if (_CurrentPageNavigation != null && _CurrentPageNavigation.pageType == DisplayPageType.CanvasPage)
                    {
                        HookCanvasControlEvents();
                    }

                    OnPageNavigated(_CurrentPageNavigation);
                }
            }
        }

        public DisplayPageType CurrentPage => CurrentPageNavigation.pageType;

        #endregion

        #region Constructor

        public DisplayControlViewModel(IDisplayControlView view)
        {
            this._view = view;
            this._canvasLoadCancellationTokenSource = new CancellationTokenSource();

            HookCollectionsEvents();

            InitializeAfterConstructor();

            OpenPage(DisplayPageType.CanvasPage);
        }

        #endregion

        #region Event Handlers

        #region WindowTitleBarControlModel

        private async void WindowTitleBarControlModel_OnSwitchApplicationViewRequestedEvent(object sender, EventArgs e)
        {
            Debugger.Break(); // TODO: Improve Compact Overlay mode

            if (ApplicationView.GetForCurrentView().ViewMode == ApplicationViewMode.Default)
            {
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
            }
            else
            {
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
            }
        }

        #endregion

        #region NavigationControlModel

        private async void NavigationControlModel_NavigateLastEvent(object sender, EventArgs e)
        {
            if (CurrentPage == DisplayPageType.CanvasPage && _currentCollectionContainer.HasBack())
            {
                _canvasLoadCancellationTokenSource.Cancel();
                _canvasLoadCancellationTokenSource.Dispose();
                _canvasLoadCancellationTokenSource = new CancellationTokenSource();

                await _currentCollectionContainer.NavigateLast(PasteCanvasModel, _canvasLoadCancellationTokenSource.Token);
                CheckNavigation();
                await SetSuggestedActions();
            }
        }

        private async void NavigationControlModel_NavigateBackEvent(object sender, EventArgs e)
        {
            if (CurrentPage == DisplayPageType.CanvasPage && _currentCollectionContainer.HasBack())
            {
                _canvasLoadCancellationTokenSource.Cancel();
                _canvasLoadCancellationTokenSource.Dispose();
                _canvasLoadCancellationTokenSource = new CancellationTokenSource();

                await _currentCollectionContainer.NavigateBack(PasteCanvasModel, _canvasLoadCancellationTokenSource.Token);
                CheckNavigation();
                await SetSuggestedActions();
            }
        }

        private async void NavigationControlModel_NavigateFirstEvent(object sender, EventArgs e)
        {
            if (CurrentPage == DisplayPageType.CanvasPage && _currentCollectionContainer.HasNext())
            {
                _canvasLoadCancellationTokenSource.Cancel();
                _canvasLoadCancellationTokenSource.Dispose();
                _canvasLoadCancellationTokenSource = new CancellationTokenSource();

                _currentCollectionContainer.NavigateFirst(PasteCanvasModel);
                CheckNavigation();
                await SetSuggestedActions();
            }
        }

        private async void NavigationControlModel_NavigateForwardEvent(object sender, EventArgs e)
        {
            if (CurrentPage == DisplayPageType.CanvasPage && _currentCollectionContainer.HasNext())
            {
                _canvasLoadCancellationTokenSource.Cancel();
                _canvasLoadCancellationTokenSource.Dispose();
                _canvasLoadCancellationTokenSource = new CancellationTokenSource();

                await _currentCollectionContainer.NavigateNext(PasteCanvasModel, _canvasLoadCancellationTokenSource.Token);
                CheckNavigation();
                await SetSuggestedActions();
            }
        }

        private void NavigationControlModel_GoToHomeEvent(object sender, EventArgs e)
        {
            OpenPage(DisplayPageType.HomePage);
        }

        private async void NavigationControlModel_GoToCanvasEvent(object sender, EventArgs e)
        {
            OpenPage(DisplayPageType.CanvasPage);

            CheckNavigation();

            Debug.WriteLine(_currentCollectionContainer.Name);

            // We might navigate from home to a canvas that's already filled, so initialize the content
            if (_currentCollectionContainer.IsFilled)
            {
                await PasteCanvasModel.TryLoadExistingData(_currentCollectionContainer.CurrentCanvas, _canvasLoadCancellationTokenSource.Token);
            }
        }

        #endregion

        #region CollectionsControlViewModel

        private void CollectionsControlViewModel_OnCollectionItemsRefreshRequested(object sender, CollectionItemsRefreshRequestedEventArgs e)
        {
            if (NavigationToolBarControlModel != null && e.selectedCollection != null)
            {
                CheckNavigation();
            }
        }

        private void CollectionsControlViewModel_OnCollectionAdded(object sender, CollectionAddedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void CollectionsControlViewModel_OnCollectionRemoved(object sender, CollectionRemovedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void CollectionsControlViewModel_OnCollectionSelectionChanged(object sender, CollectionSelectionChangedEventArgs e)
        {
            _currentCollectionContainer = e.selectedCollection;
        }

        private void CollectionsControlViewModel_OnCollectionOpenRequested(object sender, CollectionOpenRequestedEventArgs e)
        {
            Debugger.Break(); // This operation is not implemented
            return;

            OpenPage(DisplayPageType.CollectionsPreview);
        }

        #endregion

        #region PasteCanvasModel

        private void PasteCanvasModel_OnErrorOccurredEvent(object sender, ErrorOccurredEventArgs e)
        {
            // TODO: Implement this
        }

        private void PasteCanvasModel_OnFileDeletedEvent(object sender, FileDeletedEventArgs e)
        {
            // TODO: Implement this
        }

        private void PasteCanvasModel_OnFileModifiedEvent(object sender, FileModifiedEventArgs e)
        {
            // TODO: Implement this
        }

        private void PasteCanvasModel_OnFileCreatedEvent(object sender, FileCreatedEventArgs e)
        {
            _currentCollectionContainer.RefreshAddItem(e.file, e.contentType);
        }

        private async void PasteCanvasModel_OnPasteRequestedEvent(object sender, PasteRequestedEventArgs e)
        {
            if (e.hasContent)  // TODO: f It doesn't work - always is false bruh why? my crappy code
            {
                // Already has content, meaning we need to switch to the next page
                OpenNewCanvas();

                // Forward the paste operation
                await PasteCanvasModel.TryPasteData(e.forwardedDataPackage, _canvasLoadCancellationTokenSource.Token);
            }
        }

        private async void PasteCanvasModel_OnContentLoadedEvent(object sender, ContentLoadedEventArgs e)
        {
            CheckNavigation();

            await SetSuggestedActions();
        }

        private void PasteCanvasModel_OnOpenNewCanvasRequestedEvent(object sender, OpenOpenNewCanvasRequestedEventArgs e)
        {
            OpenNewCanvas();
        }

        #endregion

        #endregion

        #region Public Helpers

        public async void InitializeAfterLoad()
        {
            HookTitleBarEvents();
            HookToolbarEvents();

            NavigationToolBarControlModel.NotifyCurrentPageChanged(CurrentPageNavigation);

            // We must hook them here because only now it is not null
            // Hook events if the page navigated is canvas page
            if (CurrentPageNavigation != null && CurrentPage == DisplayPageType.CanvasPage)
            {
                HookCanvasControlEvents();
            }

            UpdateTitleBar();
            CheckNavigation();
            await SetSuggestedActions();
        }

        #endregion

        #region Private Helpers

        private void OpenPage(DisplayPageType pageType, bool simulateNavigation = false)
        {
            CurrentPageNavigation = new DisplayFrameNavigationDataModel(pageType, _currentCollectionContainer, simulateNavigation);
        }

        private async void InitializeAfterConstructor()
        {
            await CollectionsControlViewModel.ReloadAllCollections();
        }

        private void CheckNavigation()
        {
            NavigationToolBarControlModel.NavigationControlModel.NavigateBackEnabled = _currentCollectionContainer.HasBack();
            NavigationToolBarControlModel.NavigationControlModel.NavigateForwardEnabled = _currentCollectionContainer.HasNext();
        }

        private void OpenNewCanvas()
        {
            _currentCollectionContainer.DangerousSetIndex(_currentCollectionContainer.Items.Count);
            CurrentPageNavigation = new DisplayFrameNavigationDataModel(
                    CurrentPageNavigation.pageType,
                    CurrentPageNavigation.collectionContainer,
                    CurrentPageNavigation.transitionInfo, true);
        }

        private async void OnPageNavigated(DisplayFrameNavigationDataModel navigationDataModel)
        {
            UpdateTitleBar();

            await SetSuggestedActions();
        }

        private async Task SetSuggestedActions()
        {
            if (NavigationToolBarControlModel == null)
            {
                return;
            }

            switch (CurrentPage)
            {
                case DisplayPageType.CanvasPage:
                    {
                        // Add suggested actions
                        if (_currentCollectionContainer.IsFilled)
                        {
                            NavigationToolBarControlModel.AdaptiveOptionsControlModel.SetActions(await AdaptiveActionsHelpers.GetActionsForFilledCanvasPage(_currentCollectionContainer));
                        }
                        else
                        {
                            NavigationToolBarControlModel.AdaptiveOptionsControlModel.SetActions(AdaptiveActionsHelpers.GetActionsForEmptyCanvasPage(PasteCanvasModel));
                        }

                        break;
                    }

                case DisplayPageType.HomePage:
                    {
                        NavigationToolBarControlModel.AdaptiveOptionsControlModel.SetActions(AdaptiveActionsHelpers.GetActionsForUnselectedCollection());

                        break;
                    }
            }
        }

        private void UpdateTitleBar()
        {
            if (WindowTitleBarControlModel == null)
            {
                return;
            }

            switch(CurrentPage)
            {
                case DisplayPageType.HomePage:
                    {
                        WindowTitleBarControlModel.SetTitleBarForCollectionsView();

                        break;
                    }

                case DisplayPageType.CanvasPage:
                    {
                        WindowTitleBarControlModel.SetTitleBarForCanvasView(_currentCollectionContainer.Name);

                        break;
                    }

                case DisplayPageType.CollectionsPreview:
                    {
                        WindowTitleBarControlModel.SetTitleBarForDefaultView();

                        break;
                    }
            }
        }

        #endregion

        #region Event Hooks

        private void UnhookTitleBarEvents()
        {
            if (this.WindowTitleBarControlModel != null)
            {
                this.WindowTitleBarControlModel.OnSwitchApplicationViewRequestedEvent -= WindowTitleBarControlModel_OnSwitchApplicationViewRequestedEvent;
            }
        }

        private void HookTitleBarEvents()
        {
            UnhookTitleBarEvents();
            if (this.WindowTitleBarControlModel != null)
            {
                this.WindowTitleBarControlModel.OnSwitchApplicationViewRequestedEvent += WindowTitleBarControlModel_OnSwitchApplicationViewRequestedEvent;
            }
        }

        private void HookToolbarEvents()
        {
            UnhookToolbarEvents();
            if (this.NavigationToolBarControlModel != null && this.NavigationToolBarControlModel.NavigationControlModel != null)
            {
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateLastEvent += NavigationControlModel_NavigateLastEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateBackEvent += NavigationControlModel_NavigateBackEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateFirstEvent += NavigationControlModel_NavigateFirstEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateForwardEvent += NavigationControlModel_NavigateForwardEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.GoToHomeEvent += NavigationControlModel_GoToHomeEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.GoToCanvasEvent += NavigationControlModel_GoToCanvasEvent;
            }
        }

        private void UnhookToolbarEvents()
        {
            if (this.NavigationToolBarControlModel != null && this.NavigationToolBarControlModel.NavigationControlModel != null)
            {
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateLastEvent -= NavigationControlModel_NavigateLastEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateBackEvent -= NavigationControlModel_NavigateBackEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateForwardEvent -= NavigationControlModel_NavigateForwardEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.NavigateFirstEvent -= NavigationControlModel_NavigateFirstEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.GoToHomeEvent -= NavigationControlModel_GoToHomeEvent;
                this.NavigationToolBarControlModel.NavigationControlModel.GoToCanvasEvent -= NavigationControlModel_GoToCanvasEvent;
            }
        }

        private void HookCollectionsEvents()
        {
            UnhookCollectionsEvents();
            CollectionsControlViewModel.OnCollectionOpenRequested += CollectionsControlViewModel_OnCollectionOpenRequested;
            CollectionsControlViewModel.OnCollectionSelectionChanged += CollectionsControlViewModel_OnCollectionSelectionChanged;
            CollectionsControlViewModel.OnCollectionRemoved += CollectionsControlViewModel_OnCollectionRemoved;
            CollectionsControlViewModel.OnCollectionAdded += CollectionsControlViewModel_OnCollectionAdded;
            CollectionsControlViewModel.OnCollectionItemsRefreshRequested += CollectionsControlViewModel_OnCollectionItemsRefreshRequested;
        }

        private void UnhookCollectionsEvents()
        {
            CollectionsControlViewModel.OnCollectionOpenRequested -= CollectionsControlViewModel_OnCollectionOpenRequested;
            CollectionsControlViewModel.OnCollectionSelectionChanged -= CollectionsControlViewModel_OnCollectionSelectionChanged;
            CollectionsControlViewModel.OnCollectionRemoved -= CollectionsControlViewModel_OnCollectionRemoved;
            CollectionsControlViewModel.OnCollectionAdded -= CollectionsControlViewModel_OnCollectionAdded;
            CollectionsControlViewModel.OnCollectionItemsRefreshRequested -= CollectionsControlViewModel_OnCollectionItemsRefreshRequested;
        }

        private void HookCanvasControlEvents()
        {
            UnhookCanvasControlEvents();
            if (PasteCanvasModel != null)
            {
                this.PasteCanvasModel.OnOpenNewCanvasRequestedEvent += PasteCanvasModel_OnOpenNewCanvasRequestedEvent;
                this.PasteCanvasModel.OnContentLoadedEvent += PasteCanvasModel_OnContentLoadedEvent;
                this.PasteCanvasModel.OnPasteRequestedEvent += PasteCanvasModel_OnPasteRequestedEvent;
                this.PasteCanvasModel.OnFileCreatedEvent += PasteCanvasModel_OnFileCreatedEvent;
                this.PasteCanvasModel.OnFileModifiedEvent += PasteCanvasModel_OnFileModifiedEvent;
                this.PasteCanvasModel.OnFileDeletedEvent += PasteCanvasModel_OnFileDeletedEvent;
                this.PasteCanvasModel.OnErrorOccurredEvent += PasteCanvasModel_OnErrorOccurredEvent;
            }
        }

        private void UnhookCanvasControlEvents()
        {
            if (PasteCanvasModel != null)
            {
                this.PasteCanvasModel.OnOpenNewCanvasRequestedEvent -= PasteCanvasModel_OnOpenNewCanvasRequestedEvent;
                this.PasteCanvasModel.OnContentLoadedEvent -= PasteCanvasModel_OnContentLoadedEvent;
                this.PasteCanvasModel.OnPasteRequestedEvent -= PasteCanvasModel_OnPasteRequestedEvent;
                this.PasteCanvasModel.OnFileCreatedEvent -= PasteCanvasModel_OnFileCreatedEvent;
                this.PasteCanvasModel.OnFileModifiedEvent -= PasteCanvasModel_OnFileModifiedEvent;
                this.PasteCanvasModel.OnFileDeletedEvent -= PasteCanvasModel_OnFileDeletedEvent;
                this.PasteCanvasModel.OnErrorOccurredEvent -= PasteCanvasModel_OnErrorOccurredEvent;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            UnhookTitleBarEvents();
            UnhookToolbarEvents();
            UnhookCollectionsEvents();
            UnhookCanvasControlEvents();
        }

        #endregion
    }
}