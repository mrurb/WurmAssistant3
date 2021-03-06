﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AldursLab.WurmApi;
using AldursLab.WurmAssistant3.Areas.Config;
using AldursLab.WurmAssistant3.Areas.Core;
using AldursLab.WurmAssistant3.Areas.Logging;
using Ninject;

namespace AldursLab.WurmAssistant3.Areas.WurmApi
{
    public class WurmApiAreaConfig : AreaConfig
    {
        public override void Configure(IKernel kernel)
        {
            kernel.Bind<IWurmApi>().ToMethod(BuildWurmApiFactory(kernel)).InSingletonScope();
        }

        private static Func<Ninject.Activation.IContext, IWurmApi> BuildWurmApiFactory(IKernel kernel)
        {
            return context =>
            {
                IWurmAssistantConfig config = kernel.Get<IWurmAssistantConfig>();
                IWurmApiLoggerFactory loggerFactory = kernel.Get<IWurmApiLoggerFactory>();
                IWurmApiEventMarshaller eventMarshaller = kernel.Get<IWurmApiEventMarshaller>();
                IWurmAssistantDataDirectory wurmAssistantDataDirectory = kernel.Get<IWurmAssistantDataDirectory>();

                if (string.IsNullOrWhiteSpace(config.WurmGameClientInstallDirectory))
                {
                    throw new InvalidOperationException("Unknown path to Wurm Game Client installation folder.");
                }

                IWurmClientInstallDirectory wurmInstallDirectory =
                    new WurmInstallDirectoryOverride(config.WurmGameClientInstallDirectory);
                ServerInfoManager serverInfoManager = kernel.Get<ServerInfoManager>();

                var wurmApiConfig = new WurmApiConfig
                {
                    Platform = Platform.Windows,
                    ClearAllCaches = config.DropAllWurmApiCachesToggle,
                    WurmUnlimitedMode = config.WurmUnlimitedMode
                };
                serverInfoManager.UpdateWurmApiConfigDictionary(wurmApiConfig.ServerInfoMap);

                var wurmApiDataDir =
                    new DirectoryInfo(Path.Combine(wurmAssistantDataDirectory.DirectoryPath, "WurmApi"));

                var wurmApi = AldursLab.WurmApi.WurmApiFactory.Create(
                    new WurmApiCreationOptions()
                    {
                        DataDirPath = wurmApiDataDir.FullName,
                        WurmApiLogger = loggerFactory.Create(),
                        WurmApiEventMarshaller = eventMarshaller,
                        WurmClientInstallDirectory = wurmInstallDirectory,
                        WurmApiConfig = wurmApiConfig
                    });

                config.DropAllWurmApiCachesToggle = false;

                var validator = new WurmClientValidator(wurmApi, config);
                if (!validator.SkipOnStart)
                {
                    var issues = validator.Validate();
                    if (issues.Any()) validator.ShowSummaryWindow(issues);
                }

                return wurmApi;
            };
        }
    }
}
