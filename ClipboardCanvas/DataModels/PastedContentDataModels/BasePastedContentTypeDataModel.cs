﻿using ClipboardCanvas.Enums;
using ClipboardCanvas.Helpers;
using ClipboardCanvas.Helpers.SafetyHelpers;
using ClipboardCanvas.ReferenceItems;
using ClipboardCanvas.ViewModels.UserControls.CanvasDisplay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace ClipboardCanvas.DataModels.PastedContentDataModels
{
    public abstract class BasePastedContentTypeDataModel
    {
        private static readonly SafeWrapperResult FoldersNotSupportedResult = new SafeWrapperResult(OperationErrorCode.InvalidOperation, new InvalidOperationException(), "Displaying content for folders is not yet supported.");

        private static readonly SafeWrapperResult CannotGetContentTypeResult = new SafeWrapperResult(OperationErrorCode.InvalidOperation, new InvalidOperationException(), "Couldn't display content for this file");

        private static readonly SafeWrapperResult CannotReceiveClipboardDataResult = new SafeWrapperResult(OperationErrorCode.AccessUnauthorized, "Couldn't retrieve clipboard data");

        public static async Task<BasePastedContentTypeDataModel> GetContentType(CanvasFile canvasFile, BasePastedContentTypeDataModel contentType)
        {
            if (contentType is InvalidContentTypeDataModel invalidContentType)
            {
                if (!invalidContentType.needsReinitialization)
                {
                    return invalidContentType;
                }
            }

            if (contentType != null)
            {
                return contentType;
            }
            
            if ((await canvasFile.SourceItem) is StorageFolder)
            {
                return new InvalidContentTypeDataModel(FoldersNotSupportedResult, false);
            }
            else if ((await canvasFile.SourceItem) is StorageFile file)
            {
                string ext = Path.GetExtension(file.Path);

                return await GetContentTypeFromExtension(file, ext);
            }
            else // The sourceFile was null
            {
                return new InvalidContentTypeDataModel(CannotGetContentTypeResult, false);
            }
        }

        public static async Task<BasePastedContentTypeDataModel> GetContentType(IStorageItem item, BasePastedContentTypeDataModel contentType)
        {
            if (contentType is InvalidContentTypeDataModel invalidContentType)
            {
                if (!invalidContentType.needsReinitialization)
                {
                    return invalidContentType;
                }
            }

            if (contentType != null)
            {
                return contentType;
            }

            if (item is StorageFile file)
            {
                string ext = Path.GetExtension(file.Path);

                if (ReferenceFile.IsReferenceFile(file))
                {
                    // Reference File, get the destination file extension
                    ReferenceFile referenceFile = await ReferenceFile.GetFile(file);

                    if (referenceFile.ReferencedItem == null)
                    {
                        return new InvalidContentTypeDataModel(referenceFile.LastError, false);
                    }

                    if (referenceFile.ReferencedItem is StorageFolder)
                    {
                        return new InvalidContentTypeDataModel(new SafeWrapperResult(OperationErrorCode.InvalidOperation, new InvalidOperationException(), "Displaying content for folders is not yet supported."), false);
                    }
                    else
                    {
                        file = referenceFile.ReferencedItem as StorageFile;
                    }

                    ext = Path.GetExtension(file.Path);
                }

                return await GetContentTypeFromExtension(file, ext);
            }
            else if (item is StorageFolder)
            {
                return new InvalidContentTypeDataModel(FoldersNotSupportedResult, false);
            }
            else
            {
                return new InvalidContentTypeDataModel(CannotGetContentTypeResult, false);
            }
        }

        public static async Task<BasePastedContentTypeDataModel> GetContentTypeFromExtension(StorageFile file, string ext)
        {
            // Image
            if (ImageCanvasViewModel.Extensions.Contains(ext))
            {
                return new ImageContentType();
            }

            // Text
            if (TextCanvasViewModel.Extensions.Contains(ext))
            {
                return new TextContentType();
            }

            // Media
            if (MediaCanvasViewModel.Extensions.Contains(ext))
            {
                return new MediaContentType();
            }

            // WebView
            if (WebViewCanvasViewModel.Extensions.Contains(ext))
            {
                if (ext == Constants.FileSystem.WEBSITE_LINK_FILE_EXTENSION)
                {
                    return new WebViewContentType(WebViewCanvasMode.ReadWebsite);
                }

                return new WebViewContentType(WebViewCanvasMode.ReadHtml);
            }

            // Markdown
            if (MarkdownCanvasViewModel.Extensions.Contains(ext))
            {
                return new MarkdownContentType();
            }

            // Default, try as text
            if (await TextCanvasViewModel.CanLoadAsText(file))
            {
                // Text
                return new TextContentType();
            }

            // Use fallback
            return new FallbackContentType();
        }

        public static async Task<BasePastedContentTypeDataModel> GetContentTypeFromDataPackage(DataPackageView dataPackage)
        {
            // Decide content type and initialize view model

            // From raw clipboard data
            if (dataPackage.Contains(StandardDataFormats.Bitmap))
            {
                // Image
                return new ImageContentType();
            }
            else if (dataPackage.Contains(StandardDataFormats.Text))
            {
                SafeWrapper<string> text = await SafeWrapperRoutines.SafeWrapAsync(() => dataPackage.GetTextAsync().AsTask());

                if (!text)
                {
                    Debugger.Break(); // What!?
                    return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
                }

                // Check if it's url
                if (StringHelpers.IsUrl(text))
                {
                    // The url may point to file
                    if (StringHelpers.IsUrlFile(text))
                    {
                        // Image
                        return new SafeWrapper<BasePastedContentTypeDataModel>(new ImageContentType(), SafeWrapperResult.S_SUCCESS);
                    }
                    else
                    {
                        // Webpage link
                        //InitializeViewModel(() => new WebViewCanvasViewModel(_view, WebViewCanvasMode.ReadWebsite, CanvasPreviewMode.InteractionAndPreview));
                        if (App.AppSettings.UserSettings.PrioritizeMarkdownOverText)
                        {
                            // Markdown
                            return new MarkdownContentType();
                        }
                        else
                        {
                            // Normal text
                            return new TextContentType();
                        }
                    }
                }
                else
                {
                    if (App.AppSettings.UserSettings.PrioritizeMarkdownOverText)
                    {
                        // Markdown
                        return new MarkdownContentType();
                    }
                    else
                    {
                        // Normal text
                        return new TextContentType();
                    }
                }
            }
            else if (dataPackage.Contains(StandardDataFormats.StorageItems)) // From clipboard storage items
            {
                IReadOnlyList<IStorageItem> items = await dataPackage.GetStorageItemsAsync();

                if (items.Count > 1)
                {
                    // TODO: More than one item, paste in Boundless Canvas
                }
                else if (items.Count == 1)
                {
                    // One item, decide view model for it
                    IStorageItem item = items.First();

                    BasePastedContentTypeDataModel contentType = await BasePastedContentTypeDataModel.GetContentType(item, null);
                    if (contentType is InvalidContentTypeDataModel)
                    {
                        return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
                    }

                    return contentType;
                }
                else
                {
                    // No items
                    return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
                }
            }

            return new InvalidContentTypeDataModel(CannotReceiveClipboardDataResult);
        }
    }
}
