﻿using System.IO;
using Windows.Storage;
using Microsoft.AppCenter.Analytics;

using ClipboardCanvas.Models.JsonSettings;
using ClipboardCanvas.DataModels;

namespace ClipboardCanvas.Services
{
    public class UserSettingsService : BaseJsonSettingsModel, IUserSettingsService
    {
        private IApplicationService _applicationService;

        #region Constructor

        public UserSettingsService(IApplicationService applicationService) 
            : base(Path.Combine(ApplicationData.Current.LocalFolder.Path, Constants.LocalSettings.SETTINGS_FOLDERNAME, Constants.LocalSettings.USER_SETTINGS_FILENAME),
                  isCachingEnabled: true)
        {
            this._applicationService = applicationService;

            TrackAppCenterAnalytics();
        }

        #endregion

        #region Private Helpers

        private void TrackAppCenterAnalytics()
        {
            Analytics.TrackEvent($"{nameof(PushErrorNotification)} {PushErrorNotification}");
            Analytics.TrackEvent($"{nameof(ShowTimelineOnHomepage)} {ShowTimelineOnHomepage}");
            Analytics.TrackEvent($"{nameof(DeletePermanentlyAsDefault)} {DeletePermanentlyAsDefault}");
            Analytics.TrackEvent($"{nameof(OpenNewCanvasOnPaste)} {OpenNewCanvasOnPaste}");
            Analytics.TrackEvent($"{nameof(AlwaysPasteFilesAsReference)} {AlwaysPasteFilesAsReference}");
            Analytics.TrackEvent($"{nameof(PrioritizeMarkdownOverText)} {PrioritizeMarkdownOverText}");
            Analytics.TrackEvent($"{nameof(ShowDeleteConfirmationDialog)} {ShowDeleteConfirmationDialog}");
            Analytics.TrackEvent($"{nameof(UseInfiniteCanvasAsDefault)} {UseInfiniteCanvasAsDefault}");
            Analytics.TrackEvent($"{nameof(IsAutopasteEnabled)} {IsAutopasteEnabled}");
            Analytics.TrackEvent($"{nameof(PushAutopasteNotification)} {PushAutopasteNotification}");
            Analytics.TrackEvent($"{nameof(PushAutopasteFailedNotification)} {PushAutopasteFailedNotification}");
        }

        #endregion

        #region IUserSettings

        public AppLanguageModel AppLanguage
        {
            get => Get<AppLanguageModel>(new AppLanguageModel(null));
            set => Set(value);
        }


        public bool PushErrorNotification
        {
            get => Get<bool>(true);
            set => Set<bool>(value);
        }

        public bool ShowTimelineOnHomepage
        {
            get => Get<bool>(true);
            set => Set<bool>(value);
        }

        public bool DeletePermanentlyAsDefault
        {
            get => Get<bool>(false);
            set => Set<bool>(value);
        }

        public bool OpenNewCanvasOnPaste
        {
            get => Get<bool>(false);
            set => Set<bool>(value);
        }

        public bool AlwaysPasteFilesAsReference
        {
            get => _applicationService.IsInRestrictedAccessMode ? false : Get<bool>(true);
            set => Set<bool>(value);
        }

        public bool PrioritizeMarkdownOverText
        {
            get => Get<bool>(false);
            set => Set<bool>(value);
        }

        public bool ShowDeleteConfirmationDialog
        {
            get => Get<bool>(true);
            set => Set<bool>(value);
        }

        public bool UseInfiniteCanvasAsDefault
        {
            get => Get<bool>(true);
            set => Set<bool>(value);
        }

        public bool IsAutopasteEnabled
        {
            get => Get(false);
            set => Set(value);
        }

        public bool PushAutopasteNotification
        {
            get => Get(true);
            set => Set(value);
        }

        public bool PushAutopasteFailedNotification
        {
            get => Get(true);
            set => Set(value);
        }

        #endregion
    }
}
