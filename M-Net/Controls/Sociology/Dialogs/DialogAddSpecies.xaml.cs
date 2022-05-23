﻿using System;
using System.Windows;
using System.Windows.Controls;

namespace M.Controls.Sociology.Dialogs
{
    /// <summary>
    /// Interaction logic for DialogAddSpecies.xaml
    /// </summary>
    public partial class DialogAddSpecies : UserControl
    {
        public event Action Add;
        public event Action Close;

        public string LocalPath = "";

        public DialogAddSpecies()
        {
            InitializeComponent();
        }

        private void ButtonCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close?.Invoke();
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
        {
            Add?.Invoke();
        }

        private void ButtonLocalPath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Warp Species|*.species",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                LocalPath = Dialog.FileName;

                string LocalPathShort = LocalPath;
                if (LocalPathShort.Length > 50)
                    LocalPathShort = LocalPath.Substring(0, 22) + "..." + LocalPath.Substring(LocalPath.Length - 26, 25);

                ButtonLocalPath.Content = LocalPathShort;
            }
        }
    }
}
