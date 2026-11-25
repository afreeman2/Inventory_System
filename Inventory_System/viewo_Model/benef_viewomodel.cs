using DevExpress.Data.Linq;



using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Core;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using System.Collections;

using Microsoft.EntityFrameworkCore;
using Inventory_System.converts;
using System.Windows;

namespace Inventory_System.viewo_Model
{
    public class benef_viewomodel : ViewModelBase
    {
        EntityInstantFeedbackSource _ItemsSource;
        Cont_maintContext _Context;
        IList _listing;
        public EntityInstantFeedbackSource ItemsSource
        {
            get
            {
                if (_ItemsSource == null)
                {
                    _ItemsSource = new EntityInstantFeedbackSource
                    {
                        KeyExpression = nameof(T_Benef.Benef_ID)
                    };
                    DbContextHelper.InitOptions(
                     App.ConnectionString
                 );

                    _ItemsSource.GetQueryable += (sender, e) => {
                        var context = new Cont_maintContext(DbContextHelper.Options);
                        e.QueryableSource = context.T_Benefs.OrderBy(k => k.Benef).AsNoTracking();
                    };
                }
                return _ItemsSource;
            }
        }

        public IList listing
        {
            get
            {
                if (_listing == null && !DevExpress.Mvvm.ViewModelBase.IsInDesignMode)
                {
                    DbContextHelper.InitOptions(
                 App.ConnectionString
             );
                    var context = new Cont_maintContext(DbContextHelper.Options);
                    _listing = context.T_Benefs.Select(item => new { Benef_ID = item.Benef_ID, Build = item.Benef, Location_ID = item.Location_ID, Location = item.Location }).ToArray();

                }
                return _listing;
            }
        }

        [Command]
        public void DataSourceRefresh(DataSourceRefreshArgs args)
        {
            //MessageBox.Show("ValidateRowDeletion تم استدعاؤها");
            _listing = null;
            ItemsSource.Refresh();
        }

        [Command]
        public void ValidateRow(EditFormRowValidationArgs args)
        {
            var context = ((EditIssueInfo)args.EditOperationContext).DbContext;
            context.SaveChanges();
        }


        [Command]
        public void ValidateRowDeletion(EditFormValidateRowDeletionArgs args)
        {
            var key = Convert.ToInt16(args.Keys.Single());
            DbContextHelper.InitOptions(
                App.ConnectionString
            );
            using var context = new Cont_maintContext(DbContextHelper.Options);
            // التحقق من ارتباط المبني بالمستندات
            // منع الحذف إذا كان المقر مرتبطًا بمستندات أو تأمين أو فواتير أو عقود.
            bool exists =
                context.T_OwnedPremises_Contrects.Any(x => x.Benef_ID == key);
            if (exists)
            {
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";

                ThemedMessageBox.Show(
                    null,
                    title: "منظومة الحصر",
                    text: "لا يمكن حذف المستفيد، لأنه مرتبط ببيانات أخرى.",
                    messageBoxButtons: MessageBoxButton.OK,
                    defaultButton: MessageBoxResult.OK,
                    icon: MessageBoxImage.Error,
                    true);

                ApplicationThemeHelper.ApplicationThemeName = "None";

                return;
            }
            // تأكيد الحذف
            ApplicationThemeHelper.ApplicationThemeName = "Win11Light";

            var result = ThemedMessageBox.Show(
                null,
                title: "منظومة الحصر",
                text: "هل أنت متأكد من حذف المستفيد؟",
                messageBoxButtons: MessageBoxButton.YesNo,
                defaultButton: MessageBoxResult.No,
                icon: MessageBoxImage.Question,
                true);

            if (result != MessageBoxResult.Yes)
            {
                ApplicationThemeHelper.ApplicationThemeName = "None";
                return;
            }
            // تنفيذ الحذف
            var item = new T_Benef
            {
                Benef_ID = key
                ,
                Location = App.location.Location,
                Branch_ID = App.location.Branch_ID,
                Location_ID = App.location.Location_ID
            };

            context.Entry(item).State = EntityState.Deleted;

            context.SaveChanges();
            App._listbenef.Remove(App._listbenef.SingleOrDefault(k=> k.Benef_ID == key));   
            ThemedMessageBox.Show(
                null,
                title: "منظومة الحصر",
                text: "تم حذف المستفيد بنجاح.",
                messageBoxButtons: MessageBoxButton.OK,
                defaultButton: MessageBoxResult.OK,
                icon: MessageBoxImage.Information,
                true);
            ApplicationThemeHelper.ApplicationThemeName = "None";

        }

    }

}
