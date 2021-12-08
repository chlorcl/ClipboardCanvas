﻿using ClipboardCanvas.Enums;

namespace ClipboardCanvas.DataModels.ContentDataModels
{
    public sealed class WebViewContentType : BaseContentTypeModel
    {
        public readonly WebViewCanvasMode mode;

        public WebViewContentType(WebViewCanvasMode mode)
        {
            this.mode = mode;
        }
    }
}
