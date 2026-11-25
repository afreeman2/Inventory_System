using DevExpress.XtraReports.UI;
using Inventory_System.Models;
using Inventory_System.viewo_Model;
using System.IO;
using System.Xml.Linq;

namespace Inventory_System.reports;

#region صف التقرير المستقل عن اتصال قاعدة البيانات
// لكل رقم صف مستقل؛ لا نمرر DbContext أو مجموعة خطوط داخل صف مستند واحد إلى محرك التقارير.
public sealed record PhoneLineReportRow(PhoneLineRow Line, string PhoneLocation, string Branch,
    string Department, string Unit, string DepartmentHead);
#endregion

public static class PhoneLineReport
{
    #region تحميل قالب المستخدم وربط حقوله بالصفوف المقروءة من EF Core
    public static XtraReport Create(IReadOnlyCollection<PhoneLineRow> rows, string title, bool grouped, string branch)
    {
        // يبقى القالب الأصلي محفوظًا؛ نحول مساراته في نسخة الذاكرة فقط إلى صفوف التقرير المسطحة.
        var path = Path.Combine(AppContext.BaseDirectory, "Reports", grouped ? "phone_line_group.repx" : "phone_line.repx");
        var layout = XDocument.Load(path);
        var root = layout.Root ?? throw new InvalidDataException("ملف قالب تقرير الهواتف غير صالح.");
        // إزالة مصدر المصمم قبل التحميل تمنع محاولة إنشاء DbContext بمعامل options فارغ.
        root.Element("ComponentStorage")?.Remove();
        root.Attribute("DataSource")?.Remove();
        root.SetAttributeValue("DataMember", string.Empty);
        var fields = new Dictionary<string, string>
        {
            ["[T_Sub_PHones_Buildings].[locationphone]"] = "[PhoneLocation]",
            ["[T_Sub_PHones_Buildings].[T_Section].[Secti]"] = "[Line.Section]",
            ["[T_Sub_PHones_Buildings].[nameuser]"] = "[Line.UserName]",
            ["[T_Sub_PHones_Buildings].[Phone]"] = "[Line.Phone]",
            ["[T_Sub_PHones_Buildings].[T_Manage].[Manag]"] = "[Line.Management]",
            ["[T_Inventory_Location].[In_Location]"] = "[Line.Building]",
            ["[T_Inventory_Location].[T_Location].[Branch].[Branch]"] = "[Branch]",
            ["[mangordep]"] = "[Department]",
            ["[deporunit]"] = "[Unit]",
            ["[depuserdrop]"] = "[DepartmentHead]"
        };
        foreach (var expression in root.Descendants().Attributes("Expression"))
            if (fields.TryGetValue(expression.Value, out var field)) expression.Value = field;
        foreach (var field in root.Descendants().Attributes("FieldName"))
            if (field.Value == "T_Sub_PHones_Buildings.T_Manage.Manag_ID") field.Value = "Line.ManagementId";

        var report = new XtraReport();
        try
        {
            using var stream = new MemoryStream();
            layout.Save(stream); stream.Position = 0;
            report.LoadLayoutFromXml(stream);
            // الأسماء الإدارية تأتي من قواعد الفرع المعتمدة، والأرقام تبقى نصوصًا لحفظ الأصفار الأولى.
            report.DataSource = rows.Select(row =>
            {
                var document = new T_Phones_Building { Branch_ID = row.Key.Scope.BranchId };
                return new PhoneLineReportRow(row,
                    string.Join(" - ", new[] { row.Management, row.Section }.Where(x => !string.IsNullOrWhiteSpace(x))),
                    branch, document.mangordep, document.deporunit, document.depuserdrop);
            }).ToList();
            report.DataMember = string.Empty;
            report.DisplayName = title;
            report.RequestParameters = false;
            // عنوان الكشف ثابت في تصميم المستخدم؛ عنوان التقرير المجمع يأتي من خانة عنوان التقرير.
            if (grouped && report.FindControl("xrLabel10", true) is XRLabel heading)
            { heading.ExpressionBindings.Clear(); heading.Text = title; }
            if (grouped && report.FindControl("xrLabel4", true) is XRLabel caption)
                caption.Text = "الإدارة أو القسم:";
            return report;
        }
        catch { report.Dispose(); throw; }
    }
    #endregion
}
