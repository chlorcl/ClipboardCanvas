﻿using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

using ClipboardCanvas.DataModels;
using ClipboardCanvas.Helpers.SafetyHelpers;
using ClipboardCanvas.CanvasFileReceivers;
using ClipboardCanvas.Helpers.Filesystem;
using ClipboardCanvas.Contexts.Operations;
using ClipboardCanvas.Helpers;

namespace ClipboardCanvas.CanavsPasteModels
{
    public class TextPasteModel : BasePasteModel
    {
        public string Text { get; private set; }

        public TextPasteModel(ICanvasItemReceiverModel canvasFileReceiver, IOperationContextReceiver operationContextReceiver)
            : base(canvasFileReceiver, operationContextReceiver)
        {
        }

        protected async override Task<SafeWrapper<CanvasItem>> GetCanvasFileFromExtension(ICanvasItemReceiverModel canvasFileReceiver)
        {
            return await canvasFileReceiver.CreateNewCanvasItemFromExtension(".txt");
        }

        protected override async Task<SafeWrapperResult> SaveDataToFile()
        {
            return await FilesystemOperations.WriteFileText(await sourceFile, Text);
        }

        protected async override Task<SafeWrapperResult> SetDataFromDataPackage(DataPackageView dataPackage)
        {
            SafeWrapper<string> text = await dataPackage.SafeGetTextAsync();

            Text = text;

            return text;
        }

        public async override Task<SafeWrapperResult> SetDataFromExistingItem(IStorageItem item)
        {
            if (item is not StorageFile file)
            {
                return ItemIsNotAFileResult;
            }

            SafeWrapper<string> text = await FilesystemOperations.ReadFileText(file);

            this.Text = text;

            return text;
        }
    }
}
