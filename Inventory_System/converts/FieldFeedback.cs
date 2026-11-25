using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using System.Windows.Media;
using System.Windows.Data;
 using Binding = System.Windows.Data.Binding;
using Control = System.Windows.Controls.Control;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
namespace Inventory_System.converts
{
    public static class FieldFeedback
    {
        #region خصائص التحقق وحالة الخانة
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(FieldFeedback), new PropertyMetadata(false, Enable));
        public static readonly DependencyProperty HasValueProperty = DependencyProperty.RegisterAttached("HasValue", typeof(bool), typeof(FieldFeedback), new PropertyMetadata(false));
        public static bool GetEnabled(DependencyObject field) => (bool)field.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject field, bool value) => field.SetValue(EnabledProperty, value);
        public static bool GetHasValue(DependencyObject field) => (bool)field.GetValue(HasValueProperty);
        // تحدد العلامة إطار الإدخال فقط، فلا يمتد حد الخطأ إلى بطاقة أو نافذة كاملة.
        public static readonly DependencyProperty IsFieldSurfaceProperty = DependencyProperty.RegisterAttached("IsFieldSurface", typeof(bool), typeof(FieldFeedback), new PropertyMetadata(false));
        public static bool GetIsFieldSurface(DependencyObject field) => (bool)field.GetValue(IsFieldSurfaceProperty);
        public static void SetIsFieldSurface(DependencyObject field, bool value) => field.SetValue(IsFieldSurfaceProperty, value);
        #endregion

        #region ربط القالب المشترك ومراقبة المدخلات
        private static void Enable(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            if (source is not Control field || e.NewValue is not true) return;
            // نضيف ربط تحقق محايدًا عند غياب الربط الأصلي، دون تغيير قيمة الإدخال أو قواعده.
            if (field.GetBindingExpression(FrameworkElement.TagProperty) is null)
                BindingOperations.SetBinding(field, FrameworkElement.TagProperty, new Binding("Name") { Source = field, Mode = BindingMode.OneWay });
            field.PreviewTextInput += (_, _) => Clear(field);
            field.SetResourceReference(Validation.ErrorTemplateProperty, "Validation.FieldError");
            field.Loaded += FieldLoaded;
            field.Unloaded += FieldUnloaded;
            if (field is TextBox text) text.TextChanged += (_, _) => Changed(field);
            if (field is ComboBox combo) combo.SelectionChanged += (_, _) => Changed(field);
            if (field is PasswordBox password) password.PasswordChanged += (_, _) => Changed(field);
            if (field is DatePicker date) date.SelectedDateChanged += (_, _) => Changed(field);
            if (field is BaseEdit editor) editor.EditValueChanged += (_, _) => Changed(field);
            if (field.IsLoaded) FieldLoaded(field, new RoutedEventArgs());
        }

        private static void FieldLoaded(object sender, RoutedEventArgs e)
        {
            var field = (Control)sender;
            ConfigureSurface(field);
            // يشمل بحث الجدول الكتابة واللصق والتفريغ؛ نفصل المراقب السابق عند إعادة الفتح.
            if (field is GridSearchControl)
            {
                var descriptor = DependencyPropertyDescriptor.FromProperty(GridSearchControl.SearchTextProperty, typeof(GridSearchControl));
                descriptor.RemoveValueChanged(field, SearchChanged);
                descriptor.AddValueChanged(field, SearchChanged);
            }
            Changed(field);
        }

        private static void FieldUnloaded(object sender, RoutedEventArgs e)
        {
            // لا يحتفظ مراقب الخاصية بالنافذة بعد إغلاقها.
            if (sender is GridSearchControl field)
                DependencyPropertyDescriptor.FromProperty(GridSearchControl.SearchTextProperty, typeof(GridSearchControl)).RemoveValueChanged(field, SearchChanged);
        }

        private static void SearchChanged(object? sender, EventArgs e)
        {
            if (sender is Control field) Changed(field);
        }

        private static void ConfigureSurface(Control field)
        {
            DependencyObject? parent = LogicalTreeHelper.GetParent(field) ?? VisualTreeHelper.GetParent(field);
            while (parent is not null && parent is not Window)
            {
                if (GetIsFieldSurface(parent) && parent is UIElement surface)
                {
                    if (surface is FrameworkElement element)
                        element.SetResourceReference(Validation.ErrorTemplateProperty, "Validation.FieldError");
                    Validation.SetValidationAdornerSite(field, surface);
                    return;
                }
                // لا نخرج من محرر داخلي إلى إطار محرر آخر.
                if (parent is Control && parent is not ContentControl) break;
                parent = LogicalTreeHelper.GetParent(parent) ?? VisualTreeHelper.GetParent(parent);
            }
            // المحرر المستقل، مثل كلمة المرور، يستخدم حدوده كاملة.
            Validation.SetValidationAdornerSite(field, null);
        }
        private static void Changed(Control field)
        {
            Clear(field);
            bool filled = field switch
            {
                TextBox text => !string.IsNullOrWhiteSpace(text.Text),
                ComboBox combo => combo.SelectedItem is not null,
                PasswordBox password => password.Password.Length > 0,
                DatePicker date => date.SelectedDate.HasValue,
                GridSearchControl search => !string.IsNullOrWhiteSpace(search.SearchText),
                BaseEdit editor => editor.EditValue is string value ? !string.IsNullOrWhiteSpace(value) : editor.EditValue is not null,
                _ => false
            };
            field.SetValue(HasValueProperty, filled);
        }
        #endregion

        #region إظهار الخطأ ومسحه مع حفظ البيانات وقواعد التحقق
        public static bool Reject(Control field, string message)
        {
            // نضمن القالب حتى عند استدعاء التحقق من خانة منشأة برمجيًا.
            if (!GetEnabled(field)) SetEnabled(field, true);
            if (field.GetBindingExpression(FrameworkElement.TagProperty) is null)
                BindingOperations.SetBinding(field, FrameworkElement.TagProperty, new Binding("Name") { Source = field, Mode = BindingMode.OneWay });
            ConfigureSurface(field);
            if (field.GetBindingExpression(FrameworkElement.TagProperty) is { } binding)
                Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, message, null));
            field.Focus(); return false;
        }
        public static void Clear(Control field)
        {
            if (field.GetBindingExpression(FrameworkElement.TagProperty) is { } binding) Validation.ClearInvalid(binding);
        }
        #endregion
    }

}
