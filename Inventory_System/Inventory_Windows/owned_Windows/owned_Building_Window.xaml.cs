using DevExpress.Data.Async.Helpers;
using DevExpress.Data.Browsing;
using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Native;
using DevExpress.Xpf.Grid;
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

namespace Inventory_System.Inventory_Windows.owned_Windows
{
    /// <summary>
    /// Interaction logic for servic_types.xaml
    /// </summary>
    public partial class owned_Building_Window : MetroWindow
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

            if (App._listbuilding == null)
                App._listbuilding = new ObservableCollection<T_Inventory_Location>(await context.T_Inventory_Locations.Include(k=> k.T_OwnedPremises).OrderBy(k => k.In_Location).ToListAsync());


            TxtName.Focus();
        }

        private void View_SearchStringToFilterCriteria(object sender, SearchStringToFilterCriteriaEventArgs e) =>
            ArabicGridSearch.ApplyPrefixVariants(e, "In_Location", _isPasting);
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

            var names = context.T_Inventory_Locations
                .AsNoTracking()
                .Select(x => x.In_Location)
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

            var names = context.T_Inventory_Locations.Where(k => k.In_Location_ID != ((T_Inventory_Location)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow).In_Location_ID)
                .AsNoTracking()
                .Select(x => x.In_Location)
                .ToList();

            return names.Any(x =>
                NormalizeArabic(x) == searchName);
        }

        #endregion

        #region تهيئة النافذة وربط حالة الخانات
        // يراقب البحث تغيّر النص نفسه لتزول رسالة التحقق فور الكتابة أو اللصق أو التفريغ.
        private readonly System.ComponentModel.DependencyPropertyDescriptor? buildingSearchDescriptor;

        public owned_Building_Window()
        {
            InitializeComponent();
           
        }
        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            await LoadUsersAsync();
        }
        #endregion

        #region تفاعل الخانات وإلغاء لوحة التعديل
        // يبقى التحقق عند الحفظ، ويُمسح الخطأ السابق مباشرة عند تعديل اسم المبنى.
        private void BuildingName_Changed(object? sender, EventArgs e)
        {
            FieldFeedback.Clear(TxtName);
        }

        // إغلاق اللوحة يفرغ مسودة الاسم والعنوان وأخطاءها؛ السجل الأصلي لا يُعدّل عند الإلغاء.
        private void BuildingEdit_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            if (config_Nationalty is null || TxtName_update is null || Txtadress_update is null
                || config_Nationalty.IsOpen) return;

            TxtName_update.Clear();
            Txtadress_update.Clear();
            FieldFeedback.Clear(TxtName_update);
            FieldFeedback.Clear(Txtadress_update);
        }

        private void BuildingWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !config_Nationalty.IsOpen) return;
            closeconfig_Click(sender, new RoutedEventArgs());
            e.Handled = true;
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
                Txtaddress.Focus();

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
                Txtadress_update.Focus(); 

        }
        #endregion

        #region textaddress

        private void Txtaddress_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                insert_Click(insert, new RoutedEventArgs());
        }

        private void Txtaddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            Txtaddress.Tag = 0;
        }
        #endregion

        #region text_addredd_update
        private void Txtadress_update_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                update_button_ClickAsync(update_button, new RoutedEventArgs());
        }

        private void Txtadress_update_TextChanged(object sender, TextChangedEventArgs e)
        {
            Txtadress_update.Tag = 0;
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
                    ErrorRule.ErrorMessage = "أدخل المبني";
                    TxtName.GetBindingExpression(TagProperty).UpdateSource();
                    TxtName.Focus();
                    return;
                }
                string name = TxtName.SearchText.Trim();

                if (InstitutionExists(name))
                {
                    TxtName.Tag = 1;
                    ErrorRule.ErrorMessage = "المبني موجود مسبقًا";
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
                T_Inventory_Location t = new T_Inventory_Location
                {
                    In_Location = name,
                    Branch_ID = App.location.Branch_ID,
                    Location = App.location.Location,
                    Location_ID = App.location.Location_ID,
                    Address=Txtaddress.Text,
                    types=1
                    
                ,
                };
                  

                T_OwnedPremise op = new T_OwnedPremise
                {
                    Branch_ID = App.location.Branch_ID,
                    Location = App.location.Location,
                    Location_ID = App.location.Location_ID,
                    Date_Modify=DateTime.Now.Date.ToDateOnly(),
                  In_Location_ID=t.In_Location_ID,
                  

                };
                t.T_OwnedPremises= op;
                context.T_Inventory_Locations.Add(t);

                await context.SaveChangesAsync();

                await transaction.CommitAsync();
                if (App._listbuilding == null)
                    App._listbuilding = new ObservableCollection<T_Inventory_Location>();
                App._listbuilding.Add(new T_Inventory_Location
                {
                    In_Location=t.In_Location,
                    In_Location_ID = t.In_Location_ID,
                    Branch_ID = App.location.Branch_ID,
                    Location = App.location.Location,
                    Location_ID = App.location.Location_ID,
                    T_OwnedPremises=t.T_OwnedPremises,
                });

                ((owned_building_viewmodel)this.DataContext).DataSourceRefresh(new DataSourceRefreshArgs());
                TxtName.SearchText = null;
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";
                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"تم ادراج المبني",
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
                    ErrorRule.ErrorMessage = "أدخل المبني";
                    TxtName_update.GetBindingExpression(TagProperty).UpdateSource();
                    TxtName_update.Focus();
                    return;
                }
                string name = TxtName_update.Text.Trim();

                if (InstitutionExists_update(name))
                {
                    TxtName_update.Tag = 1;
                    ErrorRule.ErrorMessage = "اسم المبني موجود مسبقًا";
                    TxtName_update.GetBindingExpression(TagProperty).UpdateSource();
                    TxtName_update.Focus();
                    return;

                }
                #endregion

                DbContextHelper.InitOptions(
              App.ConnectionString
          );

                using var context = new Cont_maintContext(DbContextHelper.Options);
                int selectedId = ((T_Inventory_Location)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow).In_Location_ID;
                await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                T_Inventory_Location t = await context.T_Inventory_Locations.SingleAsync(type => type.In_Location_ID == selectedId);
                t.In_Location = name;
                t.Address = Txtadress_update.Text;
                App._listbuilding.SingleOrDefault(k => k.In_Location_ID == t.In_Location_ID).Address = t.Address;
                App._listbuilding.SingleOrDefault(k => k.In_Location_ID == t.In_Location_ID).In_Location = t.In_Location;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                //((T_Inventory_Location)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow).In_Location = TxtName_update.Text;

                //((T_Inventory_Location)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).Content[1]).In_Location = TxtName_update.Text; 
                //TxtName_update.Clear();
                ((owned_building_viewmodel)this.DataContext).DataSourceRefresh(new DataSourceRefreshArgs());
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";
                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"تم تعديل المبني",
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
            T_Inventory_Location t = ((T_Inventory_Location)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow);
            TxtName_update.Text = t.In_Location;
            Txtadress_update.Text = t.Address;  
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
            // النص الإرشادي يصف خانة الاسم، ولا يُحسب قيمة مدخلة في تلوين الخانة.
            (sender as GridSearchControl).NullText = "أدخل اسم المبنى";
        }

        private void MetroWindow_Loaded_1(object sender, RoutedEventArgs e)
        {

        }
    }

}
