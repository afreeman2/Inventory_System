using Inventory_System.converts;
using Inventory_System.Dashboard;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.Premises;
using Inventory_System.support_Data;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Control = System.Windows.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Inventory_System.Archive;

public partial class ArchiveWindow : MetroWindow
{
    #region حالة النطاق والتحديد والمسودة المستقلة
    private readonly int type;
    private readonly ServiceScope scope;
    private readonly Func<Cont_maintContext> contextFactory;
    private List<ArchiveCard> buildings = [], albums = [], documents = [];
    private ArchiveCard? building, album, editedAlbum, editedDocument;
    private byte[]? replacement;
    private MemoryStream? previewStream;
    private bool busy, closed, viewing, clearing;
    private int draftVersion;
    private readonly DispatcherTimer clickTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private ArchiveCard? pendingPreview;
    private ArchiveCard? pendingAlbum;
    // نعتمد مهلة النقر المزدوج المضبوطة في Windows حتى لا يبدأ تحميل مفرد يعطل النقرة الثانية.
    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    public ArchiveWindow(int premisesType)
    {
        if (premisesType is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(premisesType));
        type = premisesType;
        var location = App.location ?? throw new InvalidOperationException("اختر موقع الدخول أولًا.");
        scope = new(location.Branch_ID, location.Location_ID, location.Location);
        contextFactory = () => new Cont_maintContext(DbContextHelper.Options ?? throw new InvalidOperationException("الاتصال غير مهيأ."));
        DevExpress.Xpf.PdfViewer.PdfViewerLocalizer.Active = new ArabicPdfViewerLocalizer();
        InitializeComponent();
        Title = ArchiveTitle.Text = type == 1 ? "أرشيف المباني المملوكة" : "أرشيف المباني المؤجرة";
        AlbumIcon.ItemsSource = DocumentIcon.ItemsSource = ArchiveIcons.All;
        DocumentFlyout.AllowDrop = true;
        DocumentFlyout.Drop += File_Drop;
        DocumentFlyout.DragOver += File_DragOver;
        clickTimer.Tick += PreviewTimer_Tick;
        clickTimer.Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime());
        ApplyFilters();
    }

    private bool Allowed => PremisesSection.All.First(x => x.Register == PremisesRegister.Archive).CanOpen((PremisesKind)type)
        && App.location is { } location && location.Branch_ID == scope.BranchId && location.Location_ID == scope.LocationId && location.Location == scope.Location;

    // يفصل هذا الغلاف الأخطاء التشغيلية عن تحقق الخانات ويمنع النقر المكرر أثناء أي عملية قاعدة أو ماسح.
    private async Task Run(Func<ArchiveStore, Task> action)
    {
        if (busy || closed) return;
        if (!Allowed) { UiMessages.Show(this, "الأرشيف", "لا تملك صلاحية الأرشيف أو تغير موقع الدخول.", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        busy = true; Workspace.IsEnabled = false; DocumentFields.IsEnabled = false; AlbumFlyout.IsEnabled = false;
        try { await using var db = contextFactory(); await action(new(db, scope, type)); }
        catch (Exception ex) { if (!closed) UiMessages.Show(this, "الأرشيف", "تعذر إكمال العملية.\n" + ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; Workspace.IsEnabled = true; AlbumFlyout.IsEnabled = true; DocumentFields.IsEnabled = !viewing; ApplyFilters(); }
    }
    #endregion

    #region تحميل المستويات والتصفية والبحث
    private async void Window_Loaded(object sender, RoutedEventArgs e) => await Run(Reload);
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Run(Reload);
    private async Task Reload(ArchiveStore store)
    {
        // نعطي النافذة فرصة للرسم، ثم نقرأ المقرات. تُحدّث المستويات المفتوحة فقط عند التحديث أو الحفظ.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        var refreshedBuildings = await store.BuildingsAsync();
        var refreshedBuilding = refreshedBuildings.FirstOrDefault(x => x.Id == building?.Id);
        var refreshedAlbums = refreshedBuilding == null ? [] : await store.AlbumsAsync(refreshedBuilding.Id);
        var refreshedAlbum = refreshedAlbums.FirstOrDefault(x => x.Id == album?.Id);
        var refreshedDocuments = refreshedBuilding == null || refreshedAlbum == null
            ? [] : await store.DocumentsAsync(refreshedBuilding.Id, refreshedAlbum.Id);

        buildings = refreshedBuildings;
        building = refreshedBuilding;
        albums = refreshedAlbums;
        album = refreshedAlbum;
        documents = refreshedDocuments;
        // خيارات التصفية من المقرات المعروضة نفسها، لذلك لا نحتاج استعلامًا آخر أو ذاكرة مشتركة بين المواقع.
        var selectedLookup = BuildingSearch.SelectedValue;
        BuildingSearch.ItemsSource = buildings.Where(x => x.LookupId.HasValue).DistinctBy(x => x.LookupId).ToList();
        BuildingSearch.SelectedValue = selectedLookup;
        Status.Text = "تم تحديث الأرشيف • " + DateTime.Now.ToString("HH:mm");
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) { if (IsInitialized) ApplyFilters(); }
    private void ApplyFilters()
    {
        if (Buildings == null || Albums == null || Documents == null) return;
        bool Match(string value, string term) => value.Contains(term.Trim(), StringComparison.CurrentCultureIgnoreCase);
        // مصدر الفلترة هو اختيار الـ ComboBox الذي عدّله المستخدم، لا نص داخلي من TextBox.
        int? selectedBuildingLookup = (BuildingSearch.SelectedItem as ArchiveCard)?.LookupId;
        var filteredBuildings = selectedBuildingLookup == null
            ? buildings
            : buildings.Where(x => x.LookupId == selectedBuildingLookup).ToList();
        var filteredAlbums = albums.Where(x => Match(x.Title, AlbumSearch.Text)
            && (AlbumSearchDate.SelectedDate == null || x.Date == DateOnly.FromDateTime(AlbumSearchDate.SelectedDate.Value))).ToList();
        var filteredDocuments = documents.Where(x => Match(x.Title + " " + x.Subject, DocumentSearch.Text)).ToList();
        // لا يبقى تحديد مخفي بعد التصفية، حتى تكون قائمة الحذف الجماعي واضحة للمستخدم.
        foreach (var x in albums.Except(filteredAlbums).Concat(documents.Except(filteredDocuments))) x.IsSelected = false;
        Buildings.ItemsSource = filteredBuildings; Albums.ItemsSource = filteredAlbums; Documents.ItemsSource = filteredDocuments;
        // تعاد البطاقة المفتوحة كتحديد صريح بعد تحديث مصدر القائمة، فلا يختفي اسمها أو إطارها عند التصفية.
        Buildings.SelectedItem = building != null && filteredBuildings.Contains(building) ? building : null;
        BuildingStats.Text = $"{filteredBuildings.Count:N0} من {buildings.Count:N0} مبنى";
        // نجمع العدّ من بطاقات ألبومات المقر بعد تحميلها، من دون قراءة تفاصيل المستندات.
        BuildingSummary.Visibility = building == null ? Visibility.Collapsed : Visibility.Visible;
        BuildingSummary.Text = building == null ? "" : $"{building.Title}\n{albums.Count:N0} ألبوم  •  {albums.Sum(x => x.Count):N0} مستند";
        AlbumStats.Text = building == null ? "اختر مبنى بالنقر المزدوج" : $"{filteredAlbums.Count:N0} من {albums.Count:N0} ألبوم";
        DocumentStats.Text = album == null ? "اختر ألبومًا لعرض مستنداته" : $"{filteredDocuments.Count:N0} من {documents.Count:N0} مستند";
        AlbumsHeading.Text = building == null ? "ألبومات المبنى" : "ألبومات: " + building.Title;
        DocumentsHeading.Text = album == null ? "مستندات الألبوم" : "مستندات: " + album.Title;
        AddAlbumButton.IsEnabled = building != null && !busy;
        AddDocumentButton.IsEnabled = album != null && !busy;
    }

    // النقرة الواحدة تفتح المقر وتعرض اسمه وعدد ألبوماته ومستنداته مع إبقاء البطاقة محددة بحواف مستديرة.
    private async void Building_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || FindCard(e.OriginalSource) is not { } card || card.Id == building?.Id) return;
        Buildings.SelectedItem = card;
        clickTimer.Stop(); pendingPreview = pendingAlbum = null;
        await Run(async store =>
        {
            var selectedAlbums = await store.AlbumsAsync(card.Id);
            building = card; album = null; albums = selectedAlbums; documents = [];
            ClearLevelFilters();
            Status.Text = "تم اختيار المبنى: " + card.Title;
        });
    }

    // الفلتر يضيّق قائمة المقرات فقط، ويمسح ملخص المقر المفتوح إذا خرج من النتائج.
    private void BuildingSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || busy) return;
        if (building != null && BuildingSearch.SelectedItem is ArchiveCard filter && building.LookupId != filter.LookupId)
        {
            clickTimer.Stop(); pendingPreview = pendingAlbum = null;
            building = album = null; albums = []; documents = [];
            ClearLevelFilters();
        }
        ApplyFilters();
    }

    private void Album_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || e.ChangedButton != MouseButton.Left || sender is not FrameworkElement { DataContext: ArchiveCard card } || busy) return;
        e.Handled = true;
        clickTimer.Stop(); pendingPreview = null; pendingAlbum = null;
        if (e.ClickCount == 2) { OpenAlbum(card); return; }
        pendingAlbum = card; clickTimer.Start();
    }

    private void Document_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || e.ChangedButton != MouseButton.Left || sender is not FrameworkElement { DataContext: ArchiveCard card } || busy) return;
        e.Handled = true; clickTimer.Stop(); pendingAlbum = null;
        if (e.ClickCount == 2) { pendingPreview = null; _ = OpenDocument(card, false); }
        else { pendingPreview = card; clickTimer.Start(); }
    }

    private async void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        clickTimer.Stop(); var card = pendingPreview; pendingPreview = null;
        var selectedAlbum = pendingAlbum; pendingAlbum = null;
        if (selectedAlbum != null && albums.Contains(selectedAlbum) && !AlbumFlyout.IsOpen && !DocumentFlyout.IsOpen)
        {
            await Run(async store => { album = selectedAlbum; DocumentSearch.Clear(); documents = await store.DocumentsAsync(building!.Id, selectedAlbum.Id); });
            return;
        }
        if (card != null && documents.Contains(card) && !AlbumFlyout.IsOpen && !DocumentFlyout.IsOpen) await OpenDocument(card, true);
    }

    private static ArchiveCard? FindCard(object original)
    {
        DependencyObject? node = original as DependencyObject;
        while (node != null)
        {
            if (node is FrameworkElement { DataContext: ArchiveCard card }) return card;
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    private void ClearLevelFilters() { AlbumSearch.Clear(); AlbumSearchDate.SelectedDate = null; DocumentSearch.Clear(); }
    private void ClearAlbumFilter_Click(object sender, RoutedEventArgs e) { AlbumSearch.Clear(); AlbumSearchDate.SelectedDate = null; AlbumSearchDate.Text = ""; ApplyFilters(); }
    #endregion

    #region مسودة الألبوم والتحقق والحفظ
    private void AddAlbum_Click(object sender, RoutedEventArgs e) { if (building != null) OpenAlbum(null); }
    private void OpenAlbum(ArchiveCard? card)
    {
        if (busy || building == null || !Allowed) return;
        clickTimer.Stop(); pendingAlbum = pendingPreview = null; editedAlbum = card;
        AlbumTitle.Text = card?.Title ?? ""; AlbumDate.SelectedDate = card?.Date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        AlbumIcon.SelectedItem = ArchiveIcons.All.FirstOrDefault(x => x.Kind == card?.Icon) ?? ArchiveIcons.All[0];
        AlbumEditorTitle.Text = card == null ? "ألبوم جديد" : "تعديل الألبوم";
        ClearErrors(AlbumTitle, AlbumDate, AlbumIcon); AlbumFlyout.IsOpen = true;
        Dispatcher.BeginInvoke(() => AlbumTitle.Focus(), DispatcherPriority.Input);
    }

    private async void SaveAlbum_Click(object sender, RoutedEventArgs e)
    {
        if (busy || building == null) return;
        if (string.IsNullOrWhiteSpace(AlbumTitle.Text)) { FieldFeedback.Reject(AlbumTitle, "أدخل اسم الألبوم."); return; }
        if (!TryDate(AlbumDate, out var date)) return;
        if (AlbumIcon.SelectedItem is not ArchiveIcon icon) { FieldFeedback.Reject(AlbumIcon, "اختر أيقونة الألبوم."); return; }
        await Run(async store => { await store.SaveAlbumAsync(building.Id, editedAlbum?.Id, AlbumTitle.Text, date, icon.Kind.ToString()); AlbumFlyout.IsOpen = false; await Reload(store); });
    }

    private void CancelAlbum_Click(object sender, RoutedEventArgs e) { if (!busy) AlbumFlyout.IsOpen = false; }
    private void AlbumFlyout_Changed(object sender, RoutedEventArgs e)
    {
        if (AlbumFlyout.IsOpen) return;
        editedAlbum = null; AlbumTitle.Clear(); AlbumDate.SelectedDate = null; AlbumDate.Text = ""; AlbumIcon.SelectedItem = null;
        ClearErrors(AlbumTitle, AlbumDate, AlbumIcon);
    }

    private static bool TryDate(DatePicker field, out DateOnly date)
    {
        if (!string.IsNullOrWhiteSpace(field.Text) && DateTime.TryParse(field.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
        { date = DateOnly.FromDateTime(parsed); return true; }
        date = default; return FieldFeedback.Reject(field, "أدخل تاريخًا صحيحًا.");
    }
    private static void ClearErrors(params Control[] fields) { foreach (var field in fields) FieldFeedback.Clear(field); }
    #endregion

    #region مسودة المستند والمعاينة والاستيراد والمسح
    private async void AddDocument_Click(object sender, RoutedEventArgs e) { if (album != null) await OpenDocument(null, false); }
    private async Task OpenDocument(ArchiveCard? card, bool readOnly)
    {
        if (building == null || album == null || busy) return;
        await Run(async store =>
        {
            editedDocument = card; replacement = null; viewing = readOnly; ++draftVersion;
            DocumentTitle.Text = card?.Title ?? ""; DocumentDate.SelectedDate = card?.Date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
            DocumentSubject.Text = card?.Subject ?? "";
            DocumentIcon.SelectedItem = ArchiveIcons.All.FirstOrDefault(x => x.Kind == card?.Icon) ?? ArchiveIcons.All[1];
            DocumentEditorTitle.Text = readOnly ? "معاينة المستند" : card == null ? "مستند جديد" : "تعديل المستند";
            SaveDocumentButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
            ClearPreview(); AttachmentName.Text = card == null ? "" : "الملف المؤرشف";
            FileStatus.Text = card == null ? "PDF أو صور • الحد 100 ميجابايت" : "إدراج ملف جديد يستبدل الملف المحفوظ عند الحفظ فقط";
            ClearErrors(DocumentTitle, DocumentDate, DocumentSubject, DocumentIcon, ScannerChoice, AttachmentName);
            // يفتح أولًا بعارض PDF المرئي ثم يحمّل بايتات المستند؛ عناصر PDF الأصلية لا ترسم مصدرها جيدًا وهي داخل Flyout مغلق.
            DocumentFlyout.IsOpen = true;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            if (card != null) ShowBytes(await store.ReadAsync(building.Id, album.Id, card.Id, card.Legacy));
            // تملأ قائمة الماسحات عند كل فتح للإضافة أو التعديل، مع إبقاء زر التحديث متاحًا لاحقًا.
            await LoadScannersAsync();
        });
    }

    // يبدّل مصدر المعاينة بعد تجهيز PDF، ويبقى ملف المسودة الأصلي منفصلًا عن نسخة العرض.
    private void ShowBytes(byte[] bytes)
    {
        var pdf = ArchiveFiles.CreatePreviewPdf(bytes);
        SetPreviewPdf(pdf);
        PreviewHint.Visibility = Visibility.Collapsed;
    }
    private void ClearPreview()
    {
        // تعرض صفحة المسودة المؤقتة شريط الأدوات قبل اختيار الملف.
        SetPreviewPdf(ArchiveFiles.EmptyPdf());
        PreviewHint.Visibility = Visibility.Visible;
    }

    // يحتفظ العارض بمجرى القراءة ما دام يعرض الصفحة الحالية.
    private void SetPreviewPdf(byte[] pdf)
    {
        ReleasePreviewPdf();
        previewStream = new MemoryStream(pdf, false);
        PdfViewer.DocumentSource = previewStream;
    }

    // يفصل تحرير مصدر العارض عن إنشاء صفحة المسودة كي لا يبقى تيار صفحة مؤقتة عند تحميل ملف المستخدم.
    private void ReleasePreviewPdf()
    {
        PdfViewer.DocumentSource = null; previewStream?.Dispose(); previewStream = null;
    }
    private void PdfViewer_DocumentClosing(object sender, DevExpress.Xpf.PdfViewer.DocumentClosingEventArgs e)
    {
        // العارض للقراءة؛ لا نحفظ تعليماته أو تعديلاته ضمن الأرشيف دون أمر حفظ المسودة.
        e.SaveDialogResult = MessageBoxResult.No;
    }

    private void PdfViewer_ExceptionMessage(object sender, DevExpress.Xpf.PdfViewer.ExceptionMessageEventArgs e)
    {
        // لا نترك العارض يفتح رسالة أجنبية؛ نعالج فشل العرض بالرسالة العربية المعتمدة دون تغيير الملف.
        e.MessageAction = DevExpress.Xpf.PdfViewer.ExceptionMessageAction.Ignore;
        int version = draftVersion;
        var message = e.Exception?.Message ?? e.Message;
        Dispatcher.BeginInvoke(() =>
        {
            if (!closed && DocumentFlyout.IsOpen && version == draftVersion)
                UiMessages.Show(this, "معاينة المستند", "تعذر عرض ملف PDF.\n" + message, MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (busy || viewing) return;
        var dialog = new OpenFileDialog { Filter = "PDF والصور|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif", Multiselect = false };
        if (dialog.ShowDialog(this) == true) await Import(dialog.FileName);
    }
    private async Task Import(string path) => await Run(async _ =>
    {
        var bytes = await Task.Run(() => ArchiveFiles.Read(path)); ShowBytes(bytes); replacement = bytes;
        AttachmentName.Text = Path.GetFileName(path);
        FileStatus.Text = $"{Path.GetFileName(path)} • {bytes.Length / 1024:N0} كيلوبايت";
        FileStatus.Foreground = Brushes.SlateGray;
    });
    private void File_DragOver(object sender, DragEventArgs e) { e.Effects = !viewing && !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void File_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!busy && !viewing && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths) await Import(paths[0]);
    }
    private async Task LoadScannersAsync()
    {
        var selected = ScannerChoice.SelectedItem;
        try
        {
            var devices = await Task.Run(ArchiveFiles.Scanners); ScannerChoice.ItemsSource = devices;
            ScannerChoice.SelectedItem = devices.Contains(selected as string) ? selected : devices.FirstOrDefault();
            if (devices.Length == 0) FileStatus.Text = "لا توجد ماسحات TWAIN متاحة؛ يمكنك إدراج ملف من الحاسوب.";
        }
        catch (Exception ex)
        {
            // فشل تعريف TWAIN لا يمنع فتح أو تعديل المستند ولا يحجب الاستيراد من الحاسوب.
            ScannerChoice.ItemsSource = Array.Empty<string>(); ScannerChoice.SelectedItem = null;
            FileStatus.Text = "تعذر تحديث الماسحات: " + ex.Message;
        }
    }
    private async void RefreshScanners_Click(object sender, RoutedEventArgs e) => await Run(async _ => await LoadScannersAsync());
    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (busy || viewing) return;
        if (ScannerChoice.SelectedItem is not string scanner) { FieldFeedback.Reject(ScannerChoice, "حدّث قائمة الماسحات ثم اختر الماسح."); return; }
        int version = draftVersion;
        await Run(async _ =>
        {
            FileStatus.Text = "جارٍ المسح الضوئي…";
            var bytes = await ArchiveFiles.ScanAsync(scanner);
            if (version != draftVersion || !DocumentFlyout.IsOpen) return;
            ShowBytes(bytes); replacement = bytes; AttachmentName.Text = "مستند من الماسح الضوئي.pdf";
            FileStatus.Text = $"تم المسح • {bytes.Length / 1024:N0} كيلوبايت"; FileStatus.Foreground = Brushes.SlateGray;
        });
    }

    private async void SaveDocument_Click(object sender, RoutedEventArgs e)
    {
        if (busy || viewing || building == null || album == null) return;
        if (string.IsNullOrWhiteSpace(DocumentTitle.Text)) { FieldFeedback.Reject(DocumentTitle, "أدخل عنوان المستند."); return; }
        if (!TryDate(DocumentDate, out var date)) return;
        if (DocumentIcon.SelectedItem is not ArchiveIcon icon) { FieldFeedback.Reject(DocumentIcon, "اختر أيقونة المستند."); return; }
        if (editedDocument == null && replacement == null) { FieldFeedback.Reject(AttachmentName, "أدرج ملفًا أو استخدم الماسح قبل الحفظ."); return; }
        await Run(async store =>
        {
            await store.SaveDocumentAsync(building.Id, album.Id, editedDocument?.Id, editedDocument?.Legacy == true,
                DocumentTitle.Text, date, DocumentSubject.Text, icon.Kind.ToString(), replacement);
            DocumentFlyout.IsOpen = false; await Reload(store);
        });
    }
    private void CancelDocument_Click(object sender, RoutedEventArgs e) { if (!busy) DocumentFlyout.IsOpen = false; }
    private void DocumentFlyout_Changed(object sender, RoutedEventArgs e)
    {
        if (DocumentFlyout.IsOpen || clearing) return;
        clearing = true;
        try
        {
            // بعد الإغلاق لا نحتاج صفحة المسودة؛ تنشأ فقط عند فتح محرر مستند جديد أو قائم.
            ++draftVersion; ReleasePreviewPdf(); editedDocument = null; replacement = null; viewing = false;
            DocumentTitle.Clear(); DocumentDate.SelectedDate = null; DocumentDate.Text = ""; DocumentSubject.Clear(); DocumentIcon.SelectedItem = null;
            ScannerChoice.ItemsSource = null; ScannerChoice.SelectedItem = null; AttachmentName.Clear(); FileStatus.Text = ""; FileStatus.Foreground = Brushes.SlateGray;
            ClearErrors(DocumentTitle, DocumentDate, DocumentSubject, DocumentIcon, ScannerChoice, AttachmentName);
        }
        finally { clearing = false; }
    }
    #endregion

    #region الحذف الفردي والجماعي وإغلاق النافذة بأمان
    private void SelectAlbums_Click(object sender, RoutedEventArgs e) { foreach (var row in Albums.Items.Cast<ArchiveCard>()) row.IsSelected = true; }
    private void ClearAlbums_Click(object sender, RoutedEventArgs e) { foreach (var row in albums) row.IsSelected = false; }
    private void SelectDocuments_Click(object sender, RoutedEventArgs e) { foreach (var row in Documents.Items.Cast<ArchiveCard>()) row.IsSelected = true; }
    private void ClearDocuments_Click(object sender, RoutedEventArgs e) { foreach (var row in documents) row.IsSelected = false; }
    private async void DeleteAlbum_Click(object sender, RoutedEventArgs e) { e.Handled = true; if (sender is FrameworkElement { DataContext: ArchiveCard card }) await DeleteAlbums([card]); }

    private async void DeleteDocument_Click(object sender, RoutedEventArgs e) { e.Handled = true; if (sender is FrameworkElement { DataContext: ArchiveCard card }) await DeleteDocuments([card]); }
    private async void DeleteSelectedAlbums_Click(object sender, RoutedEventArgs e) => await DeleteAlbums(albums.Where(x => x.IsSelected).ToArray());
    private async void DeleteSelectedDocuments_Click(object sender, RoutedEventArgs e) => await DeleteDocuments(documents.Where(x => x.IsSelected).ToArray());
    private async Task DeleteAlbums(ArchiveCard[] cards)
    {
        if (busy || building == null || cards.Length == 0) return;
        clickTimer.Stop(); pendingPreview = null;
        if (UiMessages.Show(this, "حذف الألبومات", $"هل تريد حذف {cards.Length} ألبوم بكامل مستنداته؟\n{string.Join("، ", cards.Take(5).Select(x => x.Title))}\nلا يمكن التراجع عن الحذف.", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Run(async store => { await store.DeleteAlbumsAsync(building.Id, cards.Select(x => x.Id).ToArray()); await Reload(store); });
    }
    private async Task DeleteDocuments(ArchiveCard[] cards)
    {
        if (busy || building == null || album == null || cards.Length == 0) return;
        clickTimer.Stop(); pendingPreview = null;
        if (UiMessages.Show(this, "حذف المستندات", $"هل تريد حذف {cards.Length} مستند؟\n{string.Join("، ", cards.Take(5).Select(x => x.Title))}\nلا يمكن التراجع عن الحذف.", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Run(async store => { await store.DeleteDocumentsAsync(building.Id, album.Id, cards); await Reload(store); });
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // يمر الاستيراد من أمر إدراج الملف فقط حتى تتطابق المعاينة مع الملف الذي سيحفظ.
        if (DocumentFlyout.IsOpen && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.O) { e.Handled = true; return; }
        if (e.Key != Key.Escape || busy) return;
        if (DocumentFlyout.IsOpen) DocumentFlyout.IsOpen = false;
        else if (AlbumFlyout.IsOpen) AlbumFlyout.IsOpen = false;
        e.Handled = true;
    }
    private void Close_Click(object sender, RoutedEventArgs e) { if (!busy) Close(); }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; return; }
        closed = true; ++draftVersion; clickTimer.Stop(); clickTimer.Tick -= PreviewTimer_Tick; pendingPreview = pendingAlbum = null;
        ReleasePreviewPdf(); replacement = null; documents.Clear(); albums.Clear(); buildings.Clear();
    }
    #endregion
}
