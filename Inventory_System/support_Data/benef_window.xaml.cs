using DevExpress.Data.Async.Helpers;
using DevExpress.Data.Browsing;
using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Native;
using DevExpress.Xpf.Grid;
using Inventory_System;
using Inventory_System.converts;
using Inventory_System.DatabContext;
using Inventory_System.Models;
using MahApps.Metro.Controls;
using Inventory_System.converts;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.viewo_Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using TextBox = System.Windows.Controls.TextBox;

namespace Inventory_System.support_Data
{
    /// <summary>
    /// Interaction logic for servic_types.xaml
    /// </summary>
    public partial class benef_window : MetroWindow
    {
        private bool _isPasting;
        private CancellationTokenSource _searchCancellation;
        #region "All Functions"
        private async Task LoadUsersAsync()
        {
            DbContextHelper.InitOptions(
       App.ConnectionString
   );

            using var context = new Cont_maintContext(DbContextHelper.Options);

            if (App._listbenef == null)
                App._listbenef = new ObservableCollection<T_Benef>(await context.T_Benefs.OrderBy(k => k.Benef).ToListAsync());
           
          
            TxtName.Focus();
        }

        private void View_SearchStringToFilterCriteria(object sender, SearchStringToFilterCriteriaEventArgs e) =>
            ArabicGridSearch.ApplyPrefixVariants(e, "Benef", _isPasting);
        private string NormalizeArabic(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text
                .Replace("أ", "ا")
                .Replace("إ", "ا")
                .Replace("آ", "ا")
                .Replace("ٱ", "ا")

                .Replace("ى", "ي")
                .Replace("ئ", "ي")

                .Replace("ؤ", "و")

                .Replace("ة", "ه")

                .Replace("ـ", "")

                .Replace("َ", "")
                .Replace("ُ", "")
                .Replace("ِ", "")
                .Replace("ً", "")
                .Replace("ٌ", "")
                .Replace("ٍ", "")
                .Replace("ْ", "")
                .Replace("ّ", "");
        }

        public bool InstitutionExists(string name)
        {
            string searchName = NormalizeArabic(name);

            if (string.IsNullOrEmpty(searchName))
                return false;

            DbContextHelper.InitOptions(
                App.ConnectionString
            );

            using var context = new Cont_maintContext(DbContextHelper.Options);

            var names = context.T_Benefs
                .AsNoTracking()
                .Select(x => x.Benef)
                .ToList();

            return names.Any(x =>
                NormalizeArabic(x) == searchName);
        }
        public bool InstitutionExists_update(string name)
        {
            string searchName = NormalizeArabic(name);

            if (string.IsNullOrEmpty(searchName))
                return false;

            DbContextHelper.InitOptions(
                App.ConnectionString
            );

            using var context = new Cont_maintContext(DbContextHelper.Options);

            var names = context.T_Benefs.Where(k => k.Benef_ID != ((T_Benef)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow).Benef_ID)
                .AsNoTracking()
                .Select(x => x.Benef)
                .ToList();

            return names.Any(x =>
                NormalizeArabic(x) == searchName);
        }

        #endregion

        #region " Windows Behavior"
        public benef_window()
        {
            InitializeComponent();
        }
        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            await LoadUsersAsync();
         
        }
        #endregion



        #region " Controls Behavior"
        #region TxtName

        private void TxtName_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TxtName.Tag = 0;
        }
        private void TxtName_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                insert_Click(insert, new RoutedEventArgs());

        }
        #endregion

        #region Class code
        private void ClassCode_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (sender == TxtName_update)
                update_button_ClickAsync(update_button, new RoutedEventArgs());
            else
                insert_Click(insert, new RoutedEventArgs());
        }

        private void ClassCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            var input = (TextBox)sender;
            input.Tag = 0;
            input.GetBindingExpression(TagProperty)?.UpdateSource();
        }



        private static void ShowClassCodeError(TextBox input, string message)
        {
            input.Tag = 1;
            ErrorRule.ErrorMessage = message;
            input.GetBindingExpression(TagProperty)?.UpdateSource();
            input.Focus();
        }


        #endregion


        #region TxtName_update
        private void TxtName_update_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtName_update.Tag = 0;
        }
        private void TxtName_update_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                update_button_ClickAsync(update_button, new RoutedEventArgs());

        }
        #endregion

        #endregion








        #region All Main Button
        #region insert
        private async void insert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtName.Tag = 0;

                #region قيود ادخال
                if (string.IsNullOrWhiteSpace(TxtName.SearchText))
                {
                    TxtName.Tag = 1;
                    ErrorRule.ErrorMessage = "أدخل اسم المستفيد";
                    TxtName.GetBindingExpression(TagProperty).UpdateSource();
                    TxtName.Focus();
                    return;
                }
                string name = TxtName.SearchText.Trim();

                if (InstitutionExists(name))
                {
                    TxtName.Tag = 1;
                    ErrorRule.ErrorMessage = "اسم المستفيد موجود مسبقًا";
                    TxtName.GetBindingExpression(TagProperty).UpdateSource();
                    TxtName.Focus();
                    return;

                }


                #endregion

                DbContextHelper.InitOptions(
              App.ConnectionString
          );

                using var context = new Cont_maintContext(DbContextHelper.Options);
                // Keep the availability check and save atomic across simultaneous users.
                await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                T_Benef t = new T_Benef
                {
                    Benef = name,
                    Branch_ID = App.location.Branch_ID,
                    Location = App.location.Location,
                    Location_ID = App.location.Location_ID
                ,
                    Date_Modify = DateTime.Now.Date.ToDateOnly()
                };
                context.T_Benefs.Add(t);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                if(App._listbenef == null)
                    App._listbenef = new ObservableCollection<T_Benef>();  
                App._listbenef.Add(new T_Benef
                {
                    Benef = t.Benef,
                    Branch_ID = t.Branch_ID,
                    Date_Modify = DateOnly.FromDateTime(DateTime.Now),
                    Benef_ID = t.Benef_ID,
                    Location = t.Location,
                    Location_ID = t.Location_ID
                });

                ((benef_viewomodel)this.DataContext).DataSourceRefresh(new DataSourceRefreshArgs());
                TxtName.SearchText = null;
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";
                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"تم ادراج بيانات المستفيد",
               messageBoxButtons: MessageBoxButton.OK,
               defaultButton: MessageBoxResult.OK,
               icon: MessageBoxImage.Information,
               true);
                ApplicationThemeHelper.ApplicationThemeName = "None";
                TxtName.Focus();


            }
            catch (Exception ex)
            {
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";
                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"خطأ {ex.Message}",
               messageBoxButtons: MessageBoxButton.OK,
               defaultButton: MessageBoxResult.OK,
               icon: MessageBoxImage.Error,
               true);
                ApplicationThemeHelper.ApplicationThemeName = "None";
            }
        }
        #endregion

        #region update
        private async void update_button_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtName_update.Tag = 0;
                #region قيود ادخال
                if (string.IsNullOrWhiteSpace(TxtName_update.Text))
                {
                    TxtName_update.Tag = 1;
                    ErrorRule.ErrorMessage = "أدخل اسم المستفيد";
                    TxtName_update.GetBindingExpression(TagProperty).UpdateSource();
                    TxtName_update.Focus();
                    return;
                }
                string name = TxtName_update.Text.Trim();

                if (InstitutionExists_update(name))
                {
                    TxtName_update.Tag = 1;
                    ErrorRule.ErrorMessage = "اسم المستفيد موجود مسبقًا";
                    TxtName_update.GetBindingExpression(TagProperty).UpdateSource();
                    TxtName_update.Focus();
                    return;

                }
                #endregion

                DbContextHelper.InitOptions(
              App.ConnectionString
          );

                using var context = new Cont_maintContext(DbContextHelper.Options);
                int selectedId = ((T_Benef)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow).Benef_ID;
                await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                T_Benef t = await context.T_Benefs.SingleAsync(type => type.Benef_ID == selectedId);
                t.Benef = name;

                App._listbenef.SingleOrDefault(k => k.Benef_ID == t.Benef_ID).Benef = t.Benef;
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                //((T_Benef)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow).Benef = TxtName_update.Text;

                //((T_Benef)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).Content[1]).Benef = TxtName_update.Text; 
                //TxtName_update.Clear();
                ((benef_viewomodel)this.DataContext).DataSourceRefresh(new DataSourceRefreshArgs());
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";
                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"تم تعديل بيانات المستفيد",
               messageBoxButtons: MessageBoxButton.OK,
               defaultButton: MessageBoxResult.OK,
               icon: MessageBoxImage.Information,
               true);
                ApplicationThemeHelper.ApplicationThemeName = "None";
                TxtName.Focus();
                TxtName_update.Clear();

                TxtName_update.Tag = 0;
                config_Nationalty.IsOpen = false;


            }
            catch (Exception ex)
            {
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";
                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"خطأ {ex.Message}",
               messageBoxButtons: MessageBoxButton.OK,
               defaultButton: MessageBoxResult.OK,
               icon: MessageBoxImage.Error,
               true);

                ApplicationThemeHelper.ApplicationThemeName = "None";
                config_Nationalty.IsOpen = false;

            }

        }
        #endregion
        #region close Window
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion

        #region close update
        private void closeconfig_Click(object sender, RoutedEventArgs e)
        {
            TxtName_update.Clear();

            TxtName_update.Tag = 0;
            config_Nationalty.IsOpen = false;
        }
        #endregion
        #endregion







        #region GridControl
        private void Benef_List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Benef_List.SelectedItem == null)
                return;
            T_Benef t = ((T_Benef)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow);
            TxtName_update.Text = t.Benef;
            TxtName_update.Focus();
            config_Nationalty.IsOpen = true;

        }


        #endregion

        private void TxtName_Pasting(object sender, DataObjectPastingEventArgs e)
        {


            _isPasting = true;
            Dispatcher.BeginInvoke(
        new Action(() =>
        {
            _isPasting = false;
        }),
        DispatcherPriority.Background);

        }

        private void TxtName_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as GridSearchControl).NullText = "أدخل بيانات المستفيد";
        }

        
    }

}
