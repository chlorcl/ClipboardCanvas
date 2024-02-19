﻿using ClipboardCanvas.WinUI.Imaging;
using Microsoft.UI.Xaml.Data;
using System;

namespace ClipboardCanvas.WinUI.ValueConverters
{
    internal sealed class ImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is IconImage iconImage)
                return iconImage.IconGlyph;

            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}