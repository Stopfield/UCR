﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UCR.Annotations;
using UCR.Core;
using UCR.Utilities;
using UCR.ViewModels;
using UCR.Views.Device;
using UCR.Views.Profile;

namespace UCR.Views
{

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Context context;

        public string ActiveProfileBreadCrumbs => context?.ActiveProfile != null ? context.ActiveProfile.ProfileBreadCrumbs() : "None";

        public MainWindow()
        {
            DataContext = this;
            new ResourceLoader().Load();
            InitializeComponent();
            context = Context.Load();

            context.SetActiveProfileCallback(ActiveProfileChanged);
            ReloadProfileTree();
        }

        private bool GetSelectedItem(out ProfileItem profileItem)
        {
            var pi = ProfileTree.SelectedItem as ProfileItem;
            if (pi == null)
            {
                MessageBox.Show("Please select a profile", "No profile selected!",MessageBoxButton.OK, MessageBoxImage.Exclamation);
                profileItem = null;
                return false;
            }
            profileItem = pi;
            return true;
        }

        private void ReloadProfileTree()
        {
            var profileTree = ProfileItem.GetProfileTree(context.Profiles);
            ProfileTree.ItemsSource = profileTree;
        }


        #region Profile Actions

        private void ActivateProfile(object sender, RoutedEventArgs e)
        {
            var a = sender as ProfileItem;
            ProfileItem pi;
            if (!GetSelectedItem(out pi)) return;
            if (!context.ProfilesManager.ActivateProfile(pi.profile))
            {
                MessageBox.Show("The profile could not be activated, see the log for more details", "Profile failed to activate!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void DeactivateProfile(object sender, RoutedEventArgs e)
        {
            if (context.ActiveProfile == null) return;
            
            if (!context.ProfilesManager.DeactivateProfile(context.ActiveProfile))
            {
                MessageBox.Show("The active profile could not be deactivated, see the log for more details", "Profile failed to deactivate!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void AddProfile(object sender, RoutedEventArgs e)
        {
            var w = new TextDialog("Profile name");
            w.ShowDialog();
            if (!w.DialogResult.HasValue || !w.DialogResult.Value) return;
            context.ProfilesManager.AddProfile(w.TextResult);
            ReloadProfileTree();
        }

        private void AddChildProfile(object sender, RoutedEventArgs e)
        {
            ProfileItem pi;
            if (!GetSelectedItem(out pi)) return;
            var w = new TextDialog("Profile name");
            w.ShowDialog();
            if (!w.DialogResult.HasValue || !w.DialogResult.Value) return;
            pi.profile.AddNewChildProfile(w.TextResult);
            ReloadProfileTree();
        }

        private void EditProfile(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem)
            {
                if (!((TreeViewItem)sender).IsSelected)
                {
                    return;
                }
            }
            ProfileItem pi;
            if (!GetSelectedItem(out pi)) return;
            var win = new ProfileWindow(context, pi.profile);
            Action showAction = () => win.Show();
            Dispatcher.BeginInvoke(showAction);
        }

        private void RenameProfile(object sender, RoutedEventArgs e)
        {
            ProfileItem pi;
            if (!GetSelectedItem(out pi)) return;
            var w = new TextDialog("Rename profile", pi.profile.Title);
            w.ShowDialog();
            if (!w.DialogResult.HasValue || !w.DialogResult.Value) return;
            pi.profile.Rename(w.TextResult);
            ReloadProfileTree();
        }

        private void CopyProfile(object sender, RoutedEventArgs e)
        {
            // TODO: Implement
            MessageBox.Show("Not yet implemented", "We're sorry...", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return;

            ProfileItem pi;
            if (!GetSelectedItem(out pi)) return;
            MessageBoxResult result = MessageBox.Show("Are you sure you want to delete '" + pi.profile.Title + "'?", "Remove profile", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                pi.profile.Remove();
                ReloadProfileTree();
            }
        }

        private void RemoveProfile(object sender, RoutedEventArgs e)
        {
            ProfileItem pi;
            if (!GetSelectedItem(out pi)) return;
            var result = MessageBox.Show("Are you sure you want to remove '" + pi.profile.Title +"'?", "Remove profile", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            pi.profile.Remove();
            ReloadProfileTree();
        }

        #endregion Profile Actions

        private void ManageDeviceLists_OnClick(object sender, RoutedEventArgs e)
        {
            var win = new DeviceListWindow(context);
            Action showAction = () => win.Show();
            Dispatcher.BeginInvoke(showAction);
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (context.IsNotSaved)
            {
                var result = MessageBox.Show("Do you want to save before closing?", "Unsaved data", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                switch (result)
                {
                    case MessageBoxResult.None:
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        return;
                    case MessageBoxResult.Yes:
                        context.SaveContext();
                        break;
                    case MessageBoxResult.No:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            context.Dispose();
        }

        private void ActiveProfileChanged()
        {
            OnPropertyChanged(nameof(ActiveProfileBreadCrumbs));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Save_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            context.SaveContext();
        }

        private void Save_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = context.IsNotSaved;
        }
    }
}