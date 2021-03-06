﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AldursLab.WurmAssistant3.Areas.Main.ViewModels;
using AldursLab.WurmAssistant3.Utils.Extensions;

namespace AldursLab.WurmAssistant3.Areas.Main.Views
{
    /// <summary>
    /// Interaction logic for NewsView.xaml
    /// </summary>
    public partial class NewsView : Window
    {
        public NewsView()
        {
            InitializeComponent();

            Icon = Properties.Resources.WurmAssistantIcon.ToImageSource();
        }
    }
}
