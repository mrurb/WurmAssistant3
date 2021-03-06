﻿using System;
using AldursLab.WurmAssistant3.Areas.Config;
using AldursLab.WurmAssistant3.Areas.Logging;
using JetBrains.Annotations;

namespace AldursLab.WurmAssistant3.Areas.TrayPopups
{
    /// <summary>
    /// A simple system for showing popup windows in bottom-right-most side of main screen. 
    /// </summary>
    /// <remarks>
    /// Adapted from library: http://www.codeproject.com/Articles/277584/Notification-Window
    /// This wrapper runs NotificationWindow in another thread to minimize the likelyhood,
    /// that popup will steal user focus, however it still sometimes happens and the cause is unknown.
    /// </remarks>
    [KernelBind(BindingHint.Singleton)]
    class TrayPopups : ITrayPopups
    {
        readonly ILogger logger;
        readonly PopupManager manager;

        public TrayPopups([NotNull] ILogger logger, [NotNull] IWurmAssistantConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            manager = new PopupManager(logger, config);
        }

        /// <summary>
        /// Add message to Popup queue
        /// </summary>
        /// <param name="content">content of the message</param>
        /// <param name="title">title of the message</param>
        /// <param name="timeToShowMillis">how long should this popup be visible, in milliseconds</param>
        public void Schedule(string content, string title, int timeToShowMillis = 3000)
        {
            manager.ScheduleCustomPopupNotify(content, title, timeToShowMillis);
        }

        /// <summary>
        /// Add message to Popup queue with default title
        /// </summary>
        /// <param name="content">content of the message</param>
        /// <param name="timeToShowMillis">how long should this popup be visible, in milliseconds</param>
        public void Schedule(string content, int timeToShowMillis = 3000)
        {
            Schedule(content, null, timeToShowMillis);
        }
    }
}
