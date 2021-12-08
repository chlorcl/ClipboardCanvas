﻿using ClipboardCanvas.ViewModels.UserControls;
using System.Collections.Generic;

namespace ClipboardCanvas.Models
{
    public interface ISuggestedActionsControlModel
    {
        bool ShowNoActionsLabelSuppressed { get; set; }

        void SetActions(IEnumerable<SuggestedActionsControlItemViewModel> actions);

        void AddAction(SuggestedActionsControlItemViewModel action);

        void RemoveAction(SuggestedActionsControlItemViewModel action);

        void RemoveActionAt(int index);

        void RemoveAllActions();
    }
}
