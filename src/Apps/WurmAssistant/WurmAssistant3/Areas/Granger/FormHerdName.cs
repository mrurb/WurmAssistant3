﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AldursLab.WurmAssistant3.Areas.Granger.DataLayer;
using AldursLab.WurmAssistant3.Areas.Logging;
using AldursLab.WurmAssistant3.Utils.WinForms;
using JetBrains.Annotations;

namespace AldursLab.WurmAssistant3.Areas.Granger
{
    public partial class FormHerdName : ExtendedForm
    {
        readonly GrangerContext context;
        string[] allHerdNames;
        Form mainForm;
        readonly ILogger logger;

        readonly string renamingHerd;

        public FormHerdName(
            [NotNull] GrangerContext context,
            [NotNull] Form mainForm, 
            [NotNull] ILogger logger, 
            [CanBeNull] string renamingHerd = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (mainForm == null) throw new ArgumentNullException(nameof(mainForm));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            this.mainForm = mainForm;
            this.logger = logger;
            this.renamingHerd = renamingHerd;
            this.context = context;

            InitializeComponent();

            allHerdNames = context.Herds.Select(x => x.HerdId).ToArray();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (renamingHerd != null)
            {
                try
                {
                    context.RenameHerd(renamingHerd, textBox1.Text);
                }
                catch (Exception _e)
                {
                    logger.Error(_e, "RenameHerd error");
                    MessageBox.Show("renaming herd failed, check log for details");
                }
            }
            else
            {
                try
                {
                    context.InsertHerd(textBox1.Text);
                }
                catch (Exception _e)
                {
                    logger.Error(_e, "AddHerd error");
                    MessageBox.Show("adding herd failed, check log for details");
                }
            }
        }

        bool NameIsValid
        {
            get
            {
                if (textBox1.Text.Length < 1)
                {
                    labelInfo.Text = "name can't be empty";
                    return false;
                }
                else if (textBox1.Text.Length > 20)
                {
                    labelInfo.Text = "name too long";
                    return false;
                }
                else if (!Regex.IsMatch(textBox1.Text, @"^\w+$"))
                {
                    labelInfo.Text = "must be one word made of letters and/or numbers";
                    return false;
                }
                else if (context.Herds.Any(x => x.HerdId == textBox1.Text))
                {
                    labelInfo.Text = "herd with this name already exists";
                    return false;
                }
                else
                {
                    labelInfo.Text = "";
                    return true;
                }
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            buttonOK.Enabled = NameIsValid;
        }

        private void FormHerdName_Load(object sender, EventArgs e)
        {
            textBox1.Select();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                buttonOK.PerformClick();
            }
        }
    }
}
