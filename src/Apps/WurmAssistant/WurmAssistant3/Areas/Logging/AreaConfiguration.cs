﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AldursLab.WurmAssistant3.Areas.Logging.Contracts;
using JetBrains.Annotations;
using Ninject;

namespace AldursLab.WurmAssistant3.Areas.Logging
{
    [UsedImplicitly]
    public class AreaConfiguration : IAreaConfiguration
    {
        public void Configure(IKernel kernel)
        {
            kernel.Bind<ILogger>().ToMethod(context =>
            {
                // create logger with category matching target type name
                var factory = context.Kernel.Get<ILoggerFactory>();
                if (context.Request.Target != null)
                {
                    var type = context.Request.Target.Member.DeclaringType;
                    return factory.Create(type != null ? type.FullName : string.Empty);
                }
                else
                {
                    return factory.Create("");
                }
            });
        }
    }
}
