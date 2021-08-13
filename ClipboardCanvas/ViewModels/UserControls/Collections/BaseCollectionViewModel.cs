﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Microsoft.Toolkit.Mvvm.Input;
using Windows.System;
using ClipboardCanvas.DataModels;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;

using ClipboardCanvas.EventArguments.CanvasControl;
using ClipboardCanvas.EventArguments.Collections;
using ClipboardCanvas.Extensions;
using ClipboardCanvas.Helpers.Filesystem;
using ClipboardCanvas.Helpers.SafetyHelpers;
using ClipboardCanvas.Models;
using ClipboardCanvas.Enums;
using ClipboardCanvas.Helpers.SafetyHelpers.ExceptionReporters;
using ClipboardCanvas.Exceptions;
using ClipboardCanvas.Contexts;
using ClipboardCanvas.Services;
using ClipboardCanvas.Helpers;
using ClipboardCanvas.ViewModels.UserControls.InAppNotifications;

namespace ClipboardCanvas.ViewModels.UserControls.Collections
{
    public abstract class BaseCollectionViewModel : ObservableObject, ICollectionModel, IDisposable
    {
        #region Protected Members

        protected readonly SafeWrapperResult CollectionFolderNotFound = new SafeWrapperResult(OperationErrorCode.NotFound, new DirectoryNotFoundException(), "The folder associated with this collection was not found.");

        protected readonly SafeWrapperResult RestrictedAccessUnauthorized = StaticExceptionReporters.DefaultSafeWrapperExceptionReporter.GetStatusResult(new UnauthorizedAccessException());

        protected StorageFolder collectionFolder;

        protected CanvasNavigationDirection canvasNavigationDirection;

        protected int currentIndex;

        protected StorageFile iconFile;

        protected IDialogService DialogService { get; } = Ioc.Default.GetService<IDialogService>();

        #endregion

        #region Public Properties

        public ObservableCollection<CollectionItemViewModel> CollectionItems { get; protected set; }

        public SearchContext SearchContext { get; set; }

        public bool IsCollectionAvailable { get; protected set; }

        public virtual bool IsOnNewCanvas => currentIndex == CollectionItems.Count;

        public CollectionItemViewModel CurrentCollectionItemViewModel => CollectionItems.Count == currentIndex ? null : CollectionItems[currentIndex];

        public string CollectionPath { get; protected set; }

        public string DisplayName => Path.GetFileName(CollectionPath);
        
        public bool IsCollectionInitialized { get; protected set; }

        protected bool isCanvasInitializing;
        public bool IsCollectionInitializing
        {
            get => isCanvasInitializing;
            protected set => SetProperty(ref isCanvasInitializing, value);
        }

        protected bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        protected string editBoxText;
        public string EditBoxText
        {
            get => editBoxText;
            set => SetProperty(ref editBoxText, value);
        }

        protected bool isEditingName;
        public bool IsEditingName
        {
            get => isEditingName;
            protected set => SetProperty(ref isEditingName, value);
        }

        protected bool editBoxFocus;
        public bool EditBoxFocus
        {
            get => editBoxFocus;
            protected set => SetProperty(ref editBoxFocus, value);
        }

        protected SafeWrapperResult collectionErrorInfo;
        public SafeWrapperResult CollectionErrorInfo
        {
            get => collectionErrorInfo;
            protected set => SetProperty(ref collectionErrorInfo, value);
        }

        protected bool errorIconVisibility;
        public bool ErrorIconVisibility
        {
            get => errorIconVisibility;
            protected set => SetProperty(ref errorIconVisibility, value);
        }

        protected bool usesCustomIcon;
        public bool UsesCustomIcon
        {
            get => usesCustomIcon;
            set => SetProperty(ref usesCustomIcon, value);
        }

        protected BitmapImage customIcon;
        public BitmapImage CustomIcon
        {
            get => customIcon;
            set => SetProperty(ref customIcon, value);
        }

        #endregion

        #region Events

        public event EventHandler<OpenNewCanvasRequestedEventArgs> OnOpenNewCanvasRequestedEvent;

        public event EventHandler<CanvasLoadFailedEventArgs> OnCanvasLoadFailedEvent;

        public event EventHandler<GoToHomepageRequestedEventArgs> OnGoToHomepageRequestedEvent;

        public event EventHandler<CollectionErrorRaisedEventArgs> OnCollectionErrorRaisedEvent;

        public event EventHandler<CollectionItemsInitializationStartedEventArgs> OnCollectionItemsInitializationStartedEvent;

        public event EventHandler<CollectionItemsInitializationFinishedEventArgs> OnCollectionItemsInitializationFinishedEvent;

        public event EventHandler<TipTextUpdateRequestedEventArgs> OnTipTextUpdateRequestedEvent;

        #endregion

        #region Commands

        public ICommand OpenCollectionLocationCommand { get; protected set; }

        public ICommand ChangeCollectionIconCommand { get; protected set; }

        public ICommand RemoveCollectionIconCommand { get; protected set; }

        public ICommand ReloadCollectionCommand { get; protected set; }

        public ICommand StartRenameCollectionCommand { get; protected set; }

        public ICommand RenameBoxKeyDownCommand { get; protected set; }

        public ICommand RenameBoxLostFocusCommand { get; protected set; }

        public ICommand RemoveCollectionCommand { get; protected set; }

        #endregion

        #region Constructor

        public BaseCollectionViewModel(StorageFolder collectionFolder)
            : this(collectionFolder, null)
        {
        }

        public BaseCollectionViewModel(string collectionPath)
            : this(null, collectionPath)
        {
        }

        public BaseCollectionViewModel(StorageFolder collectionFolder, string collectionPath)
        {
            this.collectionFolder = collectionFolder;
            if (!string.IsNullOrEmpty(collectionPath))
            {
                this.CollectionPath = collectionPath;
            }
            else
            {
                this.CollectionPath = collectionFolder?.Path;
            }

            this.CollectionItems = new ObservableCollection<CollectionItemViewModel>();

            // Create commands
            OpenCollectionLocationCommand = new AsyncRelayCommand(OpenCollectionLocation);
            ChangeCollectionIconCommand = new AsyncRelayCommand(ChangeCollectionIcon);
            RemoveCollectionIconCommand = new AsyncRelayCommand(RemoveCollectionIcon);
            ReloadCollectionCommand = new AsyncRelayCommand(ReloadCollection);
        }

        #endregion

        #region Command Implementation

        private async Task OpenCollectionLocation()
        {
            if (!IsCollectionAvailable)
            {
                return;
            }

            await Launcher.LaunchFolderAsync(collectionFolder);
        }

        private async Task ChangeCollectionIcon()
        {
            string errorMessage = "Couldn't set custom Collection icon.";

            StorageFile pickedIcon = await DialogService.PickSingleFile(new List<string>() { ".png", ".jpg", ".jpeg"/*, ".gif", ".svg"*/ });
            if (pickedIcon != null)
            {
                // If already has an icon...
                if (UsesCustomIcon && this.iconFile != null)
                {
                    SafeWrapperResult result = await FilesystemOperations.DeleteItem(this.iconFile);

                    if (!result)
                    {
                        await PushErrorNotification("Current icon could not be deleted.", result);
                    }
                }

                SafeWrapper<StorageFolder> iconsFolder = await StorageHelpers.GetCollectionIconsFolder();
                if (!iconsFolder)
                {
                    await PushErrorNotification(errorMessage, iconsFolder);
                    return;
                }

                SafeWrapper<StorageFile> iconFile = await FilesystemOperations.CreateFile(iconsFolder, pickedIcon.Name);
                if (!iconFile)
                {
                    await PushErrorNotification(errorMessage, iconFile);
                    return;
                }
                this.iconFile = iconFile.Result;

                SafeWrapperResult copyResult = await FilesystemOperations.CopyFileAsync(pickedIcon, this.iconFile, null); // TODO: In the future, add StatusCenter - StatusCenter.CreateNewOperation().OperationContext;
                if (!copyResult)
                {
                    await PushErrorNotification(errorMessage, copyResult);
                    return;
                }

                SafeWrapperResult setIconResult = await InitializeIconIfSet(this.iconFile);
                if (!setIconResult)
                {
                    await PushErrorNotification(errorMessage, setIconResult);
                    return;
                }

                // Serialize again because icon was updated
                SettingsSerializationHelpers.UpdateSavedCollectionsSetting();
            }
        }

        public async Task RemoveCollectionIcon()
        {
            if (UsesCustomIcon && iconFile != null)
            {
                SafeWrapperResult result = await FilesystemOperations.DeleteItem(iconFile);

                if (!result)
                {
                    if (result != OperationErrorCode.NotFound) // Only if it wasn't NotFound -- if it was, continue as usual
                    { 
                        await PushErrorNotification("Couldn't remove icon.", result);
                        return;
                    }
                }

                iconFile = null;
                CustomIcon = null;
                UsesCustomIcon = false;

                // Serialize again because icon was updated
                SettingsSerializationHelpers.UpdateSavedCollectionsSetting();
            }
        }

        private async Task ReloadCollection()
        {
            await InitializeCollectionFolder();
            await InitializeCollectionItems();
        }

        #endregion

        #region ICollectionModel

        public async Task<SafeWrapper<CanvasItem>> CreateNewCanvasFolder(string folderName = null)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = DateTime.Now.ToString(Constants.FileSystem.CANVAS_FILE_FILENAME_DATE_FORMAT);
            }

            SafeWrapper<StorageFolder> folder = await FilesystemOperations.CreateFolder(collectionFolder, folderName);

            CollectionItemViewModel collectionItem = null;
            if (folder)
            {
                collectionItem = new CollectionItemViewModel(folder.Result);
                AddCollectionItem(collectionItem);
            }

            return (collectionItem, folder.Details);
        }

        public async Task<SafeWrapper<CanvasItem>> CreateNewCanvasFile(string fileName)
        {
            var result = await CreateNewCollectionItem(fileName);

            return (result.Result, result.Details);
        }

        public async Task<SafeWrapper<CanvasItem>> CreateNewCanvasFileFromExtension(string extension)
        {
            var result = await CreateNewCollectionItemFromExtension(extension);

            return (result.Result, result.Details);
        }

        public async Task<SafeWrapper<CollectionItemViewModel>> CreateNewCollectionItemFromExtension(string extension)
        {
            string fileName = DateTime.Now.ToString(Constants.FileSystem.CANVAS_FILE_FILENAME_DATE_FORMAT);
            fileName = $"{fileName}{extension}";

            return await CreateNewCollectionItem(fileName);
        }

        public async Task<SafeWrapper<CollectionItemViewModel>> CreateNewCollectionItem(string fileName)
        {
            if (collectionFolder == null)
            {
                return new SafeWrapper<CollectionItemViewModel>(null, CollectionFolderNotFound);
            }

            SafeWrapper<StorageFile> file = await FilesystemOperations.CreateFile(collectionFolder, fileName);

            CollectionItemViewModel collectionItem = null;
            if (file)
            {
                collectionItem = new CollectionItemViewModel(file.Result);
                AddCollectionItem(collectionItem);
            }

            return (collectionItem, file.Details);
        }

        public async Task<SafeWrapperResult> DeleteCollectionItem(CollectionItemViewModel itemToDelete, bool permanently = true)
        {
            SafeWrapperResult result = await FilesystemOperations.DeleteItem(itemToDelete.AssociatedItem, permanently);

            if (result)
            {
                RemoveCollectionItem(itemToDelete);
            }

            return result;
        }

        public CollectionItemViewModel FindCollectionItem(CanvasItem canvasItem)
        {
            return CollectionItems.FirstOrDefault((item) => item.AssociatedItem.Path == canvasItem.AssociatedItem.Path);
        }

        public virtual void NavigateFirst(ICanvasPreviewModel pasteCanvasModel)
        {
            SetIndexOnNewCanvas();
            canvasNavigationDirection = CanvasNavigationDirection.Forward;

            OnOpenNewCanvasRequestedEvent?.Invoke(this, new OpenNewCanvasRequestedEventArgs());
        }

        public virtual async Task NavigateNext(ICanvasPreviewModel pasteCanvasModel, CancellationToken cancellationToken)
        {
            currentIndex++;
            canvasNavigationDirection = CanvasNavigationDirection.Forward;

            if (currentIndex == CollectionItems.Count)
            {
                // Open new canvas if _currentIndex exceeds the _items size
                OnOpenNewCanvasRequestedEvent?.Invoke(this, new OpenNewCanvasRequestedEventArgs());
            }
            else
            {
                // Otherwise, load existing data from file
                await LoadCanvasFromCollection(pasteCanvasModel, cancellationToken);
            }
        }

        public virtual async Task NavigateLast(ICanvasPreviewModel pasteCanvasModel, CancellationToken cancellationToken)
        {
            currentIndex = 0;
            canvasNavigationDirection = CanvasNavigationDirection.Backward;

            await LoadCanvasFromCollection(pasteCanvasModel, cancellationToken);
        }

        public virtual async Task NavigateBack(ICanvasPreviewModel pasteCanvasModel, CancellationToken cancellationToken)
        {
            currentIndex--;
            canvasNavigationDirection = CanvasNavigationDirection.Backward;

            await LoadCanvasFromCollection(pasteCanvasModel, cancellationToken);
        }

        public virtual void AddCollectionItem(CollectionItemViewModel collectionItemViewModel)
        {
            CollectionItems.Add(collectionItemViewModel);
        }

        public virtual void RemoveCollectionItem(CollectionItemViewModel collectionItemViewModel)
        {
            CollectionItems.Remove(collectionItemViewModel);
        }

        public virtual bool HasNext()
        {
            return currentIndex < CollectionItems.Count;
        }

        public virtual bool HasBack()
        {
            return currentIndex > 0;
        }

        public virtual void SetIndexOnNewCanvas()
        {
            currentIndex = CollectionItems.Count;
        }

        public virtual void UpdateIndex(CollectionItemViewModel collectionItemViewModel)
        {
            int newIndex = -1;

            if (collectionItemViewModel != null)
            {
                newIndex = CollectionItems.IndexOf(collectionItemViewModel);
            }

            if (newIndex == -1)
            {
                SetIndexOnNewCanvas();
            }
            else
            {
                currentIndex = newIndex;
            }
        }

        public bool IsOnOpenedCanvas(CollectionItemViewModel collectionItemViewModel)
        {
            int indexOfRequestedItemViewModel = CollectionItems.IndexOf(collectionItemViewModel);

            return indexOfRequestedItemViewModel == currentIndex;
        }

        public abstract bool CheckCollectionAvailability();

        public virtual CollectionConfigurationModel ConstructConfigurationModel()
        {
            return new CollectionConfigurationModel(CollectionPath, UsesCustomIcon, iconFile?.Name);
        }

        public virtual async Task LoadCanvasFromCollection(ICanvasPreviewModel pasteCanvasModel, CancellationToken cancellationToken, CollectionItemViewModel collectionItemViewModel = null)
        {
            // You can only load existing data
            if (CollectionItems.IsEmpty() || (canvasNavigationDirection == CanvasNavigationDirection.Forward && (IsOnNewCanvas && collectionItemViewModel == null)))
            {
                OnOpenNewCanvasRequestedEvent?.Invoke(this, new OpenNewCanvasRequestedEventArgs());
                return;
            }
            else
            {
                currentIndex = Extensions.CollectionExtensions.IndexFitBounds(CollectionItems.Count, currentIndex);

                if (collectionItemViewModel == null)
                {
                    collectionItemViewModel = CollectionItems[currentIndex];
                }
                else
                {
                    int providedCollectionItemModelIndex = CollectionItems.IndexOf(collectionItemViewModel);
                    currentIndex = providedCollectionItemModelIndex;
                }

                SafeWrapperResult result = await pasteCanvasModel.TryLoadExistingData(collectionItemViewModel, cancellationToken);

                if (result == OperationErrorCode.NotFound && result.Exception is not ReferencedFileNotFoundException) // A canvas is missing, meaning we need to reload all other items
                {
                    if (!StorageHelpers.Exists(CollectionPath))
                    {
                        SetCollectionError(CollectionFolderNotFound);

                        // TODO: Pass error code here in the future
                        OnGoToHomepageRequestedEvent?.Invoke(this, new GoToHomepageRequestedEventArgs());
                        return;
                    }

                    // We must reload items because some were missing
                    OnTipTextUpdateRequestedEvent?.Invoke(this, new TipTextUpdateRequestedEventArgs("We've noticed some items went missing. We're reloading the Collection for you.", TimeSpan.FromMilliseconds(Constants.UI.CanvasContent.COLLECTION_RELOADING_TIP_DELAY)));
                    await InitializeCollectionItems();

                    if (canvasNavigationDirection == CanvasNavigationDirection.Forward)
                    {
                        if (!HasNext())
                        {
                            // Doesn't have next, so we're on new canvas - open new canvas
                            OnOpenNewCanvasRequestedEvent?.Invoke(this, new OpenNewCanvasRequestedEventArgs());
                        }
                        else
                        {
                            currentIndex++;
                        }
                    }
                    else
                    {
                        if (HasBack())
                        {
                            currentIndex--;
                        }
                    }

                    if (CollectionItems.IsEmpty())
                    {
                        OnOpenNewCanvasRequestedEvent?.Invoke(this, new OpenNewCanvasRequestedEventArgs());
                        return;
                    }
                    else
                    {
                        int providedCollectionItemModelIndex = CollectionItems.IndexOf(collectionItemViewModel);
                        if (providedCollectionItemModelIndex != -1)
                        {
                            currentIndex = providedCollectionItemModelIndex;
                        }

                        if (currentIndex < CollectionItems.Count)
                        {
                            collectionItemViewModel = CollectionItems[currentIndex];

                            // Load canvas again
                            result = await pasteCanvasModel.TryLoadExistingData(collectionItemViewModel, cancellationToken);
                        }
                    }
                }
                else if (result == OperationErrorCode.InProgress)
                {
                    // Content is still being pasted...
                    // TODO: Hook event to collectionItemViewModel.OperationContext.OnOperationFinishedEvent
                }
                else if (result == OperationErrorCode.InvalidOperation)
                {
                    // View Model wasn't found
                    // Cannot display content for this file. - i.e. canvas display doesn't exists for this file
                }

                if (!result)
                {
                    OnCanvasLoadFailedEvent?.Invoke(this, new CanvasLoadFailedEventArgs(result));
                }
            }
        }

        public virtual async Task<bool> InitializeCollectionItems()
        {
            if (collectionFolder != null)
            {
                IsCollectionInitializing = true;
                OnCollectionItemsInitializationStartedEvent?.Invoke(this, new CollectionItemsInitializationStartedEventArgs(this));

                IEnumerable<IStorageItem> items = await Task.Run(async () => await collectionFolder.GetItemsAsync());

                CollectionItems.Clear();
                if (!items.IsEmpty())
                {
                    // Sort items from oldest (last canvas) to newest (first canvas)
                    items = items.OrderBy((x) => x.DateCreated.DateTime);

                    // Save indexes for later
                    int savedIndex = currentIndex;
                    int savedItemsCount = CollectionItems.Count;

                    foreach (var item in items)
                    {
                        AddCollectionItem(new CollectionItemViewModel(item));
                    }

                    // TODO: save index somewhere to file?
                    // Calculate new index
                    int newItemsCount = CollectionItems.Count;
                    int newIndex = Math.Max(savedIndex, savedIndex - (savedItemsCount - newItemsCount));

                    this.currentIndex = Extensions.CollectionExtensions.IndexFitBounds(this.CollectionItems.Count + 1, newIndex); // Increase the Items.Count by one to account for new canvas being always Items.Count
                }

                IsCollectionInitializing = false;
                IsCollectionInitialized = true;
                OnCollectionItemsInitializationFinishedEvent?.Invoke(this, new CollectionItemsInitializationFinishedEventArgs(this));

                return true;
            }

            IsCollectionInitialized = false;
            return false;
        }

        public async Task<bool> InitializeCollectionFolder()
        {
            if (!CheckCollectionAvailability())
            {
                return false;
            }
            else
            {
                SafeWrapper<StorageFolder> result = await StorageHelpers.ToStorageItemWithError<StorageFolder>(CollectionPath);
                collectionFolder = result;

                if (!result)
                {
                    SetCollectionError(result);

                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        public async Task<SafeWrapperResult> InitializeIconIfSet(CollectionConfigurationModel collectionConfiguration)
        {
            if (collectionConfiguration == null || !collectionConfiguration.usesCustomIcon)
            {
                return SafeWrapperResult.SUCCESS;
            }

            SafeWrapper<StorageFolder> iconsFolder = await StorageHelpers.GetCollectionIconsFolder();
            if (!iconsFolder)
            {
                return iconsFolder;
            }

            SafeWrapper<StorageFile> iconFile = await StorageHelpers.ToStorageItemWithError<StorageFile>(Path.Combine(iconsFolder.Result.Path, collectionConfiguration.iconFileName));

            if (!iconFile)
            {
                return iconFile;
            }

            return await InitializeIconIfSet(iconFile);
        }

        public async Task<SafeWrapperResult> InitializeIconIfSet(StorageFile iconFile)
        {
            UsesCustomIcon = true;
            this.iconFile = iconFile;
            await Task.Delay(Constants.UI.CONTROL_LOAD_DELAY);

            return await SafeWrapperRoutines.SafeWrapAsync(async () =>
            {
                using (IRandomAccessStream fileStream = await iconFile.OpenReadAsync())
                {
                    CustomIcon = await ImagingHelpers.ToBitmapAsync(fileStream);
                }
            });
        }

        #endregion

        #region Protected Helpers

        protected virtual void SetCollectionError(SafeWrapperResult safeWrapperResult)
        {
            if (!safeWrapperResult)
            {
                CollectionErrorInfo = safeWrapperResult;
                ErrorIconVisibility = true;
                IsCollectionAvailable = false;
            }
            else
            {
                ErrorIconVisibility = false;
                IsCollectionAvailable = true;
            }

            OnCollectionErrorRaisedEvent?.Invoke(this, new CollectionErrorRaisedEventArgs(safeWrapperResult));
        }

        protected virtual async Task PushErrorNotification(string errorMessage, SafeWrapperResult result)
        {
            IInAppNotification notification = DialogService.GetNotification();
            notification.ViewModel.NotificationText = $"{errorMessage} Error: {result.ErrorCode}";
            notification.ViewModel.ShownButtons = InAppNotificationButtonType.OkButton;

            await notification.ShowAsync(4000);
        }

        #endregion

        #region IDisposable

        public virtual void Dispose()
        {

        }

        #endregion
    }
}
