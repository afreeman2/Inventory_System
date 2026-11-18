using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.Automation;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;

namespace Inventory_System.viewo_Model;

public sealed class AccessibleGridControl : GridControl
{
    #region مزوّدو إمكانية الوصول لرؤوس الأعمدة
    // يبقى الجدول على مزوّد DevExpress الأصلي مع استبدال فحص التركيز الخاص برأس العمود فقط.
    protected override AutomationPeer OnCreateAutomationPeer() => new TableAutomationPeer(this);

    private sealed class TableAutomationPeer(GridControl owner) : GridControlAutomationPeer(owner)
    {
        protected override HeaderPanelAutomationPeerBase CreateHeaderPeer() => new HeaderAutomationPeer(GridControl);
    }

    private sealed class HeaderAutomationPeer(GridControl owner) : HeaderPanelAutomationPeer(owner)
    {
        protected override List<AutomationPeer> GetChildrenCore()
        {
            var children = base.GetChildrenCore();
            // ينشئ DevExpress رؤوسًا منطقية حتى قبل ظهور الأعمدة، وقد يعيد استخدامها من ذاكرته المؤقتة.
            foreach (var entry in HeaderPeers.ToArray())
                if (entry.Value is ColumnHeaderAutomationPeer peer && peer is not SafeColumnHeaderPeer)
                    HeaderPeers[entry.Key] = new SafeColumnHeaderPeer(DataControl, peer.Column);

            for (int i = 0; i < children.Count; i++)
                if (children[i] is ColumnHeaderAutomationPeer peer && peer is not SafeColumnHeaderPeer)
                    children[i] = HeaderPeers.Values.OfType<SafeColumnHeaderPeer>()
                        .FirstOrDefault(cached => ReferenceEquals(cached.Column, peer.Column))
                        ?? new SafeColumnHeaderPeer(DataControl, peer.Column);
            return children;
        }

        protected override ColumnHeaderAutomationPeerBase CreateColumnHeaderAutomationPeer(GridColumnHeader header) =>
            new SafeColumnHeaderPeer(DataControl, header);
    }

    private sealed class SafeColumnHeaderPeer : ColumnHeaderAutomationPeer
    {
        private readonly GridControl grid;
        private readonly GridColumnHeader? header;

        public SafeColumnHeaderPeer(GridControl grid, GridColumnHeader header) : base(grid, header)
        {
            this.grid = grid;
            this.header = header;
        }

        public SafeColumnHeaderPeer(GridControl grid, GridColumn column) : base(grid, column)
        {
            this.grid = grid;
        }

        // يفحص التركيز من الرأس المرئي، لأن رأسًا لم يُحمّل أو انفصل عند الإغلاق لا يملك عنصر تركيز.
        protected override bool HasKeyboardFocusCore()
        {
            if (header is not null) return header.IsLoaded && header.IsKeyboardFocusWithin;
            if (!grid.IsLoaded || !grid.IsKeyboardFocusWithin) return false;

            for (DependencyObject? element = Keyboard.FocusedElement as DependencyObject;
                element is Visual; element = VisualTreeHelper.GetParent(element))
                if (element is GridColumnHeader focusedHeader)
                    return focusedHeader.IsLoaded && ReferenceEquals(BaseGridHeader.GetGridColumn(focusedHeader), Column);
            return false;
        }
    }
    #endregion
}
